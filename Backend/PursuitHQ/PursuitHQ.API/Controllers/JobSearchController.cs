using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.Applications;
using PursuitHQ.API.DTOs.JobSearch;
using PursuitHQ.API.Models;
using PursuitHQ.API.Services;

namespace PursuitHQ.API.Controllers
{
    /// <summary>
    /// Searching real job postings, and saving one into the tracker.
    ///
    /// PursuitHQ never submits an application. Applying always sends the
    /// student to the employer's own posting.
    /// </summary>
    [Route("api/jobs")]
    public class JobSearchController : ApiControllerBase
    {
        private readonly IJobSearchService _search;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<JobSearchController> _logger;

        public JobSearchController(
            IJobSearchService search,
            ApplicationDbContext db,
            ILogger<JobSearchController> logger)
        {
            _search = search;
            _db = db;
            _logger = logger;
        }

        [HttpGet("search")]
        public async Task<ActionResult<JobSearchResponseDto>> Search(
            [FromQuery] string query,
            [FromQuery] string? location,
            [FromQuery] string? contractTime,
            [FromQuery] int page = 1,
            CancellationToken ct = default)
        {
            if (!_search.IsConfigured)
            {
                return StatusCode(503, new ApiErrorDto(
                    "JobSearchNotConfigured",
                    "Job search is not set up yet. Add your Adzuna app id and key to user-secrets."));
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new ApiErrorDto("MissingQuery", "Enter something to search for."));
            }

            try
            {
                var results = await _search.SearchAsync(query, location, contractTime, page, ct);

                // Mark postings the student has already tracked, so the button
                // can say "Saved" instead of offering a duplicate.
                var ids = results.Results.Select(r => r.ExternalId).ToList();

                // Pull the student's saved external ids and compare in memory.
                // Translating Contains() over a nullable string column is
                // fragile across EF providers, and a student's saved list is
                // small enough that filtering here costs nothing.
                var savedIds = await _db.JobApplications
                    .Where(a => a.UserId == CurrentUserId && a.ExternalJobId != null)
                    .Select(a => a.ExternalJobId!)
                    .ToListAsync(ct);

                var saved = savedIds.Where(x => ids.Contains(x)).ToHashSet();

                foreach (var r in results.Results)
                {
                    r.AlreadySaved = saved.Contains(r.ExternalId);
                }

                return Ok(results);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(502, new ApiErrorDto(
                    "JobBoardUnavailable",
                    "The job board could not be reached. Check that your Adzuna app id and key are correct.",
                    new Dictionary<string, string[]> { ["detail"] = new[] { ex.Message } }));
            }
            catch (TaskCanceledException)
            {
                return StatusCode(504, new ApiErrorDto(
                    "JobBoardTimeout", "The job board took too long to respond. Try again."));
            }
            catch (Exception ex)
            {
                // Better a readable message than a bare 500 the user cannot act on.
                _logger.LogError(ex, "Job search failed for query {Query}", query);

                return StatusCode(500, new ApiErrorDto(
                    "JobSearchFailed",
                    "Something went wrong running that search.",
                    new Dictionary<string, string[]> { ["detail"] = new[] { ex.Message } }));
            }
        }

        /// <summary>Adds a search result to the tracker with status Saved.</summary>
        [HttpPost("save")]
        public async Task<ActionResult<JobApplicationDto>> SaveToTracker(SaveJobFromSearchDto dto)
        {
            var duplicate = await _db.JobApplications.AnyAsync(a =>
                a.UserId == CurrentUserId && a.ExternalJobId == dto.ExternalId);

            if (duplicate)
            {
                return Conflict(new ApiErrorDto(
                    "AlreadySaved", "That posting is already in your tracker."));
            }

            var application = new JobApplication
            {
                UserId = CurrentUserId,
                Company = dto.Company,
                Role = dto.Role,
                Type = dto.Type,
                Status = ApplicationStatus.Saved,
                Source = ApplicationSource.Search,
                ExternalJobId = dto.ExternalId,
                SourceUrl = dto.ApplyUrl,
                CreatedAt = DateTime.UtcNow
            };

            _db.JobApplications.Add(application);
            await _db.SaveChangesAsync();

            return Ok(new JobApplicationDto
            {
                Id = application.Id,
                Company = application.Company,
                Role = application.Role,
                Type = application.Type,
                Status = application.Status,
                Source = application.Source,
                SourceUrl = application.SourceUrl,
                CreatedAt = application.CreatedAt
            });
        }
    }
}
