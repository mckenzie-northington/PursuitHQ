using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.Resumes;
using PursuitHQ.API.Models;
using PursuitHQ.API.Services;

namespace PursuitHQ.API.Controllers
{
    /// <summary>
    /// Resumes: built in the app or read in from a file, and checked.
    /// </summary>
    [Route("api/resumes")]
    public class ResumesController : ApiControllerBase
    {
        /// <summary>Resumes are short. A 10MB one is a mistake, not a resume.</summary>
        private const long MaxUploadBytes = 5 * 1024 * 1024;

        private static readonly string[] AllowedExtensions = { ".pdf", ".docx", ".txt", ".md" };

        private static readonly JsonSerializerOptions Json = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly ApplicationDbContext _db;
        private readonly ITextExtractionService _textExtraction;
        private readonly IResumeAiService _resumeAi;
        private readonly IAiUsageLimiter _limiter;
        private readonly ILogger<ResumesController> _logger;

        public ResumesController(
            ApplicationDbContext db,
            ITextExtractionService textExtraction,
            IResumeAiService resumeAi,
            IAiUsageLimiter limiter,
            ILogger<ResumesController> logger)
        {
            _db = db;
            _textExtraction = textExtraction;
            _resumeAi = resumeAi;
            _limiter = limiter;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<ResumeSummaryDto>>> GetResumes()
        {
            var resumes = await _db.Resumes
                .Where(r => r.UserId == CurrentUserId)
                .OrderByDescending(r => r.LastUpdated)
                .ToListAsync();

            return Ok(resumes.Select(ToSummary).ToList());
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ResumeDto>> GetResume(int id)
        {
            var resume = await FindAsync(id);
            if (resume is null) return NotFound(NotFoundError());

            return Ok(ToDto(resume));
        }

        [HttpPost]
        public async Task<ActionResult<ResumeDto>> Create(SaveResumeDto dto)
        {
            var resume = new Resume
            {
                UserId = CurrentUserId,
                Title = dto.Title.Trim(),
                Content = JsonSerializer.Serialize(dto.Content),
                CreatedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow
            };

            _db.Resumes.Add(resume);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetResume), new { id = resume.Id }, ToDto(resume));
        }

