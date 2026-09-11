using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.StudyChat;
using PursuitHQ.API.Models;
using PursuitHQ.API.Services;

namespace PursuitHQ.API.Controllers
{
    /// <summary>
    /// The study tutor: conversations scoped to a course, and the guides they
    /// produce.
    /// </summary>
    [Route("api/study")]
    public class StudyController : ApiControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _storage;
        private readonly ITextExtractionService _textExtraction;
        private readonly IStudyChatService _chat;
        private readonly IAiUsageLimiter _limiter;
        private readonly ILogger<StudyController> _logger;

        public StudyController(
            ApplicationDbContext db,
            IFileStorageService storage,
            ITextExtractionService textExtraction,
            IStudyChatService chat,
            IAiUsageLimiter limiter,
            ILogger<StudyController> logger)
        {
            _db = db;
            _storage = storage;
            _textExtraction = textExtraction;
            _chat = chat;
            _limiter = limiter;
            _logger = logger;
        }

        // ---------- conversations ----------

        [HttpGet("conversations")]
        public async Task<ActionResult<List<ConversationSummaryDto>>> GetConversations(
            [FromQuery] int? courseId)
        {
            var query = _db.StudyConversations.Where(c => c.UserId == CurrentUserId);
            if (courseId.HasValue) query = query.Where(c => c.CourseId == courseId.Value);

            var conversations = await query
                .OrderByDescending(c => c.UpdatedAt)
                .Select(c => new ConversationSummaryDto
                {
                    Id = c.Id,
                    CourseId = c.CourseId,
                    CourseName = c.Course != null ? c.Course.Name : null,
                    Title = c.Title,
                    MessageCount = c.Messages.Count,
                    UpdatedAt = c.UpdatedAt
                })
                .ToListAsync();

            return Ok(conversations);
        }

        [HttpGet("conversations/{id:int}")]
        public async Task<ActionResult<ConversationDto>> GetConversation(int id)
        {
            var conversation = await LoadAsync(id);
            if (conversation is null) return NotFound(ConversationNotFound());

            return Ok(ToDto(conversation));
        }

        [HttpPost("conversations")]
        public async Task<ActionResult<ConversationDto>> StartConversation(StartConversationDto dto)
        {
            var course = await _db.Courses
                .FirstOrDefaultAsync(c => c.Id == dto.CourseId && c.UserId == CurrentUserId);

            if (course is null)
            {
                return NotFound(new ApiErrorDto(
                    "CourseNotFound", "That course does not exist, or it does not belong to you."));
            }

            var conversation = new StudyConversation
            {
                UserId = CurrentUserId,
                CourseId = dto.CourseId,
                Title = string.IsNullOrWhiteSpace(dto.Title) ? "Study session" : dto.Title.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.StudyConversations.Add(conversation);
            await _db.SaveChangesAsync();

            conversation.Course = course;

            return CreatedAtAction(nameof(GetConversation), new { id = conversation.Id },
                ToDto(conversation));
        }

        /// <summary>Chooses which files and notes the tutor works from.</summary>
        [HttpPut("conversations/{id:int}/sources")]
        public async Task<ActionResult<ConversationDto>> SetSources(int id, SetSourcesDto dto)
        {
            var conversation = await LoadAsync(id);
            if (conversation is null) return NotFound(ConversationNotFound());

            // Only ids the student actually owns in this course are stored, so
            // a hand-edited request cannot pull another course's files into a
            // prompt.
            var materialIds = await _db.StudyMaterials
                .Where(m => dto.SourceMaterialIds.Contains(m.Id)
                            && m.UserId == CurrentUserId
                            && m.CourseId == conversation.CourseId)
                .Select(m => m.Id)
                .ToListAsync();

            var noteIds = await _db.Notes
                .Where(n => dto.SourceNoteIds.Contains(n.Id)
                            && n.UserId == CurrentUserId
                            && n.CourseId == conversation.CourseId)
                .Select(n => n.Id)
                .ToListAsync();

            conversation.SourceMaterialIds = Join(materialIds);
            conversation.SourceNoteIds = Join(noteIds);
            conversation.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(ToDto(conversation));
        }

        /// <summary>
        /// Renames a session.
        ///
        /// The first question becomes the title automatically, which is a
        /// reasonable guess and often not what you would have called it.
        /// </summary>
        [HttpPut("conversations/{id:int}")]
        public async Task<ActionResult<ConversationSummaryDto>> RenameConversation(
            int id, RenameConversationDto dto)
        {
            var conversation = await _db.StudyConversations
                .Include(c => c.Course)
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == CurrentUserId);

            if (conversation is null) return NotFound(ConversationNotFound());

            conversation.Title = dto.Title.Trim();
            await _db.SaveChangesAsync();

            return Ok(new ConversationSummaryDto
            {
                Id = conversation.Id,
                CourseId = conversation.CourseId,
                CourseName = conversation.Course?.Name,
                Title = conversation.Title,
                MessageCount = conversation.Messages.Count,
                UpdatedAt = conversation.UpdatedAt
            });
        }

