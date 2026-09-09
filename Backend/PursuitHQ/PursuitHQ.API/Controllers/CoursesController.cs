using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.Courses;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.Controllers
{
    [Route("api/[controller]")]
    public class CoursesController : ApiControllerBase
    {
        private readonly ApplicationDbContext _db;

        public CoursesController(ApplicationDbContext db) => _db = db;

        /// <summary>All of the student's courses, optionally filtered by semester.</summary>
        [HttpGet]
        public async Task<ActionResult<List<CourseDto>>> GetCourses([FromQuery] string? semester)
        {
            // Note the UserId filter. Every query in this file has one.
            var query = _db.Courses.Where(c => c.UserId == CurrentUserId);

            if (!string.IsNullOrWhiteSpace(semester))
            {
                query = query.Where(c => c.Semester == semester);
            }

            var courses = await query
                .Include(c => c.ClassSchedules)
                .OrderBy(c => c.Name)
                .Select(c => new CourseDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Professor = c.Professor,
                    Semester = c.Semester,
                    StartDate = c.StartDate,
                    EndDate = c.EndDate,
                    CreditHours = c.CreditHours,
                    ColorHex = c.ColorHex,
                    CreatedAt = c.CreatedAt,
                    AssignmentCount = c.Assignments.Count,
                    Schedules = c.ClassSchedules
                        .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
                        .Select(s => new ClassScheduleDto
                        {
                            Id = s.Id,
                            CourseId = s.CourseId,
                            DayOfWeek = s.DayOfWeek,
                            StartTime = s.StartTime,
                            EndTime = s.EndTime,
                            Location = s.Location
                        }).ToList()
                })
                .ToListAsync();

            return Ok(courses);
        }

        /// <summary>One course, with its meeting times.</summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<CourseDto>> GetCourse(int id)
        {
            var course = await _db.Courses
                .Where(c => c.Id == id && c.UserId == CurrentUserId)
                .Include(c => c.ClassSchedules)
                .FirstOrDefaultAsync();

            // 404 rather than 403: a course you do not own should look like it
            // does not exist, so this endpoint cannot be used to probe for ids.
            if (course is null) return NotFound(NotFoundError());

            return Ok(ToDto(course));
        }

        [HttpPost]
        public async Task<ActionResult<CourseDto>> CreateCourse(CreateCourseDto dto)
        {
            var course = new Course
            {
                UserId = CurrentUserId,   // from the token, never from the request body
                Name = dto.Name,
                Professor = dto.Professor,
                Semester = dto.Semester,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                CreditHours = dto.CreditHours,
                ColorHex = dto.ColorHex,
                CreatedAt = DateTime.UtcNow
            };

            _db.Courses.Add(course);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCourse), new { id = course.Id }, ToDto(course));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<CourseDto>> UpdateCourse(int id, UpdateCourseDto dto)
        {
            var course = await _db.Courses
                .Include(c => c.ClassSchedules)
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == CurrentUserId);

            if (course is null) return NotFound(NotFoundError());

            course.Name = dto.Name;
            course.Professor = dto.Professor;
            course.Semester = dto.Semester;
            course.StartDate = dto.StartDate;
            course.EndDate = dto.EndDate;
            course.CreditHours = dto.CreditHours;
            course.ColorHex = dto.ColorHex;

            await _db.SaveChangesAsync();

            return Ok(ToDto(course));
        }

        /// <summary>
        /// Deletes a course. Cascade rules also remove its schedules,
        /// assignments, folders, materials, and notes.
        /// </summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            var course = await _db.Courses
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == CurrentUserId);

            if (course is null) return NotFound(NotFoundError());

            _db.Courses.Remove(course);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        // ---------- helpers ----------

        private static ApiErrorDto NotFoundError() =>
            new("CourseNotFound", "That course does not exist, or it does not belong to you.");

        private static CourseDto ToDto(Course c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            Professor = c.Professor,
            Semester = c.Semester,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            CreditHours = c.CreditHours,
            ColorHex = c.ColorHex,
            CreatedAt = c.CreatedAt,
            AssignmentCount = c.Assignments?.Count ?? 0,
            Schedules = (c.ClassSchedules ?? new List<ClassSchedule>())
                .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
                .Select(s => new ClassScheduleDto
                {
                    Id = s.Id,
                    CourseId = s.CourseId,
                    DayOfWeek = s.DayOfWeek,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    Location = s.Location
                }).ToList()
        };
    }
}
