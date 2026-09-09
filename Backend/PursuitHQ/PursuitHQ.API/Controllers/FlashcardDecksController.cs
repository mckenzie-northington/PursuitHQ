using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.StudyTools;
using PursuitHQ.API.Models;
using PursuitHQ.API.Services;

namespace PursuitHQ.API.Controllers
{
    /// <summary>
    /// Flashcard decks: generated from course material by AI, then edited and
    /// reviewed by hand.
    /// </summary>
    [Route("api/flashcard-decks")]
    public class FlashcardDecksController : ApiControllerBase
    {
        private const int MinCards = 5;
        private const int MaxCards = 40;

        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _storage;
        private readonly ITextExtractionService _textExtraction;
        private readonly IStudyToolAiService _studyTools;
        private readonly IAiUsageLimiter _limiter;
        private readonly ILogger<FlashcardDecksController> _logger;

        public FlashcardDecksController(
            ApplicationDbContext db,
            IFileStorageService storage,
            ITextExtractionService textExtraction,
            IStudyToolAiService studyTools,
            IAiUsageLimiter limiter,
            ILogger<FlashcardDecksController> logger)
        {
            _db = db;
            _storage = storage;
            _textExtraction = textExtraction;
            _studyTools = studyTools;
            _limiter = limiter;
            _logger = logger;
        }

        /// <summary>
        /// Whether AI generation is available, and how much of today's
        /// allowance is left. The UI asks this so it can explain a disabled
        /// button instead of failing after the student clicks it.
        /// </summary>
        [HttpGet("ai-status")]
        public ActionResult<object> GetAiStatus()
        {
            var usage = _limiter.Peek(CurrentUserId);

            return Ok(new
            {
                configured = _studyTools.IsConfigured,
                used = usage.Used,
                limit = usage.Limit,
                remaining = usage.Remaining
            });
        }

        [HttpGet]
        public async Task<ActionResult<List<FlashcardDeckSummaryDto>>> GetDecks(
            [FromQuery] int? courseId)
        {
            var query = _db.FlashcardDecks.Where(d => d.UserId == CurrentUserId);

            if (courseId.HasValue) query = query.Where(d => d.CourseId == courseId.Value);

            var decks = await query
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new FlashcardDeckSummaryDto
                {
                    Id = d.Id,
                    CourseId = d.CourseId,
                    CourseName = d.Course != null ? d.Course.Name : null,
                    Title = d.Title,
                    IsAiGenerated = d.IsAiGenerated,
                    CardCount = d.Flashcards.Count,
                    SourceName = d.SourceMaterial != null
                        ? d.SourceMaterial.FileName
                        : d.SourceNote != null ? d.SourceNote.Title : null,
                    CreatedAt = d.CreatedAt
                })
                .ToListAsync();

            return Ok(decks);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<FlashcardDeckDto>> GetDeck(int id)
        {
            var deck = await _db.FlashcardDecks
                .Include(d => d.Course)
                .Include(d => d.SourceMaterial)
                .Include(d => d.SourceNote)
                .Include(d => d.Flashcards)
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == CurrentUserId);

            if (deck is null) return NotFound(DeckNotFound());

            return Ok(ToDto(deck));
        }

        /// <summary>
        /// Reads a file or note and writes flashcards from it.
        ///
        /// Nothing is saved unless the AI produced usable cards - a deck of
        /// three broken cards is worse than no deck, because it looks finished.
        /// </summary>
        [HttpPost("generate")]
        public async Task<ActionResult<FlashcardDeckDto>> Generate(
            GenerateFlashcardsDto dto, CancellationToken ct)
        {
            if (!_studyTools.IsConfigured)
            {
                return StatusCode(503, new ApiErrorDto(
                    "AiNotConfigured",
                    "AI is not set up on this server. Set the Ai:ApiKey user secret and restart the API."));
            }

            if ((dto.SourceMaterialId is null) == (dto.SourceNoteId is null))
            {
                return BadRequest(new ApiErrorDto(
                    "InvalidSource",
                    "Pick exactly one source: either an uploaded file or a note."));
            }

            var usage = _limiter.Peek(CurrentUserId);
            if (usage.Exceeded)
            {
                return StatusCode(429, new ApiErrorDto(
                    "DailyLimitReached",
                    $"You have used all {usage.Limit} AI requests for today. The count resets at midnight."));
            }

            var owns = await _db.Courses
                .AnyAsync(c => c.Id == dto.CourseId && c.UserId == CurrentUserId, ct);

            if (!owns)
            {
                return NotFound(new ApiErrorDto(
                    "CourseNotFound", "That course does not exist, or it does not belong to you."));
            }

            var source = await ResolveSourceAsync(dto, ct);
            if (source.Error is not null) return source.Error;

            var count = Math.Clamp(dto.Count, MinCards, MaxCards);

            // Counted before the call, not after: the request is what spends
            // the upstream quota, whether or not the answer turns out usable.
            _limiter.Consume(CurrentUserId);

            List<GeneratedFlashcard> generated;
            try
            {
                generated = await _studyTools.GenerateFlashcardsAsync(
                    source.Text!, source.Name!, count, ct);
            }
            catch (StudyToolException ex)
            {
                return StatusCode(502, new ApiErrorDto("GenerationFailed", ex.Message));
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "AI provider call failed");

                // GeminiAiService passes the provider's own explanation through,
                // which is far more useful than "something went wrong".
                return StatusCode(502, new ApiErrorDto("AiUnavailable", ex.Message));
            }

