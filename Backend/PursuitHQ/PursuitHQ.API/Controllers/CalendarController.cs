using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.Calendar;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.Controllers
{
    /// <summary>
    /// The unified calendar feed, plus CRUD for the student's own events.
    ///
    /// Four sources feed one calendar: recurring class meetings, assignment due
    /// dates, planned study sessions, and everything else the student adds.
    /// Merging happens here rather than in the browser, so the frontend renders
    /// one sorted list instead of reconciling four shapes.
    /// </summary>
    [Route("api/calendar")]
    public class CalendarController : ApiControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<CalendarController> _logger;

        public CalendarController(ApplicationDbContext db, ILogger<CalendarController> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>Everything on the calendar between two dates, inclusive.</summary>
        [HttpGet]
        public async Task<ActionResult<List<CalendarItemDto>>> GetCalendar(
            [FromQuery] DateOnly from, [FromQuery] DateOnly to)
        {
            if (to < from)
            {
                return BadRequest(new ApiErrorDto(
                    "InvalidRange", "The end date must be on or after the start date."));
            }

            // A wide range would expand into a huge number of class occurrences,
            // and no view needs more than a few months at once.
            if (to.DayNumber - from.DayNumber > 400)
            {
                return BadRequest(new ApiErrorDto(
                    "RangeTooLarge", "Please request a range of about a year or less."));
            }

            var items = new List<CalendarItemDto>();

            // Each source is tried separately so one failing query does not
            // take down the whole calendar, and the log names which one broke.
            var failures = new List<string>();

            foreach (var (name, load) in new (string, Func<Task<List<CalendarItemDto>>>)[]
            {
                ("classes",        () => GetClassMeetingsAsync(from, to)),
                ("assignments",    () => GetAssignmentsAsync(from, to)),
                ("study sessions", () => GetStudySessionsAsync(from, to)),
                ("events",         () => GetEventsAsync(from, to)),
            })
            {
                try
                {
                    items.AddRange(await load());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Calendar source {Source} failed", name);
                    failures.Add($"{name}: {ex.Message}");
                }
            }

            if (failures.Count > 0 && items.Count == 0)
            {
                return StatusCode(500, new ApiErrorDto(
                    "CalendarFailed",
                    "Could not load the calendar.",
                    new Dictionary<string, string[]> { ["sources"] = failures.ToArray() }));
            }

            // All-day items sort before timed ones on the same day, so a day
            // reads top to bottom sensibly. They carry a start time now (an
            // assignment's deadline), so sort on the flag rather than on
            // whether a time happens to be present.
            return Ok(items
                .OrderBy(i => i.Date)
                .ThenBy(i => i.IsAllDay ? 0 : 1)
                .ThenBy(i => i.StartTime ?? TimeOnly.MinValue)
                .ToList());
        }

        /// <summary>
        /// Expands recurring weekly class times into one entry per occurrence
        /// in the requested range.
        /// </summary>
        private async Task<List<CalendarItemDto>> GetClassMeetingsAsync(DateOnly from, DateOnly to)
        {
            var schedules = await _db.ClassSchedules
                .Where(s => s.Course!.UserId == CurrentUserId)
                .Select(s => new
                {
                    s.Id,
                    s.CourseId,
                    s.DayOfWeek,
                    s.StartTime,
                    s.EndTime,
                    s.Location,
                    CourseName = s.Course!.Name,
                    s.Course.Professor,
                    s.Course.ColorHex,
                    s.Course.StartDate,
                    s.Course.EndDate
                })
                .ToListAsync();

            var items = new List<CalendarItemDto>();

            foreach (var schedule in schedules)
            {
                // A class time repeats weekly forever unless the course says
                // when the term starts and ends, so the expansion is clipped
                // to whichever is tighter: the range being viewed, or the
                // course's own dates.
                var first = schedule.StartDate is DateOnly courseStart && courseStart > from
                    ? courseStart
                    : from;

                var last = schedule.EndDate is DateOnly courseEnd && courseEnd < to
                    ? courseEnd
                    : to;

                for (var day = first; day <= last; day = day.AddDays(1))
                {
                    if (day.DayOfWeek != schedule.DayOfWeek) continue;

                    items.Add(new CalendarItemDto
                    {
                        Id = $"class-{schedule.Id}-{day:yyyyMMdd}",
                        Type = CalendarItemType.Class,
                        Title = schedule.CourseName,
                        Subtitle = schedule.Professor,
                        Date = day,
                        StartTime = schedule.StartTime,
                        EndTime = schedule.EndTime,
                        Location = schedule.Location,
                        ColorHex = schedule.ColorHex,
                        CourseId = schedule.CourseId,
                        SourceId = schedule.Id
                    });
                }
            }

            return items;
        }

        private async Task<List<CalendarItemDto>> GetAssignmentsAsync(DateOnly from, DateOnly to)
        {
            var start = from.ToDateTime(TimeOnly.MinValue);
            var end = to.ToDateTime(TimeOnly.MaxValue);
            // Wall-clock, to match how due dates are stored. See the note in
            // AssignmentsController.
            var now = DateTime.Now;

            var assignments = await _db.Assignments
                .Where(a => a.Course!.UserId == CurrentUserId
                    && a.DueDate >= start && a.DueDate <= end)
                .Select(a => new
                {
                    a.Id,
                    a.CourseId,
                    a.Title,
                    a.DueDate,
                    a.Status,
                    CourseName = a.Course!.Name,
                    a.Course.ColorHex
                })
                .ToListAsync();

            return assignments.Select(a => new CalendarItemDto
            {
                Id = $"assignment-{a.Id}",
                Type = CalendarItemType.Assignment,
                Title = a.Title,
                Subtitle = a.CourseName,
                Date = DateOnly.FromDateTime(a.DueDate),
                // An assignment is not an appointment: it belongs in the
                // all-day strip, not slotted into the 11pm row of the hour
                // grid. The deadline time still rides along, because "due
                // 11:59 PM" is worth reading.
                IsAllDay = true,
                StartTime = TimeOnly.FromDateTime(a.DueDate),
                EndTime = null,
                ColorHex = a.ColorHex,
                CourseId = a.CourseId,
                SourceId = a.Id,
                Status = a.Status.ToString(),
                IsOverdue = a.DueDate < now && a.Status != AssignmentStatus.Completed
            }).ToList();
        }

        private async Task<List<CalendarItemDto>> GetStudySessionsAsync(DateOnly from, DateOnly to)
        {
            var sessions = await _db.StudySessions
                .Where(s => s.UserId == CurrentUserId
                    && s.ScheduledDate >= from && s.ScheduledDate <= to)
                .Select(s => new
                {
                    s.Id,
                    s.CourseId,
                    s.Title,
                    s.ScheduledDate,
                    s.StartTime,
                    s.EndTime,
                    s.Status,
                    CourseName = s.Course != null ? s.Course.Name : null,
                    ColorHex = s.Course != null ? s.Course.ColorHex : null
                })
                .ToListAsync();

            return sessions.Select(s => new CalendarItemDto
            {
                Id = $"study-{s.Id}",
                Type = CalendarItemType.StudySession,
                Title = s.Title,
                Subtitle = s.CourseName ?? "Study session",
                Date = s.ScheduledDate,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                ColorHex = s.ColorHex,
                CourseId = s.CourseId,
                SourceId = s.Id,
                Status = s.Status.ToString()
            }).ToList();
        }

        private async Task<List<CalendarItemDto>> GetEventsAsync(DateOnly from, DateOnly to)
        {
            var start = from.ToDateTime(TimeOnly.MinValue);
            var end = to.ToDateTime(TimeOnly.MaxValue);

            var events = await _db.CalendarEvents
                .Where(e => e.UserId == CurrentUserId
                    && e.StartDateTime <= end && e.EndDateTime >= start)
                .ToListAsync();

            var items = new List<CalendarItemDto>();

            foreach (var e in events)
            {
                if (e.IsRecurring && !string.IsNullOrWhiteSpace(e.RecurrenceRule))
                {
                    items.AddRange(ExpandRecurring(e, from, to));
                    continue;
                }

                // An event can span days - a weekend trip, a conference, an
                // all-day event covering a whole week. It gets one entry per
                // day it touches, clipped to the requested range, so it shows
                // up on every day it actually covers instead of only the first.
                var first = DateOnly.FromDateTime(e.StartDateTime);
                var last = DateOnly.FromDateTime(e.EndDateTime);

                var spanStart = first < from ? from : first;
                var spanEnd = last > to ? to : last;

                for (var day = spanStart; day <= spanEnd; day = day.AddDays(1))
                {
                    items.Add(ToItem(e, day, isFirstDay: day == first, isLastDay: day == last));
                }
            }

            return items;
        }

        /// <summary>
        /// Expands a weekly recurring event across the range.
        ///
        /// Only FREQ=WEEKLY with BYDAY is understood - that covers work shifts
        /// and club meetings, which is what students actually create. Anything
        /// more exotic would mean pulling in a full iCal library.
        /// </summary>
        private static IEnumerable<CalendarItemDto> ExpandRecurring(
            CalendarEvent e, DateOnly from, DateOnly to)
        {
            var rule = e.RecurrenceRule!.ToUpperInvariant();
            if (!rule.Contains("FREQ=WEEKLY")) yield break;

            var days = ParseByDay(rule);
            if (days.Count == 0)
            {
                days.Add(e.StartDateTime.DayOfWeek);
            }

            var firstDate = DateOnly.FromDateTime(e.StartDateTime);

            for (var day = from; day <= to; day = day.AddDays(1))
            {
                // Never before the event actually started.
                if (day < firstDate) continue;
                if (!days.Contains(day.DayOfWeek)) continue;

                // A recurring event is one occurrence per matching day, so
                // every occurrence is both its own first and last day.
                yield return ToItem(e, day, isFirstDay: true, isLastDay: true);
            }
        }

        private static List<DayOfWeek> ParseByDay(string rule)
        {
            var days = new List<DayOfWeek>();

            var index = rule.IndexOf("BYDAY=", StringComparison.Ordinal);
            if (index < 0) return days;

            var value = rule[(index + 6)..];
            var semicolon = value.IndexOf(';');
            if (semicolon >= 0) value = value[..semicolon];

            foreach (var code in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var day = code.Trim() switch
                {
                    "SU" => DayOfWeek.Sunday,
                    "MO" => DayOfWeek.Monday,
                    "TU" => DayOfWeek.Tuesday,
                    "WE" => DayOfWeek.Wednesday,
                    "TH" => DayOfWeek.Thursday,
                    "FR" => DayOfWeek.Friday,
                    "SA" => DayOfWeek.Saturday,
                    _ => (DayOfWeek?)null
                };

                if (day.HasValue) days.Add(day.Value);
            }

            return days;
        }

        /// <summary>
        /// One day's slice of an event.
        ///
        /// The times shown depend on where in the span this day falls: a trip
        /// that runs Friday 6pm to Sunday noon starts at 6pm on Friday, fills
        /// Saturday, and ends at noon on Sunday. Carrying the original 6pm to
        /// every day would draw the block in the wrong place twice.
        /// </summary>
        private static CalendarItemDto ToItem(
            CalendarEvent e, DateOnly date, bool isFirstDay, bool isLastDay) => new()
        {
            Id = $"event-{e.Id}-{date:yyyyMMdd}",
            Type = CalendarItemType.Event,
            Title = e.Title,
            // EventType is still stored, but it is not something the student
            // picks any more, so showing "Other" under every event was noise.
            Subtitle = null,
            Date = date,
            IsAllDay = e.IsAllDay,
            StartTime = e.IsAllDay
                ? null
                : isFirstDay ? TimeOnly.FromDateTime(e.StartDateTime) : TimeOnly.MinValue,
            EndTime = e.IsAllDay
                ? null
                : isLastDay ? TimeOnly.FromDateTime(e.EndDateTime) : new TimeOnly(23, 59),
            Location = e.Location,
            ColorHex = e.ColorHex,
            SourceId = e.Id
        };
    }
}
