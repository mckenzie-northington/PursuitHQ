using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.Courses;
using PursuitHQ.API.Models;
using PursuitHQ.API.Services;

namespace PursuitHQ.API.Controllers
{
    [Route("api/[controller]")]
    public class CoursesController : ApiControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ICreationNotifier _notifier;
        private readonly IFileStorageService _storage;

        public CoursesController(
            ApplicationDbContext db, ICreationNotifier notifier, IFileStorageService storage)
        {
            _db = db;
            _notifier = notifier;
            _storage = storage;
        }

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

            // After the save, and never allowed to fail it.
            await _notifier.ItemCreatedAsync(
                CurrentUserId, "course", course.Name,
                string.IsNullOrWhiteSpace(course.Semester) ? null : course.Semester);

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
        /// <summary>
        /// Deletes a course and everything under it.
        /// </summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteCourse(
            int id, [FromServices] IWebHostEnvironment environment, CancellationToken ct)
        {
            var course = await _db.Courses
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == CurrentUserId, ct);

            if (course is null) return NotFound(NotFoundError());

            // Four things hang off a course by an OPTIONAL foreign key: quizzes,
            // flashcard decks, study guides and study sessions. EF's default for
            // an optional relationship is ClientSetNull, which in the database
            // means "refuse the delete" - so a course with any practice test,
            // deck or guide could not be deleted at all. They are course
            // material, so they go with it.
            var quizIds = await _db.Quizzes
                .Where(q => q.CourseId == course.Id)
                .Select(q => q.Id)
                .ToListAsync(ct);

            if (quizIds.Count > 0)
            {
                // Attempts first, and in their own save. A quiz answer points at
                // its question with Restrict, so that deleting a question cannot
                // quietly wipe historical results - which means the answers have
                // to be gone before the questions can be. Deleting the attempt
                // cascades its answers away.
                var attempts = await _db.QuizAttempts
                    .Where(a => quizIds.Contains(a.QuizId))
                    .ToListAsync(ct);

                _db.QuizAttempts.RemoveRange(attempts);
                await _db.SaveChangesAsync(ct);

                var quizzes = await _db.Quizzes
                    .Where(q => quizIds.Contains(q.Id))
                    .ToListAsync(ct);

                _db.Quizzes.RemoveRange(quizzes);
            }

            _db.FlashcardDecks.RemoveRange(
                await _db.FlashcardDecks.Where(d => d.CourseId == course.Id).ToListAsync(ct));

            _db.StudyGuides.RemoveRange(
                await _db.StudyGuides.Where(g => g.CourseId == course.Id).ToListAsync(ct));

            _db.StudySessions.RemoveRange(
                await _db.StudySessions.Where(s => s.CourseId == course.Id).ToListAsync(ct));

            await _db.SaveChangesAsync(ct);

            // A material folder points at its own parent with Restrict, so that
            // deleting one folder cannot quietly take its subfolders with it.
            // That same rule blocks the cascade from the course: the database
            // refuses to remove a parent folder while a child still references
            // it, so deleting a course that had nested folders failed outright.
            //
            // Flattening them first makes every folder a root, and the course's
            // own cascade then clears them in one go. Cheaper and less fragile
            // than walking the tree deepest-first, because they are all going
            // anyway - there is no parent left to protect.
            var folders = await _db.MaterialFolders
                .Where(f => f.CourseId == course.Id)
                .ToListAsync(ct);

            if (folders.Count > 0)
            {
                foreach (var folder in folders) folder.ParentFolderId = null;

                // Notes and materials point at their folder by an optional
                // foreign key, which the database also treats as "refuse the
                // delete". The course cascade removes the folders and the notes
                // in one statement and the order between them is not
                // guaranteed - so the folder can go first, and the note still
                // pointing at it blocks the whole delete. Which course this hit
                // was pure luck.
                //
                // Detaching them first removes the argument. They are deleted a
                // moment later by the course's own cascade either way.
                var filedNotes = await _db.Notes
                    .Where(n => n.CourseId == course.Id && n.FolderId != null)
                    .ToListAsync(ct);

                foreach (var note in filedNotes) note.FolderId = null;

                var filedMaterials = await _db.StudyMaterials
                    .Where(m => m.CourseId == course.Id && m.FolderId != null)
                    .ToListAsync(ct);

                foreach (var material in filedMaterials) material.FolderId = null;

                await _db.SaveChangesAsync(ct);
            }

            // Read the stored paths before the rows go. Once the course is
            // deleted nothing points at those files, and they would sit on disk
            // forever taking up the student's quota.
            var storedPaths = await _db.StudyMaterials
                .Where(m => m.CourseId == course.Id)
                .Select(m => m.StoredPath)
                .ToListAsync(ct);

            try
            {
                _db.Courses.Remove(course);
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                // A bare 500 says nothing, and this delete has a lot of tables
                // behind it. Postgres names the exact constraint that refused,
                // and that name points straight at the table still holding a
                // reference - worth putting on screen rather than leaving in a
                // terminal for someone to go hunting for.
                //
                // Only in development: the message carries table and column
                // names, which nobody outside should be handed.
                var detail = ex.InnerException?.Message ?? ex.Message;

                return Conflict(new ApiErrorDto(
                    "CourseInUse",
                    environment.IsDevelopment()
                        ? $"Could not delete this course. The database refused: {detail}"
                        : "Something belonging to this course is still in use, so it could not be deleted."));
            }

            // After the rows, not before: an orphaned file is recoverable, a
            // row pointing at a missing file is not.
            foreach (var storedPath in storedPaths)
            {
                await _storage.DeleteAsync(storedPath, ct);
            }

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