            var deck = new FlashcardDeck
            {
                UserId = CurrentUserId,
                CourseId = dto.CourseId,
                SourceMaterialId = dto.SourceMaterialId,
                SourceNoteId = dto.SourceNoteId,
                Title = string.IsNullOrWhiteSpace(dto.Title) ? source.Name! : dto.Title.Trim(),
                IsAiGenerated = true,
                CreatedAt = DateTime.UtcNow,
                Flashcards = generated
                    .Select((card, index) => new Flashcard
                    {
                        Front = card.Front,
                        Back = card.Back,
                        Order = index
                    })
                    .ToList()
            };

            _db.FlashcardDecks.Add(deck);
            await _db.SaveChangesAsync(ct);

            await _db.Entry(deck).Reference(d => d.Course).LoadAsync(ct);

            var result = ToDto(deck);
            result.SourceName = source.Name;

            return CreatedAtAction(nameof(GetDeck), new { id = deck.Id }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<FlashcardDeckSummaryDto>> RenameDeck(int id, RenameDeckDto dto)
        {
            var deck = await _db.FlashcardDecks
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == CurrentUserId);

            if (deck is null) return NotFound(DeckNotFound());

            deck.Title = dto.Title.Trim();
            await _db.SaveChangesAsync();

            return Ok(new FlashcardDeckSummaryDto
            {
                Id = deck.Id,
                CourseId = deck.CourseId,
                Title = deck.Title,
                IsAiGenerated = deck.IsAiGenerated,
                CreatedAt = deck.CreatedAt
            });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteDeck(int id)
        {
            var deck = await _db.FlashcardDecks
                .Include(d => d.Flashcards)
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == CurrentUserId);

            if (deck is null) return NotFound(DeckNotFound());

            _db.FlashcardDecks.Remove(deck);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        // ---------- cards ----------

        [HttpPost("{deckId:int}/cards")]
        public async Task<ActionResult<FlashcardDto>> AddCard(int deckId, SaveFlashcardDto dto)
        {
            var deck = await _db.FlashcardDecks
                .Include(d => d.Flashcards)
                .FirstOrDefaultAsync(d => d.Id == deckId && d.UserId == CurrentUserId);

            if (deck is null) return NotFound(DeckNotFound());

            var card = new Flashcard
            {
                DeckId = deck.Id,
                Front = dto.Front.Trim(),
                Back = dto.Back.Trim(),
                Order = deck.Flashcards.Count == 0 ? 0 : deck.Flashcards.Max(c => c.Order) + 1
            };

            _db.Flashcards.Add(card);
            await _db.SaveChangesAsync();

            return Ok(ToDto(card));
        }

        [HttpPut("{deckId:int}/cards/{cardId:int}")]
        public async Task<ActionResult<FlashcardDto>> UpdateCard(
            int deckId, int cardId, SaveFlashcardDto dto)
        {
            var card = await FindCardAsync(deckId, cardId);
            if (card is null) return NotFound(CardNotFound());

            card.Front = dto.Front.Trim();
            card.Back = dto.Back.Trim();
            await _db.SaveChangesAsync();

            return Ok(ToDto(card));
        }

        [HttpDelete("{deckId:int}/cards/{cardId:int}")]
        public async Task<IActionResult> DeleteCard(int deckId, int cardId)
        {
            var card = await FindCardAsync(deckId, cardId);
            if (card is null) return NotFound(CardNotFound());

            _db.Flashcards.Remove(card);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// Records how a card went during review, so the deck can be drilled
        /// down to the cards that keep being missed.
        /// </summary>
        [HttpPost("{deckId:int}/cards/{cardId:int}/review")]
        public async Task<ActionResult<FlashcardDto>> ReviewCard(
            int deckId, int cardId, ReviewFlashcardDto dto)
        {
            var card = await FindCardAsync(deckId, cardId);
            if (card is null) return NotFound(CardNotFound());

            card.TimesReviewed++;
            if (dto.Correct) card.TimesCorrect++;

            await _db.SaveChangesAsync();

            return Ok(ToDto(card));
        }

        // ---------- helpers ----------

        private record SourceText(string? Text, string? Name, ActionResult? Error);

        /// <summary>
        /// Turns the requested source into plain text, or into the response
        /// explaining why it could not be.
        /// </summary>
        private async Task<SourceText> ResolveSourceAsync(GenerateFlashcardsDto dto, CancellationToken ct)
        {
            if (dto.SourceNoteId is int noteId)
            {
                var note = await _db.Notes.FirstOrDefaultAsync(
                    n => n.Id == noteId && n.UserId == CurrentUserId && n.CourseId == dto.CourseId, ct);

                if (note is null)
                {
                    return new SourceText(null, null, NotFound(new ApiErrorDto(
                        "NoteNotFound", "That note does not exist in this course.")));
                }

                if (string.IsNullOrWhiteSpace(note.Content))
                {
                    return new SourceText(null, null, BadRequest(new ApiErrorDto(
                        "SourceEmpty", "That note is empty, so there is nothing to make cards from.")));
                }

                return new SourceText(note.Content, note.Title, null);
            }

            var material = await _db.StudyMaterials.FirstOrDefaultAsync(
                m => m.Id == dto.SourceMaterialId
                     && m.UserId == CurrentUserId
                     && m.CourseId == dto.CourseId, ct);

            if (material is null)
            {
                return new SourceText(null, null, NotFound(new ApiErrorDto(
                    "MaterialNotFound", "That file does not exist in this course.")));
            }

            if (!_textExtraction.CanExtract(material.FileName, material.ContentType))
            {
                return new SourceText(null, null, BadRequest(new ApiErrorDto(
                    "UnsupportedFile",
                    "Flashcards can only be made from PDF, Word, PowerPoint, and text files.")));
            }

            try
            {
                await using var stream = await _storage.OpenAsync(material.StoredPath, ct);

                var extracted = await _textExtraction.ExtractAsync(
                    stream, material.FileName, material.ContentType,
                    maxCharacters: StudyToolAiService.MaxSourceCharacters, ct: ct);

                if (extracted.IsEmpty)
                {
                    // A scanned PDF or a deck of picture-only slides has no
                    // text layer to read.
                    return new SourceText(null, null, StatusCode(422, new ApiErrorDto(
                        "NoTextFound",
                        "No readable text was found in that file. Scanned documents and slides "
                        + "made entirely of images have no text to work from.")));
                }

                return new SourceText(extracted.Text, material.FileName, null);
            }
            catch (FileNotFoundException)
            {
                return new SourceText(null, null, NotFound(new ApiErrorDto(
                    "FileMissing", "The stored file could not be found.")));
            }
        }

        private Task<Flashcard?> FindCardAsync(int deckId, int cardId) =>
            _db.Flashcards.FirstOrDefaultAsync(c =>
                c.Id == cardId
                && c.DeckId == deckId
                && c.Deck!.UserId == CurrentUserId);

        private static ApiErrorDto DeckNotFound() =>
            new("DeckNotFound", "That deck does not exist, or it does not belong to you.");

        private static ApiErrorDto CardNotFound() =>
            new("CardNotFound", "That card does not exist, or it does not belong to you.");

        private static FlashcardDto ToDto(Flashcard c) => new()
        {
            Id = c.Id,
            Front = c.Front,
            Back = c.Back,
            Order = c.Order,
            TimesReviewed = c.TimesReviewed,
            TimesCorrect = c.TimesCorrect
        };

        private static FlashcardDeckDto ToDto(FlashcardDeck d) => new()
        {
            Id = d.Id,
            CourseId = d.CourseId,
            CourseName = d.Course?.Name,
            Title = d.Title,
            IsAiGenerated = d.IsAiGenerated,
            CardCount = d.Flashcards.Count,
            SourceName = d.SourceMaterial?.FileName ?? d.SourceNote?.Title,
            CreatedAt = d.CreatedAt,
            Cards = d.Flashcards
                .OrderBy(c => c.Order)
                .Select(ToDto)
                .ToList()
        };
    }
}
