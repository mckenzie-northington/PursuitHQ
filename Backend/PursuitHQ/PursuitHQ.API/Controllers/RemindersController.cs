using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.Reminders;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.Controllers
{
    /// <summary>
    /// Things to do on a day, ticked off from the calendar.
    /// </summary>
    [Route("api/reminders")]
    public class RemindersController : ApiControllerBase
    {
        private readonly ApplicationDbContext _db;

        public RemindersController(ApplicationDbContext db) => _db = db;

        /// <summary>
        /// Everything between two dates, or everything upcoming when no range
        /// is given.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<ReminderDto>>> GetReminders(
            [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
        {
            var query = _db.Reminders.Where(r => r.UserId == CurrentUserId);

            if (from.HasValue) query = query.Where(r => r.Date >= from.Value);
            if (to.HasValue) query = query.Where(r => r.Date <= to.Value);

            var reminders = await query
                .OrderBy(r => r.Date)
                .ThenBy(r => r.Id)
                .Select(r => ToDto(r))
                .ToListAsync();

            return Ok(reminders);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ReminderDto>> GetReminder(int id)
        {
            var reminder = await FindAsync(id);
            if (reminder is null) return NotFound(NotFoundError());

            return Ok(ToDto(reminder));
        }

        [HttpPost]
        public async Task<ActionResult<ReminderDto>> CreateReminder(SaveReminderDto dto)
        {
            var reminder = new Reminder
            {
                UserId = CurrentUserId,
                Title = dto.Title.Trim(),
                Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                Date = dto.Date,
                IsCompleted = dto.IsCompleted,
                CreatedAt = DateTime.UtcNow
            };

            _db.Reminders.Add(reminder);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetReminder), new { id = reminder.Id }, ToDto(reminder));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ReminderDto>> UpdateReminder(int id, SaveReminderDto dto)
        {
            var reminder = await FindAsync(id);
            if (reminder is null) return NotFound(NotFoundError());

            reminder.Title = dto.Title.Trim();
            reminder.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
            reminder.Date = dto.Date;
            reminder.IsCompleted = dto.IsCompleted;

            await _db.SaveChangesAsync();

            return Ok(ToDto(reminder));
        }

        /// <summary>Flips just the tick - what the calendar checkbox calls.</summary>
        [HttpPatch("{id:int}/status")]
        public async Task<ActionResult<ReminderDto>> UpdateStatus(
            int id, UpdateReminderStatusDto dto)
        {
            var reminder = await FindAsync(id);
            if (reminder is null) return NotFound(NotFoundError());

            reminder.IsCompleted = dto.IsCompleted;
            await _db.SaveChangesAsync();

            return Ok(ToDto(reminder));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteReminder(int id)
        {
            var reminder = await FindAsync(id);
            if (reminder is null) return NotFound(NotFoundError());

            _db.Reminders.Remove(reminder);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        // ---------- helpers ----------

        private Task<Reminder?> FindAsync(int id) =>
            _db.Reminders.FirstOrDefaultAsync(r => r.Id == id && r.UserId == CurrentUserId);

        private static ApiErrorDto NotFoundError() =>
            new("ReminderNotFound", "That reminder does not exist, or it does not belong to you.");

        private static ReminderDto ToDto(Reminder r) => new()
        {
            Id = r.Id,
            Title = r.Title,
            Notes = r.Notes,
            Date = r.Date,
            IsCompleted = r.IsCompleted,
            CreatedAt = r.CreatedAt
        };
    }
}
