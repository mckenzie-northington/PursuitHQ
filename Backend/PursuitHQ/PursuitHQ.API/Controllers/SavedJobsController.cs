using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.Jobs;
using PursuitHQ.API.DTOs.Resumes;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.Controllers
{
    /// <summary>
    /// Job postings the student kept, with the score their resume got.
    /// </summary>
    [Route("api/saved-jobs")]
    public class SavedJobsController : ApiControllerBase
    {
        /// <summary>Matching caps the posting at 15k; this leaves room and no more.</summary>
        private const int MaxPostingCharacters = 20_000;

        private static readonly JsonSerializerOptions Json = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly ApplicationDbContext _db;
        private readonly ILogger<SavedJobsController> _logger;

        public SavedJobsController(ApplicationDbContext db, ILogger<SavedJobsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<SavedJobSummaryDto>>> GetSavedJobs()
        {
            var jobs = await _db.SavedJobs
                .Where(j => j.UserId == CurrentUserId)
                .OrderByDescending(j => j.SavedAt)
                .Select(j => new SavedJobSummaryDto
                {
                    Id = j.Id,
                    Title = j.Title,
                    Company = j.Company,
                    Url = j.Url,
                    Score = j.Score,
                    ResumeTitle = j.Resume != null ? j.Resume.Title : null,
                    SavedAt = j.SavedAt
                })
                .ToListAsync();

            return Ok(jobs);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<SavedJobDto>> GetSavedJob(int id)
        {
            var job = await _db.SavedJobs
                .Include(j => j.Resume)
                .FirstOrDefaultAsync(j => j.Id == id && j.UserId == CurrentUserId);

            if (job is null) return NotFound(NotFoundError());

            return Ok(new SavedJobDto
            {
                Id = job.Id,
                Title = job.Title,
                Company = job.Company,
                Url = job.Url,
                Score = job.Score,
                ResumeTitle = job.Resume?.Title,
                SavedAt = job.SavedAt,
                PostingText = job.PostingText,
                Notes = job.Notes,
                Match = ReadMatch(job.MatchJson)
            });
        }

        [HttpPost]
        public async Task<ActionResult<SavedJobDto>> SaveJob(SaveJobDto dto)
        {
            // Only a resume that is actually theirs, and null rather than a
            // rejection if not - a saved job is worth keeping even if the link
            // back to the resume is wrong.
            int? resumeId = null;

            if (dto.ResumeId is int candidate)
            {
                var owned = await _db.Resumes
                    .AnyAsync(r => r.Id == candidate && r.UserId == CurrentUserId);

                if (owned) resumeId = candidate;
            }

            var posting = dto.PostingText.Length > MaxPostingCharacters
                ? dto.PostingText[..MaxPostingCharacters]
                : dto.PostingText;

            var job = new SavedJob
            {
                UserId = CurrentUserId,
                Title = dto.Title.Trim(),
                Company = string.IsNullOrWhiteSpace(dto.Company) ? null : dto.Company.Trim(),
                Url = string.IsNullOrWhiteSpace(dto.Url) ? null : dto.Url.Trim(),
                PostingText = posting,
                Score = Math.Clamp(dto.Score, 0, 100),
                MatchJson = dto.Match is null ? "" : JsonSerializer.Serialize(dto.Match),
                ResumeId = resumeId,
                Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                SavedAt = DateTime.UtcNow
            };

            _db.SavedJobs.Add(job);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSavedJob), new { id = job.Id }, new SavedJobDto
            {
                Id = job.Id,
                Title = job.Title,
                Company = job.Company,
                Url = job.Url,
                Score = job.Score,
                SavedAt = job.SavedAt,
                PostingText = job.PostingText,
                Notes = job.Notes,
                Match = dto.Match
            });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteSavedJob(int id)
        {
            var job = await _db.SavedJobs
                .FirstOrDefaultAsync(j => j.Id == id && j.UserId == CurrentUserId);

            if (job is null) return NotFound(NotFoundError());

            _db.SavedJobs.Remove(job);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        // ---------- helpers ----------

        private static ApiErrorDto NotFoundError() =>
            new("SavedJobNotFound", "That saved job does not exist, or it does not belong to you.");

        private JobMatchDto? ReadMatch(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                return JsonSerializer.Deserialize<JobMatchDto>(json, Json);
            }
            catch (JsonException ex)
            {
                // Not an error worth failing on. The title, the link and the
                // score are all still readable; only the breakdown is lost.
                _logger.LogWarning(ex, "Stored job match JSON could not be read");
                return null;
            }
        }
    }
}
