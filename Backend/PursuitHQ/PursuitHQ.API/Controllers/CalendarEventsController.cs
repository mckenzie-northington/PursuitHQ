using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.Calendar;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.Controllers
{
    /// <summary>
    /// The student's own calendar entries: work shifts, club meetings,
    /// appointments - anything that is not a class, assignment, or study session.
    /// </summary>
    [Route("api/calendar-events")]
    public class CalendarEventsController : ApiControllerBase
    {
        private readonly ApplicationDbContext _db;

        public CalendarEventsController(ApplicationDbContext db) => _db = db;

        [HttpGet]
        public async Task<ActionResult<List<CalendarEventDto>>> GetEvents(
            [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var query = _db.CalendarEvents.Where(e => e.UserId == CurrentUserId);

            if (from.HasValue) query = query.Where(e => e.EndDateTime >= from.Value);
            if (to.HasValue) query = query.Where(e => e.StartDateTime <= to.Value);

            var events = await query
                .OrderBy(e => e.StartDateTime)
                .Select(e => ToDto(e))
                .ToListAsync();

            return Ok(events);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<CalendarEventDto>> GetEvent(int id)
        {
            var found = await _db.CalendarEvents
                .FirstOrDefaultAsync(e => e.Id == id && e.UserId == CurrentUserId);

            if (found is null) return NotFound(NotFoundError());

            return Ok(ToDto(found));
        }

        [HttpPost]
        public async Task<ActionResult<CalendarEventDto>> CreateEvent(SaveCalendarEventDto dto)
        {
            var (start, end, rangeError) = NormaliseRange(dto);
            if (rangeError is not null) return BadRequest(rangeError);

            var calendarEvent = new CalendarEvent
            {
                UserId = CurrentUserId,
                Title = dto.Title,
                Description = dto.Description,
                IsAllDay = dto.IsAllDay,
                StartDateTime = start,
                EndDateTime = end,
                Location = dto.Location,
                EventType = dto.EventType,
                IsRecurring = dto.IsRecurring,
                RecurrenceRule = dto.IsRecurring ? dto.RecurrenceRule : null,
                ColorHex = dto.ColorHex,
                CreatedAt = DateTime.UtcNow
            };

            _db.CalendarEvents.Add(calendarEvent);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetEvent), new { id = calendarEvent.Id }, ToDto(calendarEvent));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<CalendarEventDto>> UpdateEvent(int id, SaveCalendarEventDto dto)
        {
            var calendarEvent = await _db.CalendarEvents
                .FirstOrDefaultAsync(e => e.Id == id && e.UserId == CurrentUserId);

            if (calendarEvent is null) return NotFound(NotFoundError());

            var (start, end, rangeError) = NormaliseRange(dto);
            if (rangeError is not null) return BadRequest(rangeError);

            calendarEvent.Title = dto.Title;
            calendarEvent.Description = dto.Description;
            calendarEvent.IsAllDay = dto.IsAllDay;
            calendarEvent.StartDateTime = start;
            calendarEvent.EndDateTime = end;
            calendarEvent.Location = dto.Location;
            calendarEvent.EventType = dto.EventType;
            calendarEvent.IsRecurring = dto.IsRecurring;
            calendarEvent.RecurrenceRule = dto.IsRecurring ? dto.RecurrenceRule : null;
            calendarEvent.ColorHex = dto.ColorHex;

            await _db.SaveChangesAsync();

            return Ok(ToDto(calendarEvent));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var calendarEvent = await _db.CalendarEvents
                .FirstOrDefaultAsync(e => e.Id == id && e.UserId == CurrentUserId);

            if (calendarEvent is null) return NotFound(NotFoundError());

            _db.CalendarEvents.Remove(calendarEvent);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        // ---------- helpers ----------

        private static ApiErrorDto NotFoundError() =>
            new("EventNotFound", "That event does not exist, or it does not belong to you.");

        /// <summary>
        /// Works out the start and end to actually store, or an error to send back.
        ///
        /// An all-day event ignores whatever times came in and snaps to whole
        /// days: midnight on the start date through 23:59:59 on the end date.
        /// Storing real endpoints rather than a null time means the range
        /// queries in CalendarController need no special case for all-day, and
        /// a single-day all-day event is a valid range instead of a zero-length
        /// one the validation below would reject.
        /// </summary>
        private static (DateTime Start, DateTime End, ApiErrorDto? Error) NormaliseRange(
            SaveCalendarEventDto dto)
        {
            if (dto.IsAllDay)
            {
                var firstDay = dto.StartDateTime.Date;
                var lastDay = dto.EndDateTime.Date;

                if (lastDay < firstDay)
                {
                    return (default, default, new ApiErrorDto(
                        "InvalidTimeRange", "The last day must be on or after the first day."));
                }

                return (firstDay, lastDay.AddDays(1).AddSeconds(-1), null);
            }

            if (dto.EndDateTime <= dto.StartDateTime)
            {
                return (default, default, new ApiErrorDto(
                    "InvalidTimeRange", "The end time must be after the start time."));
            }

            return (dto.StartDateTime, dto.EndDateTime, null);
        }

        private static CalendarEventDto ToDto(CalendarEvent e) => new()
        {
            Id = e.Id,
            Title = e.Title,
            Description = e.Description,
            StartDateTime = e.StartDateTime,
            EndDateTime = e.EndDateTime,
            IsAllDay = e.IsAllDay,
            Location = e.Location,
            EventType = e.EventType,
            IsRecurring = e.IsRecurring,
            RecurrenceRule = e.RecurrenceRule,
            ColorHex = e.ColorHex
        };
    }
}