        [HttpDelete("conversations/{id:int}")]
        public async Task<IActionResult> DeleteConversation(int id)
        {
            var conversation = await _db.StudyConversations
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == CurrentUserId);

            if (conversation is null) return NotFound(ConversationNotFound());

            _db.StudyConversations.Remove(conversation);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// Asks a question and returns the tutor's reply.
        ///
        /// Both turns are saved before returning, so a refresh never loses the
        /// question that was just asked.
        /// </summary>
        [HttpPost("conversations/{id:int}/ask")]
        public async Task<ActionResult<StudyMessageDto>> Ask(int id, AskDto dto, CancellationToken ct)
        {
            if (!_chat.IsConfigured)
            {
                return StatusCode(503, new ApiErrorDto(
                    "AiNotConfigured",
                    "AI is not set up on this server. Set the Ai:ApiKey user secret and restart the API."));
            }

            var conversation = await LoadAsync(id);
            if (conversation is null) return NotFound(ConversationNotFound());

            var usage = _limiter.Peek(CurrentUserId);
            if (usage.Exceeded)
            {
                return StatusCode(429, new ApiErrorDto(
                    "DailyLimitReached",
                    $"You have used all {usage.Limit} AI requests for today. The count resets at midnight."));
            }

            var question = dto.Question.Trim();

            var history = conversation.Messages
                .OrderBy(m => m.CreatedAt)
                .TakeLast(StudyChatService.MaxHistoryTurns)
                .Select(m => new StudyChatTurn(m.Role, m.Content))
                .ToList();

            var sourceText = await BuildSourceTextAsync(conversation, ct);

            _limiter.Consume(CurrentUserId);

            StudyChatReply reply;
            try
            {
                reply = await _chat.AskAsync(
                    new StudyChatRequest(question, conversation.Course?.Name ?? "this course",
                        sourceText, history),
                    ct);
            }
            catch (StudyToolException ex)
            {
                return StatusCode(502, new ApiErrorDto("GenerationFailed", ex.Message));
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Study chat call failed");
                return StatusCode(502, new ApiErrorDto("AiUnavailable", ex.Message));
            }

            var now = DateTime.UtcNow;

            var userMessage = new StudyMessage
            {
                ConversationId = conversation.Id,
                Role = StudyMessageRole.User,
                Content = question,
                CreatedAt = now
            };

            var assistantMessage = new StudyMessage
            {
                ConversationId = conversation.Id,
                Role = StudyMessageRole.Assistant,
                Content = reply.Text,
                ArtifactKind = reply.Artifact?.Kind ?? StudyArtifactKind.None,
                ArtifactTitle = reply.Artifact?.Title,
                ArtifactContent = reply.Artifact?.Content,
                // A tick later, so ordering by CreatedAt always puts the answer
                // after the question even when both are written in the same
                // millisecond.
                CreatedAt = now.AddMilliseconds(1)
            };

            _db.StudyMessages.AddRange(userMessage, assistantMessage);

            // The first question makes a better title than "Study session".
            if (conversation.Messages.Count == 0 && conversation.Title == "Study session")
            {
                conversation.Title = question.Length <= 60 ? question : question[..57] + "...";
            }

            conversation.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return Ok(ToDto(assistantMessage));
        }

        // ---------- saving what the chat produced ----------

