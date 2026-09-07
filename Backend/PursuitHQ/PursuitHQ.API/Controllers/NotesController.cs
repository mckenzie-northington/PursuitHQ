using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.Materials;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.Controllers
{
    /// <summary>Notes typed directly in the app, stored per course.</summary>
    [Route("api/courses/{courseId:int}/notes")]
    public class NotesController : ApiControllerBase
    {
        private readonly ApplicationDbContext _db;

        public NotesController(ApplicationDbContext db) => _db = db;

        [HttpGet]
        public async Task<ActionResult<List<NoteDto>>> GetNotes(
            int courseId, [FromQuery] int? folderId, [FromQuery] string? search)
        {
            if (!await OwnsCourseAsync(courseId)) return NotFound(CourseNotFound());

            var query = _db.Notes
                .Where(n => n.CourseId == courseId && n.UserId == CurrentUserId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                // A search looks across the whole course. Restricting it to the
                // current folder would make it useless for the thing people
                // actually search for: a file whose folder they have forgotten.
                var term = search.ToLower();
                query = query.Where(n => n.Title.ToLower().Contains(term));
            }
            else
            {
                // No folderId means the course root; an explicit id means that folder.
                query = folderId.HasValue
                    ? query.Where(n => n.FolderId == folderId)
                    : query.Where(n => n.FolderId == null);
            }

            // The list view omits Content - note bodies can be long and are not
            // needed until a note is opened.
            var notes = await query
                .OrderByDescending(n => n.UpdatedAt)
                .Select(n => new NoteDto
                {
                    Id = n.Id,
                    CourseId = n.CourseId,
                    FolderId = n.FolderId,
                    Title = n.Title,
                    Content = string.Empty,
                    CreatedAt = n.CreatedAt,
                    UpdatedAt = n.UpdatedAt
                })
                .ToListAsync();

            return Ok(notes);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<NoteDto>> GetNote(int courseId, int id)
        {
            if (!await OwnsCourseAsync(courseId)) return NotFound(CourseNotFound());

            var note = await _db.Notes
                .FirstOrDefaultAsync(n => n.Id == id && n.CourseId == courseId && n.UserId == CurrentUserId);

            if (note is null) return NotFound(NoteNotFound());

            return Ok(ToDto(note));
        }

        [HttpPost]
        public async Task<ActionResult<NoteDto>> CreateNote(int courseId, SaveNoteDto dto)
        {
            if (!await OwnsCourseAsync(courseId)) return NotFound(CourseNotFound());

            if (dto.FolderId.HasValue && !await FolderExistsAsync(courseId, dto.FolderId.Value))
            {
                return NotFound(new ApiErrorDto("FolderNotFound", "That folder does not exist."));
            }

            var now = DateTime.UtcNow;

            var note = new Note
            {
                UserId = CurrentUserId,
                CourseId = courseId,
                FolderId = dto.FolderId,
                Title = dto.Title,
                Content = dto.Content ?? string.Empty,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.Notes.Add(note);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetNote), new { courseId, id = note.Id }, ToDto(note));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<NoteDto>> UpdateNote(int courseId, int id, SaveNoteDto dto)
        {
            if (!await OwnsCourseAsync(courseId)) return NotFound(CourseNotFound());

            var note = await _db.Notes
                .FirstOrDefaultAsync(n => n.Id == id && n.CourseId == courseId && n.UserId == CurrentUserId);

            if (note is null) return NotFound(NoteNotFound());

            if (dto.FolderId.HasValue && !await FolderExistsAsync(courseId, dto.FolderId.Value))
            {
                return NotFound(new ApiErrorDto("FolderNotFound", "That folder does not exist."));
            }

            note.Title = dto.Title;
            note.Content = dto.Content ?? string.Empty;
            note.FolderId = dto.FolderId;
            note.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(ToDto(note));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteNote(int courseId, int id)
        {
            if (!await OwnsCourseAsync(courseId)) return NotFound(CourseNotFound());

            var note = await _db.Notes
                .FirstOrDefaultAsync(n => n.Id == id && n.CourseId == courseId && n.UserId == CurrentUserId);

            if (note is null) return NotFound(NoteNotFound());

            _db.Notes.Remove(note);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        // ---------- helpers ----------

        private Task<bool> OwnsCourseAsync(int courseId) =>
            _db.Courses.AnyAsync(c => c.Id == courseId && c.UserId == CurrentUserId);

        private Task<bool> FolderExistsAsync(int courseId, int folderId) =>
            _db.MaterialFolders.AnyAsync(f =>
                f.Id == folderId && f.CourseId == courseId && f.UserId == CurrentUserId);

        private static ApiErrorDto CourseNotFound() =>
            new("CourseNotFound", "That course does not exist, or it does not belong to you.");

        private static ApiErrorDto NoteNotFound() =>
            new("NoteNotFound", "That note does not exist, or it does not belong to you.");

        private static NoteDto ToDto(Note n) => new()
        {
            Id = n.Id,
            CourseId = n.CourseId,
            FolderId = n.FolderId,
            Title = n.Title,
            Content = n.Content,
            CreatedAt = n.CreatedAt,
            UpdatedAt = n.UpdatedAt
        };
    }
}
