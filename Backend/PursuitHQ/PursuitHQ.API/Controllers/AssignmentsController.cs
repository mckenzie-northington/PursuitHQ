using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.Assignments;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.Controllers
{
    [Route("api/[controller]")]
    public class AssignmentsController : ApiControllerBase
    {
        private readonly ApplicationDbContext _db;

        public AssignmentsController(ApplicationDbContext db) => _db = db;

        /// <summary>
        /// Assignments across all of the student's courses, with filters.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<AssignmentDto>>> GetAssignments(
            [FromQuery] int? courseId,
            [FromQuery] AssignmentStatus? status,
            [FromQuery] DateTime? dueBefore,
            [FromQuery] DateTime? dueAfter)
        {
            // Assignment has no UserId of its own - it belongs to a Course,
            // which belongs to a user. So ownership is enforced through the
            // course relationship.
            var query = _db.Assignments
                .Where(a => a.Course!.UserId == CurrentUserId);

            if (courseId.HasValue) query = query.Where(a => a.CourseId == courseId.Value);
            if (status.HasValue) query = query.Where(a => a.Status == status.Value);
            if (dueBefore.HasValue) query = query.Where(a => a.DueDate <= dueBefore.Value);
            if (dueAfter.HasValue) query = query.Where(a => a.DueDate >= dueAfter.Value);

            var now = DateTime.UtcNow;

            var assignments = await query
                .OrderBy(a => a.DueDate)
                .Select(a => new AssignmentDto
                {
                    Id = a.Id,
                    CourseId = a.CourseId,
                    CourseName = a.Course!.Name,
                    Title = a.Title,
                    Description = a.Description,
                    DueDate = a.DueDate,
                    Status = a.Status,
                    Grade = a.Grade,
                    CreatedAt = a.CreatedAt,
                    IsOverdue = a.DueDate < now && a.Status != AssignmentStatus.Completed
                })
                .ToListAsync();

            return Ok(assignments);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<AssignmentDto>> GetAssignment(int id)
        {
            var assignment = await _db.Assignments
                .Include(a => a.Course)
                .FirstOrDefaultAsync(a => a.Id == id && a.Course!.UserId == CurrentUserId);

            if (assignment is null) return NotFound(NotFoundError());

            return Ok(ToDto(assignment));
        }

        [HttpPost]
        public async Task<ActionResult<AssignmentDto>> CreateAssignment(CreateAssignmentDto dto)
        {
            // Confirm the student owns the course they are adding this to.
            var course = await _db.Courses
                .FirstOrDefaultAsync(c => c.Id == dto.CourseId && c.UserId == CurrentUserId);

            if (course is null)
            {
                return NotFound(new ApiErrorDto(
                    "CourseNotFound", "That course does not exist, or it does not belong to you."));
            }

            var assignment = new Assignment
            {
                CourseId = dto.CourseId,
                Title = dto.Title,
                Description = dto.Description,
                DueDate = dto.DueDate,
                Status = dto.Status,
                CreatedAt = DateTime.UtcNow
            };

            _db.Assignments.Add(assignment);
            await _db.SaveChangesAsync();

            assignment.Course = course;

            return CreatedAtAction(nameof(GetAssignment), new { id = assignment.Id }, ToDto(assignment));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<AssignmentDto>> UpdateAssignment(int id, UpdateAssignmentDto dto)
        {
            var assignment = await _db.Assignments
                .Include(a => a.Course)
                .FirstOrDefaultAsync(a => a.Id == id && a.Course!.UserId == CurrentUserId);

            if (assignment is null) return NotFound(NotFoundError());

            assignment.Title = dto.Title;
            assignment.Description = dto.Description;
            assignment.DueDate = dto.DueDate;
            assignment.Status = dto.Status;
            assignment.Grade = dto.Grade;

            await _db.SaveChangesAsync();

            return Ok(ToDto(assignment));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteAssignment(int id)
        {
            var assignment = await _db.Assignments
                .Include(a => a.Course)
                .FirstOrDefaultAsync(a => a.Id == id && a.Course!.UserId == CurrentUserId);

            if (assignment is null) return NotFound(NotFoundError());

            _db.Assignments.Remove(assignment);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        // ---------- helpers ----------

        private static ApiErrorDto NotFoundError() =>
            new("AssignmentNotFound", "That assignment does not exist, or it does not belong to you.");

        private static AssignmentDto ToDto(Assignment a) => new()
        {
            Id = a.Id,
            CourseId = a.CourseId,
            CourseName = a.Course?.Name ?? string.Empty,
            Title = a.Title,
            Description = a.Description,
            DueDate = a.DueDate,
            Status = a.Status,
            Grade = a.Grade,
            CreatedAt = a.CreatedAt,
            IsOverdue = a.DueDate < DateTime.UtcNow && a.Status != AssignmentStatus.Completed
        };
    }
}