        /// <summary>Keeps a generated study guide in the library.</summary>
        [HttpPost("messages/{id:int}/save")]
        public async Task<ActionResult<StudyGuideDto>> SaveArtifact(int id)
        {
            var message = await _db.StudyMessages
                .Include(m => m.Conversation)
                .FirstOrDefaultAsync(m => m.Id == id && m.Conversation!.UserId == CurrentUserId);

            if (message is null)
            {
                return NotFound(new ApiErrorDto(
                    "MessageNotFound", "That message does not exist, or it does not belong to you."));
            }

            if (message.ArtifactKind != StudyArtifactKind.StudyGuide
                || string.IsNullOrWhiteSpace(message.ArtifactContent))
            {
                return BadRequest(new ApiErrorDto(
                    "NothingToSave", "That message does not contain a study guide."));
            }

            if (message.SavedStudyGuideId is int existing)
            {
                var already = await _db.StudyGuides.FirstAsync(g => g.Id == existing);
                return Ok(ToDto(already));
            }

            var guide = new StudyGuide
            {
                UserId = CurrentUserId,
                CourseId = message.Conversation!.CourseId,
                Title = message.ArtifactTitle ?? "Study guide",
                Content = message.ArtifactContent,
                IsAiGenerated = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.StudyGuides.Add(guide);
            await _db.SaveChangesAsync();

            message.SavedStudyGuideId = guide.Id;
            await _db.SaveChangesAsync();

            return Ok(ToDto(guide));
        }

        // ---------- the library ----------

        [HttpGet("guides")]
        public async Task<ActionResult<List<StudyGuideSummaryDto>>> GetGuides(
            [FromQuery] int? courseId)
        {
            var query = _db.StudyGuides.Where(g => g.UserId == CurrentUserId);
            if (courseId.HasValue) query = query.Where(g => g.CourseId == courseId.Value);

            var guides = await query
                .OrderByDescending(g => g.CreatedAt)
                .Select(g => new StudyGuideSummaryDto
                {
                    Id = g.Id,
                    CourseId = g.CourseId,
                    CourseName = g.Course != null ? g.Course.Name : null,
                    Title = g.Title,
                    CreatedAt = g.CreatedAt
                })
                .ToListAsync();

            return Ok(guides);
        }

        [HttpGet("guides/{id:int}")]
        public async Task<ActionResult<StudyGuideDto>> GetGuide(int id)
        {
            var guide = await _db.StudyGuides
                .Include(g => g.Course)
                .FirstOrDefaultAsync(g => g.Id == id && g.UserId == CurrentUserId);

            if (guide is null) return NotFound(GuideNotFound());

            return Ok(ToDto(guide));
        }

        [HttpDelete("guides/{id:int}")]
        public async Task<IActionResult> DeleteGuide(int id)
        {
            var guide = await _db.StudyGuides
                .FirstOrDefaultAsync(g => g.Id == id && g.UserId == CurrentUserId);

            if (guide is null) return NotFound(GuideNotFound());

            _db.StudyGuides.Remove(guide);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        // ---------- helpers ----------

        private Task<StudyConversation?> LoadAsync(int id) =>
            _db.StudyConversations
                .Include(c => c.Course)
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == CurrentUserId);

        /// <summary>
        /// Reads the chosen files and notes into one block of text.
        ///
        /// A file that cannot be read is skipped rather than failing the whole
        /// question - one scanned PDF among five sources should not stop the
        /// other four from being useful.
        /// </summary>
        private async Task<string> BuildSourceTextAsync(
            StudyConversation conversation, CancellationToken ct)
        {
            var sb = new System.Text.StringBuilder();

            var noteIds = Split(conversation.SourceNoteIds);
            if (noteIds.Count > 0)
            {
                var notes = await _db.Notes
                    .Where(n => noteIds.Contains(n.Id) && n.UserId == CurrentUserId)
                    .ToListAsync(ct);

                foreach (var note in notes)
                {
                    sb.AppendLine($"### Note: {note.Title}");
                    sb.AppendLine(note.Content);
                    sb.AppendLine();
                }
            }

            var materialIds = Split(conversation.SourceMaterialIds);
            if (materialIds.Count > 0)
            {
                var materials = await _db.StudyMaterials
                    .Where(m => materialIds.Contains(m.Id) && m.UserId == CurrentUserId)
                    .ToListAsync(ct);

                foreach (var material in materials)
                {
                    if (sb.Length >= StudyChatService.MaxSourceCharacters) break;
                    if (!_textExtraction.CanExtract(material.FileName, material.ContentType)) continue;

                    try
                    {
                        await using var stream = await _storage.OpenAsync(material.StoredPath, ct);

                        var extracted = await _textExtraction.ExtractAsync(
                            stream, material.FileName, material.ContentType,
                            maxCharacters: StudyChatService.MaxSourceCharacters, ct: ct);

                        if (extracted.IsEmpty) continue;

                        sb.AppendLine($"### File: {material.FileName}");
                        sb.AppendLine(extracted.Text);
                        sb.AppendLine();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Skipped material {Id} while building study chat context", material.Id);
                    }
                }
            }

            return sb.ToString();
        }

        private static string? Join(List<int> ids) =>
            ids.Count == 0 ? null : string.Join(",", ids);

        private static List<int> Split(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? new List<int>()
                : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                       .Select(int.Parse)
                       .ToList();

        private static ApiErrorDto ConversationNotFound() =>
            new("ConversationNotFound", "That conversation does not exist, or it does not belong to you.");

        private static ApiErrorDto GuideNotFound() =>
            new("GuideNotFound", "That study guide does not exist, or it does not belong to you.");

        private static StudyMessageDto ToDto(StudyMessage m) => new()
        {
            Id = m.Id,
            Role = m.Role,
            Content = m.Content,
            ArtifactKind = m.ArtifactKind,
            ArtifactTitle = m.ArtifactTitle,
            ArtifactContent = m.ArtifactContent,
            SavedStudyGuideId = m.SavedStudyGuideId,
            CreatedAt = m.CreatedAt
        };

        private static StudyGuideDto ToDto(StudyGuide g) => new()
        {
            Id = g.Id,
            CourseId = g.CourseId,
            CourseName = g.Course?.Name,
            Title = g.Title,
            Content = g.Content,
            CreatedAt = g.CreatedAt
        };

        private static ConversationDto ToDto(StudyConversation c) => new()
        {
            Id = c.Id,
            CourseId = c.CourseId,
            CourseName = c.Course?.Name,
            Title = c.Title,
            MessageCount = c.Messages.Count,
            UpdatedAt = c.UpdatedAt,
            SourceMaterialIds = Split(c.SourceMaterialIds),
            SourceNoteIds = Split(c.SourceNoteIds),
            Messages = c.Messages
                .OrderBy(m => m.CreatedAt)
                .Select(ToDto)
                .ToList()
        };
    }
}
