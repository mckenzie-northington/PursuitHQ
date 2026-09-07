using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.Materials;
using PursuitHQ.API.Models;
using PursuitHQ.API.Services;

namespace PursuitHQ.API.Controllers
{
    /// <summary>Folders inside a course, for organizing study materials.</summary>
    [Route("api/courses/{courseId:int}/folders")]
    public class FoldersController : ApiControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _storage;

        public FoldersController(ApplicationDbContext db, IFileStorageService storage)
        {
            _db = db;
            _storage = storage;
        }

        [HttpGet]
        public async Task<ActionResult<List<FolderDto>>> GetFolders(int courseId, [FromQuery] int? parentId)
        {
            if (!await OwnsCourseAsync(courseId)) return NotFound(CourseNotFound());

            var folders = await _db.MaterialFolders
                .Where(f => f.CourseId == courseId && f.UserId == CurrentUserId)
                .Where(f => parentId.HasValue ? f.ParentFolderId == parentId : f.ParentFolderId == null)
                .OrderBy(f => f.Name)
                .Select(f => new FolderDto
                {
                    Id = f.Id,
                    CourseId = f.CourseId,
                    Name = f.Name,
                    ParentFolderId = f.ParentFolderId,
                    CreatedAt = f.CreatedAt,
                    FileCount = f.StudyMaterials.Count,
                    NoteCount = f.Notes.Count,
                    SubfolderCount = f.ChildFolders.Count
                })
                .ToListAsync();

            return Ok(folders);
        }

