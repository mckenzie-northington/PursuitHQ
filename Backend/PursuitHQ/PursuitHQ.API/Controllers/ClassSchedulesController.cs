using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.Courses;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.Controllers
{
    /// <summary>
    /// Weekly meeting times, nested under a course. A course that meets
    /// Monday and Wednesday has two of these.
    /// </summary>
    [Route("api/courses/{courseId:int}/schedules")]
    public class ClassSchedulesController : ApiControllerBase
    {
        private readonly ApplicationDbContext _db;

        public ClassSchedulesController(ApplicationDbContext db) => _db = db;

        [HttpGet]
        public async Task<ActionResult<List<ClassScheduleDto>>> GetSchedules(int courseId)
        {
            if (!await OwnsCourseAsync(courseId)) return NotFound(CourseNotFound());

            var schedules = await _db.ClassSchedules
                .Where(s => s.CourseId == courseId)
                .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
                .Select(s => ToDto(s))
                .ToListAsync();

            return Ok(schedules);
        }

        [HttpPost]
        public async Task<ActionResult<ClassScheduleDto>> CreateSchedule(int courseId, SaveClassScheduleDto dto)
        {
            if (!await OwnsCourseAsync(courseId)) return NotFound(CourseNotFound());

            if (dto.EndTime <= dto.StartTime)
            {
                return BadRequest(new ApiErrorDto(
                    "InvalidTimeRange", "End time must be after start time."));
            }

            var schedule = new ClassSchedule
            {
                CourseId = courseId,
                DayOfWeek = dto.DayOfWeek,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                Location = dto.Location
            };

            _db.ClassSchedules.Add(schedule);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSchedules), new { courseId }, ToDto(schedule));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ClassScheduleDto>> UpdateSchedule(int courseId, int id, SaveClassScheduleDto dto)
        {
            if (!await OwnsCourseAsync(courseId)) return NotFound(CourseNotFound());

            if (dto.EndTime <= dto.StartTime)
            {
                return BadRequest(new ApiErrorDto(
                    "InvalidTimeRange", "End time must be after start time."));
            }

            var schedule = await _db.ClassSchedules
                .FirstOrDefaultAsync(s => s.Id == id && s.CourseId == courseId);

            if (schedule is null)
            {
                return NotFound(new ApiErrorDto("ScheduleNotFound", "That meeting time does not exist."));
            }

            schedule.DayOfWeek = dto.DayOfWeek;
            schedule.StartTime = dto.StartTime;
            schedule.EndTime = dto.EndTime;
            schedule.Location = dto.Location;

            await _db.SaveChangesAsync();

            return Ok(ToDto(schedule));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteSchedule(int courseId, int id)
        {
            if (!await OwnsCourseAsync(courseId)) return NotFound(CourseNotFound());

            var schedule = await _db.ClassSchedules
                .FirstOrDefaultAsync(s => s.Id == id && s.CourseId == courseId);

            if (schedule is null)
            {
                return NotFound(new ApiErrorDto("ScheduleNotFound", "That meeting time does not exist."));
            }

            _db.ClassSchedules.Remove(schedule);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        // ---------- helpers ----------

        /// <summary>
        /// Nested resources must verify the parent too. Without this, anyone
        /// could read or edit schedules by guessing a course id.
        /// </summary>
        private Task<bool> OwnsCourseAsync(int courseId) =>
            _db.Courses.AnyAsync(c => c.Id == courseId && c.UserId == CurrentUserId);

        private static ApiErrorDto CourseNotFound() =>
            new("CourseNotFound", "That course does not exist, or it does not belong to you.");

        private static ClassScheduleDto ToDto(ClassSchedule s) => new()
        {
            Id = s.Id,
            CourseId = s.CourseId,
            DayOfWeek = s.DayOfWeek,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            Location = s.Location
        };
    }
}