        /// <summary>
        /// Reads an uploaded resume into editable sections.
        ///
        /// What comes back is a first draft to correct, not a finished record -
        /// a resume laid out in two columns comes out of a PDF interleaved, and
        /// no amount of prompting fixes that reliably. The editor is where it
        /// gets put right.
        /// </summary>
        [HttpPost("import")]
        [RequestSizeLimit(MaxUploadBytes)]
        public async Task<ActionResult<ResumeDto>> Import(IFormFile file, CancellationToken ct)
        {
            if (!_resumeAi.IsConfigured)
            {
                return StatusCode(503, new ApiErrorDto(
                    "AiNotConfigured",
                    "AI is not set up on this server, so a file cannot be read in. You can still "
                    + "start a blank resume."));
            }

            if (file is null || file.Length == 0)
            {
                return BadRequest(new ApiErrorDto("NoFile", "No file was uploaded."));
            }

            if (file.Length > MaxUploadBytes)
            {
                return BadRequest(new ApiErrorDto(
                    "FileTooLarge", "That file is larger than 5 MB, which is not a resume."));
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!AllowedExtensions.Contains(extension))
            {
                return BadRequest(new ApiErrorDto(
                    "UnsupportedFile", "Upload a PDF, Word document, or plain text file."));
            }

            var usage = _limiter.Peek(CurrentUserId);
            if (usage.Exceeded)
            {
                return StatusCode(429, new ApiErrorDto(
                    "DailyLimitReached",
                    $"You have used all {usage.Limit} AI requests for today. You can still start "
                    + "a blank resume."));
            }

            TextExtractionResult extracted;
            try
            {
                await using var stream = file.OpenReadStream();

                extracted = await _textExtraction.ExtractAsync(
                    stream, file.FileName, file.ContentType ?? "application/octet-stream",
                    maxCharacters: ResumeAiService.MaxResumeCharacters, ct: ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read uploaded resume {Name}", file.FileName);

                return BadRequest(new ApiErrorDto(
                    "UnreadableFile", "That file could not be read. Try exporting it again as a PDF."));
            }

            if (extracted.IsEmpty)
            {
                return StatusCode(422, new ApiErrorDto(
                    "NoTextFound",
                    "No readable text was found. A resume saved as an image, or a scan, has no "
                    + "text layer to read."));
            }

            _limiter.Consume(CurrentUserId);

            ResumeContentDto content;
            try
            {
                content = await _resumeAi.ParseAsync(extracted.Text, ct);
            }
            catch (StudyToolException ex)
            {
                return StatusCode(502, new ApiErrorDto("ImportFailed", ex.Message));
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "AI provider call failed during resume import");
                return StatusCode(502, new ApiErrorDto("AiUnavailable", ex.Message));
            }

            var resume = new Resume
            {
                UserId = CurrentUserId,
                Title = Path.GetFileNameWithoutExtension(file.FileName),
                Content = JsonSerializer.Serialize(content),
                CreatedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow
            };

            _db.Resumes.Add(resume);
            await _db.SaveChangesAsync(ct);

            return CreatedAtAction(nameof(GetResume), new { id = resume.Id }, ToDto(resume));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ResumeDto>> Update(int id, SaveResumeDto dto)
        {
            var resume = await FindAsync(id);
            if (resume is null) return NotFound(NotFoundError());

            resume.Title = dto.Title.Trim();
            resume.Content = JsonSerializer.Serialize(dto.Content);
            resume.LastUpdated = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(ToDto(resume));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var resume = await FindAsync(id);
            if (resume is null) return NotFound(NotFoundError());

            _db.Resumes.Remove(resume);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// Checks a resume and returns findings. Never edits it.
        ///
        /// Two sources, merged: rules for the checkable facts, and the AI for
        /// whether the writing says anything. The rules run even when the AI is
        /// unavailable or the daily allowance is gone, so this endpoint is
        /// always worth calling.
        /// </summary>
        [HttpPost("{id:int}/review")]
        public async Task<ActionResult<ResumeReviewDto>> Review(int id, CancellationToken ct)
        {
            var resume = await FindAsync(id);
            if (resume is null) return NotFound(NotFoundError());

            var content = Deserialise(resume.Content);
            var ruleFindings = ResumeFormatChecker.Check(content);

            var review = new ResumeReviewDto
            {
                Findings = ruleFindings,
                ReviewedAt = DateTime.UtcNow
            };

            var usage = _limiter.Peek(CurrentUserId);

            if (!_resumeAi.IsConfigured || usage.Exceeded)
            {
                review.Summary = _resumeAi.IsConfigured
                    ? $"You have used all {usage.Limit} AI requests for today, so this is the "
                      + "format check only. The written review is back tomorrow."
                    : "AI is not set up on this server, so this is the format check only.";

                return Ok(review);
            }

            _limiter.Consume(CurrentUserId);

            try
            {
                var aiReview = await _resumeAi.ReviewAsync(content, ct);

                review.Summary = aiReview.Summary;
                review.Strengths = aiReview.Strengths;

                // Rule findings first: they are facts, and they are the ones
                // that can be fixed without thinking hard.
                review.Findings = ruleFindings.Concat(aiReview.Findings).ToList();
            }
            catch (Exception ex) when (ex is StudyToolException or HttpRequestException)
            {
                _logger.LogWarning(ex, "Resume AI review failed");

                review.Summary =
                    "The written review could not be completed just now, so this is the format "
                    + "check only. Try again in a minute.";
            }

            return Ok(review);
        }

        // ---------- helpers ----------

        private Task<Resume?> FindAsync(int id) =>
            _db.Resumes.FirstOrDefaultAsync(r => r.Id == id && r.UserId == CurrentUserId);

        private static ApiErrorDto NotFoundError() =>
            new("ResumeNotFound", "That resume does not exist, or it does not belong to you.");

        /// <summary>
        /// Reads stored JSON back into sections.
        ///
        /// An empty or unreadable record becomes an empty resume rather than an
        /// error: a resume that will not open is worse than one that opens
        /// blank, because at least a blank one can be retyped.
        /// </summary>
        private static ResumeContentDto Deserialise(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return new ResumeContentDto();

            try
            {
                return JsonSerializer.Deserialize<ResumeContentDto>(content, Json)
                       ?? new ResumeContentDto();
            }
            catch (JsonException)
            {
                return new ResumeContentDto();
            }
        }

        private static ResumeSummaryDto ToSummary(Resume r)
        {
            var content = Deserialise(r.Content);

            return new ResumeSummaryDto
            {
                Id = r.Id,
                Title = r.Title,
                CreatedAt = r.CreatedAt,
                LastUpdated = r.LastUpdated,
                OwnerName = content.Contact.Name,
                ExperienceCount = content.Experience.Count,
                ProjectCount = content.Projects.Count
            };
        }

        private static ResumeDto ToDto(Resume r)
        {
            var summary = ToSummary(r);

            return new ResumeDto
            {
                Id = summary.Id,
                Title = summary.Title,
                CreatedAt = summary.CreatedAt,
                LastUpdated = summary.LastUpdated,
                OwnerName = summary.OwnerName,
                ExperienceCount = summary.ExperienceCount,
                ProjectCount = summary.ProjectCount,
                Content = Deserialise(r.Content)
            };
        }
    }
}
