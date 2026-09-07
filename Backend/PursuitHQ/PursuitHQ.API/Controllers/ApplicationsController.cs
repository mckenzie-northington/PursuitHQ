using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.Applications;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.Controllers
{
    /// <summary>Internship and job applications the student is tracking.</summary>
    [Route("api/[controller]")]
    public class ApplicationsController : ApiControllerBase
    {
        private readonly ApplicationDbContext _db;

        public ApplicationsController(ApplicationDbContext db) => _db = db;

        [HttpGet]
        public async Task<ActionResult<List<JobApplicationDto>>> GetApplications(
            [FromQuery] ApplicationStatus? status,
            [FromQuery] JobType? type,
            [FromQuery] string? search)
        {
            var query = _db.JobApplications.Where(a => a.UserId == CurrentUserId);

            if (status.HasValue) query = query.Where(a => a.Status == status.Value);
            if (type.HasValue) query = query.Where(a => a.Type == type.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.ToLower();
                query = query.Where(a =>
                    a.Company.ToLower().Contains(term) || a.Role.ToLower().Contains(term));
            }

            var applications = await query
                // Newest activity first: applied ones by date, saved ones by when added.
                .OrderByDescending(a => a.AppliedDate ?? a.CreatedAt)
                .ToListAsync();

            return Ok(applications.Select(ToDto).ToList());
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<JobApplicationDto>> GetApplication(int id)
        {
            var application = await _db.JobApplications
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == CurrentUserId);

            if (application is null) return NotFound(NotFoundError());

            return Ok(ToDto(application));
        }

        /// <summary>Pipeline counts, for the board view and the dashboard.</summary>
        [HttpGet("stats")]
        public async Task<ActionResult<ApplicationStatsDto>> GetStats()
        {
            var byStatus = await _db.JobApplications
                .Where(a => a.UserId == CurrentUserId)
                .GroupBy(a => a.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            int CountOf(ApplicationStatus s) =>
                byStatus.FirstOrDefault(x => x.Status == s)?.Count ?? 0;

            var applied = CountOf(ApplicationStatus.Applied);
            var interview = CountOf(ApplicationStatus.Interview);
            var offer = CountOf(ApplicationStatus.Offer);
            var rejected = CountOf(ApplicationStatus.Rejected);
            var saved = CountOf(ApplicationStatus.Saved);

            // Anything past Saved was actually submitted, including ones that
            // have since moved on to interview, offer, or rejection.
            var submitted = applied + interview + offer + rejected;

            return Ok(new ApplicationStatsDto
            {
                Saved = saved,
                Applied = applied,
                Interview = interview,
                Offer = offer,
                Rejected = rejected,
                Total = saved + submitted,
                InterviewRate = submitted == 0
                    ? 0
                    : Math.Round((interview + offer) * 100.0 / submitted, 1)
            });
        }

        [HttpPost]
        public async Task<ActionResult<JobApplicationDto>> CreateApplication(SaveJobApplicationDto dto)
        {
            var application = new JobApplication
            {
                UserId = CurrentUserId,
                Company = dto.Company,
                Role = dto.Role,
                Type = dto.Type,
                Status = dto.Status,
                // Moving straight to Applied without a date is common, so fill
                // today in rather than leaving a confusing blank.
                AppliedDate = dto.AppliedDate
                    ?? (dto.Status != ApplicationStatus.Saved ? DateTime.UtcNow : null),
                Notes = dto.Notes,
                SourceUrl = dto.SourceUrl,
                Source = ApplicationSource.Manual,
                CreatedAt = DateTime.UtcNow
            };

            _db.JobApplications.Add(application);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetApplication), new { id = application.Id }, ToDto(application));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<JobApplicationDto>> UpdateApplication(int id, SaveJobApplicationDto dto)
        {
            var application = await _db.JobApplications
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == CurrentUserId);

            if (application is null) return NotFound(NotFoundError());

            var wasSaved = application.Status == ApplicationStatus.Saved;

            application.Company = dto.Company;
            application.Role = dto.Role;
            application.Type = dto.Type;
            application.Status = dto.Status;
            application.Notes = dto.Notes;
            application.SourceUrl = dto.SourceUrl;

            if (dto.AppliedDate.HasValue)
            {
                application.AppliedDate = dto.AppliedDate;
            }
            else if (wasSaved && dto.Status != ApplicationStatus.Saved && application.AppliedDate is null)
            {
                // Moving out of Saved for the first time means it was just sent.
                application.AppliedDate = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            return Ok(ToDto(application));
        }

        /// <summary>
        /// Status-only update, for dragging a card between board columns.
        /// </summary>
        [HttpPut("{id:int}/status")]
        public async Task<ActionResult<JobApplicationDto>> UpdateStatus(
            int id, [FromBody] ApplicationStatus status)
        {
            var application = await _db.JobApplications
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == CurrentUserId);

            if (application is null) return NotFound(NotFoundError());

            if (application.Status == ApplicationStatus.Saved
                && status != ApplicationStatus.Saved
                && application.AppliedDate is null)
            {
                application.AppliedDate = DateTime.UtcNow;
            }

            application.Status = status;
            await _db.SaveChangesAsync();

            return Ok(ToDto(application));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteApplication(int id)
        {
            var application = await _db.JobApplications
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == CurrentUserId);

            if (application is null) return NotFound(NotFoundError());

            _db.JobApplications.Remove(application);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        // ---------- helpers ----------

        private static ApiErrorDto NotFoundError() =>
            new("ApplicationNotFound", "That application does not exist, or it does not belong to you.");

        private static JobApplicationDto ToDto(JobApplication a) => new()
        {
            Id = a.Id,
            Company = a.Company,
            Role = a.Role,
            Type = a.Type,
            Status = a.Status,
            AppliedDate = a.AppliedDate,
            Notes = a.Notes,
            Source = a.Source,
            SourceUrl = a.SourceUrl,
            CreatedAt = a.CreatedAt,
            DaysSinceApplied = a.AppliedDate.HasValue
                ? (int)(DateTime.UtcNow - a.AppliedDate.Value).TotalDays
                : null
        };
    }
}