        [HttpPost]
        public async Task<ActionResult<FolderDto>> CreateFolder(int courseId, SaveFolderDto dto)
        {
            if (!await OwnsCourseAsync(courseId)) return NotFound(CourseNotFound());

            if (dto.ParentFolderId.HasValue && !await FolderExistsAsync(courseId, dto.ParentFolderId.Value))
            {
                return NotFound(new ApiErrorDto("ParentFolderNotFound", "That parent folder does not exist."));
            }

            var folder = new MaterialFolder
            {
                UserId = CurrentUserId,
                CourseId = courseId,
                Name = dto.Name,
                ParentFolderId = dto.ParentFolderId,
                CreatedAt = DateTime.UtcNow
            };

            _db.MaterialFolders.Add(folder);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetFolders), new { courseId }, ToDto(folder));
        }

        /// <summary>Renames a folder, or moves it under a different parent.</summary>
        [HttpPut("{id:int}")]
        public async Task<ActionResult<FolderDto>> UpdateFolder(int courseId, int id, SaveFolderDto dto)
        {
            if (!await OwnsCourseAsync(courseId)) return NotFound(CourseNotFound());

            var folder = await _db.MaterialFolders
                .FirstOrDefaultAsync(f => f.Id == id && f.CourseId == courseId && f.UserId == CurrentUserId);

            if (folder is null) return NotFound(FolderNotFound());

            if (dto.ParentFolderId == id)
            {
                return BadRequest(new ApiErrorDto("InvalidParent", "A folder cannot be its own parent."));
            }

            // Moving a folder into one of its own descendants would create a
            // loop that nothing could ever list or delete.
            if (dto.ParentFolderId.HasValue)
            {
                if (!await FolderExistsAsync(courseId, dto.ParentFolderId.Value))
                {
                    return NotFound(new ApiErrorDto("ParentFolderNotFound", "That parent folder does not exist."));
                }

                if (await IsDescendantAsync(dto.ParentFolderId.Value, id))
                {
                    return BadRequest(new ApiErrorDto(
                        "InvalidParent", "A folder cannot be moved inside one of its own subfolders."));
                }
            }

            folder.Name = dto.Name;
            folder.ParentFolderId = dto.ParentFolderId;
            await _db.SaveChangesAsync();

            return Ok(ToDto(folder));
        }

        /// <summary>
        /// Deletes a folder and everything inside it, including the stored files.
        /// </summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteFolder(int courseId, int id)
        {
            if (!await OwnsCourseAsync(courseId)) return NotFound(CourseNotFound());

            var folder = await _db.MaterialFolders
                .FirstOrDefaultAsync(f => f.Id == id && f.CourseId == courseId && f.UserId == CurrentUserId);

            if (folder is null) return NotFound(FolderNotFound());

            // Collect this folder and every descendant. The database uses
            // Restrict on the self-reference, so the tree is removed here in
            // deepest-first order rather than by cascade.
            var allIds = await CollectFolderTreeAsync(courseId, id);

            var materials = await _db.StudyMaterials
                .Where(m => m.FolderId != null && allIds.Contains(m.FolderId!.Value))
                .ToListAsync();

            // Remove the database rows first. An orphaned file on disk is
            // recoverable; a database row pointing at a missing file is not.
            var notes = await _db.Notes
                .Where(n => n.FolderId != null && allIds.Contains(n.FolderId!.Value))
                .ToListAsync();

            _db.StudyMaterials.RemoveRange(materials);
            _db.Notes.RemoveRange(notes);

            var folders = await _db.MaterialFolders
                .Where(f => allIds.Contains(f.Id))
                .ToListAsync();

            // Deepest first, so no row is removed while a child still points at it.
            foreach (var f in folders.OrderByDescending(f => DepthOf(f, folders)))
            {
                _db.MaterialFolders.Remove(f);
            }

            await _db.SaveChangesAsync();

            foreach (var m in materials)
            {
                await _storage.DeleteAsync(m.StoredPath);
            }

            return NoContent();
        }

        // ---------- helpers ----------

        private Task<bool> OwnsCourseAsync(int courseId) =>
            _db.Courses.AnyAsync(c => c.Id == courseId && c.UserId == CurrentUserId);

        private Task<bool> FolderExistsAsync(int courseId, int folderId) =>
            _db.MaterialFolders.AnyAsync(f =>
                f.Id == folderId && f.CourseId == courseId && f.UserId == CurrentUserId);

        /// <summary>True if candidateId sits somewhere beneath ancestorId.</summary>
        private async Task<bool> IsDescendantAsync(int candidateId, int ancestorId)
        {
            var current = await _db.MaterialFolders
                .Where(f => f.Id == candidateId)
                .Select(f => f.ParentFolderId)
                .FirstOrDefaultAsync();

            var guard = 0;
            while (current.HasValue && guard++ < 100)
            {
                if (current.Value == ancestorId) return true;

                current = await _db.MaterialFolders
                    .Where(f => f.Id == current.Value)
                    .Select(f => f.ParentFolderId)
                    .FirstOrDefaultAsync();
            }

            return false;
        }

        private async Task<List<int>> CollectFolderTreeAsync(int courseId, int rootId)
        {
            var all = await _db.MaterialFolders
                .Where(f => f.CourseId == courseId && f.UserId == CurrentUserId)
                .Select(f => new { f.Id, f.ParentFolderId })
                .ToListAsync();

            var result = new List<int> { rootId };
            var frontier = new List<int> { rootId };

            while (frontier.Count > 0)
            {
                var children = all
                    .Where(f => f.ParentFolderId.HasValue && frontier.Contains(f.ParentFolderId.Value))
                    .Select(f => f.Id)
                    .ToList();

                children.RemoveAll(result.Contains);
                if (children.Count == 0) break;

                result.AddRange(children);
                frontier = children;
            }

            return result;
        }

        private static int DepthOf(MaterialFolder folder, List<MaterialFolder> all)
        {
            var depth = 0;
            var current = folder;
            var guard = 0;

            while (current.ParentFolderId.HasValue && guard++ < 100)
            {
                var parent = all.FirstOrDefault(f => f.Id == current.ParentFolderId.Value);
                if (parent is null) break;
                current = parent;
                depth++;
            }

            return depth;
        }

        private static ApiErrorDto CourseNotFound() =>
            new("CourseNotFound", "That course does not exist, or it does not belong to you.");

        private static ApiErrorDto FolderNotFound() =>
            new("FolderNotFound", "That folder does not exist, or it does not belong to you.");

        private static FolderDto ToDto(MaterialFolder f) => new()
        {
            Id = f.Id,
            CourseId = f.CourseId,
            Name = f.Name,
            ParentFolderId = f.ParentFolderId,
            CreatedAt = f.CreatedAt
        };
    }
}
