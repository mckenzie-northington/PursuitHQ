using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs.Dashboard;
using PursuitHQ.API.Models;
using PursuitHQ.API.Services;

namespace PursuitHQ.API.Controllers
{
    /// <summary>The home page, in one request.</summary>
    [Route("api/dashboard")]
    public class DashboardController : ApiControllerBase
    {
        /// <summary>How far ahead "due soon" looks.</summary>
        private const int LookaheadDays = 7;

        /// <summary>Enough to pick one back up; more would be a list, not a prompt.</summary>
        private const int RecentLimit = 3;

        private readonly ApplicationDbContext _db;
        private readonly ICalendarFeedService _feed;

        public DashboardController(ApplicationDbContext db, ICalendarFeedService feed)
        {
            _db = db;
            _feed = feed;
        }

        [HttpGet]
        public async Task<ActionResult<DashboardDto>> Get(CancellationToken ct)
        {
            // Wall-clock, to match how due dates are stored. See the note in
            // DatabaseDesign.md section 4a.
            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);
            var horizon = now.Date.AddDays(LookaheadDays + 1);

            var user = await _db.Users
                .Where(u => u.Id == CurrentUserId)
                .Select(u => new { u.FirstName })
                .FirstOrDefaultAsync(ct);

            var schedule = await _feed.GetAsync(CurrentUserId, today, today, ct);

            // Assignments due in the window, plus anything already late -
            // something overdue is the most important thing on this page and
            // must not fall off the bottom of a date range.
            var dueSoon = await _db.Assignments
                .Where(a => a.Course!.UserId == CurrentUserId
                            && a.Status != AssignmentStatus.Completed
                            && a.DueDate < horizon)
                .OrderBy(a => a.DueDate)
                .Select(a => new DashboardAssignmentDto
                {
                    Id = a.Id,
                    CourseId = a.CourseId,
                    CourseName = a.Course!.Name,
                    ColorHex = a.Course.ColorHex,
                    Title = a.Title,
                    DueDate = a.DueDate,
                    Status = a.Status,
                    IsOverdue = a.DueDate < now
                })
                .ToListAsync(ct);

            var courses = await _db.Courses
                .Where(c => c.UserId == CurrentUserId)
                .OrderBy(c => c.Name)
                .Select(c => new DashboardCourseDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    ColorHex = c.ColorHex,
                    Professor = c.Professor,
                    OpenAssignments = c.Assignments.Count(a => a.Status != AssignmentStatus.Completed)
                })
                .ToListAsync(ct);

            var decks = await _db.FlashcardDecks
                .Where(d => d.UserId == CurrentUserId)
                .OrderByDescending(d => d.CreatedAt)
                .Take(RecentLimit)
                .Select(d => new
                {
                    d.Id,
                    d.Title,
                    CourseName = d.Course != null ? d.Course.Name : null,
                    CardCount = d.Flashcards.Count,
                    Reviewed = d.Flashcards.Sum(c => c.TimesReviewed),
                    Correct = d.Flashcards.Sum(c => c.TimesCorrect)
                })
                .ToListAsync(ct);

            var tests = await _db.Quizzes
                .Where(q => q.UserId == CurrentUserId)
                .OrderByDescending(q => q.CreatedAt)
                .Take(RecentLimit)
                .Select(q => new DashboardTestDto
                {
                    Id = q.Id,
                    Title = q.Title,
                    CourseName = q.Course != null ? q.Course.Name : null,
                    QuestionCount = q.Questions.Count,
                    AttemptCount = q.Attempts.Count(a => a.CompletedAt != null),
                    BestScore = q.Attempts.Where(a => a.Score != null).Max(a => a.Score)
                })
                .ToListAsync(ct);

            var deckCount = await _db.FlashcardDecks.CountAsync(d => d.UserId == CurrentUserId, ct);
            var quizCount = await _db.Quizzes.CountAsync(q => q.UserId == CurrentUserId, ct);
            var guideCount = await _db.StudyGuides.CountAsync(g => g.UserId == CurrentUserId, ct);

            return Ok(new DashboardDto
            {
                FirstName = user?.FirstName ?? "there",
                Today = today,
                TodaysSchedule = schedule.Items,
                DueSoon = dueSoon,
                Courses = courses,
                RecentDecks = decks.Select(d => new DashboardDeckDto
                {
                    Id = d.Id,
                    Title = d.Title,
                    CourseName = d.CourseName,
                    CardCount = d.CardCount,
                    // Null, not zero, when nothing has been reviewed - "never
                    // studied" and "studied and got everything wrong" are very
                    // different things to show someone.
                    Accuracy = d.Reviewed == 0
                        ? null
                        : (int)Math.Round(d.Correct * 100.0 / d.Reviewed)
                }).ToList(),
                RecentTests = tests,
                Counts = new DashboardCountsDto
                {
                    Courses = courses.Count,
                    OpenAssignments = courses.Sum(c => c.OpenAssignments),
                    Overdue = dueSoon.Count(a => a.IsOverdue),
                    StudyTools = deckCount + quizCount + guideCount
                }
            });
        }
    }
}
