using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.Materials;
using PursuitHQ.API.Models;
using PursuitHQ.API.Services;

namespace PursuitHQ.API.Controllers
{
    /// <summary>Uploaded study material files, per course.</summary>
    [Route("api/courses/{courseId:int}/materials")]
    public class MaterialsController : ApiControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _storage;
        private readonly FileStorageOptions _options;

        public MaterialsController(
            ApplicationDbContext db,
            IFileStorageService storage,
            IOptions<FileStorageOptions> options)
        {
            _db = db;
            _storage = storage;
            _options = options.Value;
        }

        [HttpGet]
        public async Task<ActionResult<List<StudyMaterialDto>>> GetMaterials(
            int courseId, [FromQuery] int? folderId, [FromQuery] string? search)
        {
            if (!await OwnsCourseAsync(courseId)) return NotFound(CourseNotFound());

            var query = _db.StudyMaterials
                .Where(m => m.CourseId == courseId && m.UserId == CurrentUserId);

            // No folderId means the course root; an explicit id means that folder.
            query = folderId.HasValue
                ? query.Where(m => m.FolderId == folderId)
                : query.Where(m => m.FolderId == null);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(m => m.FileName.ToLower().Contains(search.ToLower()));
            }

            var materials = await query
                .OrderByDescending(m => m.UploadedAt)
                .Select(m => ToDto(m))
                .ToListAsync();

            return Ok(materials);
        }

        /// <summary>
        /// Uploads a file. Validation order matters: size, then type, then quota,
        /// and only then is anything written to disk.
        /// </summary>
        [HttpPost]
        [RequestSizeLimit(30_000_000)]
        public async Task<ActionResult<StudyMaterialDto>> Upload(
            int courseId,
            IFormFile file,
            [FromForm] int? folderId,
            [FromForm] string? description,
            CancellationToken ct)
        {
            if (!await OwnsCourseAsync(courseId)) return NotFound(CourseNotFound());

            if (file is null || file.Length == 0)
            {
                return BadRequest(new ApiErrorDto("NoFile", "No file was uploaded."));
            }

            if (file.Length > _options.MaxFileSizeBytes)
            {
                var mb = _options.MaxFileSizeBytes / 1_048_576;
                return StatusCode(413, new ApiErrorDto(
                    "FileTooLarge", $"Files must be {mb} MB or smaller."));
            }

            if (!_options.IsAllowed(file.FileName, file.ContentType))
            {
                return StatusCode(415, new ApiErrorDto(
                    "UnsupportedFileType",
                    $"That file type is not allowed. Accepted types: {string.Join(", ", _options.AllowedExtensions)}"));
            }

            var usedBytes = await _db.StudyMaterials
                .Where(m => m.UserId == CurrentUserId)
                .SumAsync(m => m.SizeBytes, ct);

            if (usedBytes + file.Length > _options.MaxUserQuotaBytes)
            {
                return BadRequest(new ApiErrorDto(
                    "QuotaExceeded", "This upload would exceed your storage limit."));
            }

            if (folderId.HasValue && !await FolderExistsAsync(courseId, folderId.Value))
            {
                return NotFound(new ApiErrorDto("FolderNotFound", "That folder does not exist."));
            }

            string storedPath;
            await using (var stream = file.OpenReadStream())
            {
                storedPath = await _storage.SaveAsync(stream, file.FileName, ct);
            }

            var material = new StudyMaterial
            {
                UserId = CurrentUserId,
                CourseId = courseId,
                FolderId = folderId,
                FileName = Path.GetFileName(file.FileName),
                StoredPath = storedPath,
                ContentType = file.ContentType,
                SizeBytes = file.Length,
                Description = description,
                UploadedAt = DateTime.UtcNow
            };

            _db.StudyMaterials.Add(material);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch
            {
                // The file is on disk but the row failed to save. Clean up rather
                // than leaving a file nothing references.
                await _storage.DeleteAsync(storedPath, ct);
                throw;
            }

            return CreatedAtAction(nameof(GetMaterials), new { courseId }, ToDto(material));
        }

        /// <summary>
        /// Streams a file back with its original name. Every download passes
        /// through here so ownership is always checked.
        /// </summary>
        [HttpGet("{id:int}/download")]
        public async Task<IActionResult> Download(int courseId, int id, CancellationToken ct)
        {
            if (!await OwnsCourseAsync(courseId)) return NotFound(CourseNotFound());

            var material = await _db.StudyMaterials
                .FirstOrDefaultAsync(m => m.Id == id && m.CourseId == courseId && m.UserId == CurrentUserId, ct);

            if (material is null) return NotFound(MaterialNotFound());

            try
            {
                var stream = await _storage.OpenAsync(material.StoredPath, ct);

                // "attachment" makes the browser download rather than render it,
                // which stops an uploaded HTML or SVG file running as a page.
                return File(stream, material.ContentType, material.FileName);
            }
            catch (FileNotFoundException)
            {
                return NotFound(new ApiErrorDto(
                    "FileMissing", "The stored file could not be found."));
            }
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<StudyMaterialDto>> UpdateMaterial(
            int courseId, int id, UpdateMaterialDto dto)
        {
            if (!await OwnsCourseAsync(courseId)) return NotFound(CourseNotFound());

            var material = await _db.StudyMaterials
                .FirstOrDefaultAsync(m => m.Id == id && m.CourseId == courseId && m.UserId == CurrentUserId);

            if (material is null) return NotFound(MaterialNotFound());

            if (dto.FolderId.HasValue && !await FolderExistsAsync(courseId, dto.FolderId.Value))
            {
                return NotFound(new ApiErrorDto("FolderNotFound", "That folder does not exist."));
            }

            // Renaming changes only the display name. The file on disk keeps its
            // GUID name, so nothing needs to move.
            material.FileName = Path.GetFileName(dto.FileName);
            material.Description = dto.Description;
            material.FolderId = dto.FolderId;

            await _db.SaveChangesAsync();

            return Ok(ToDto(material));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteMaterial(int courseId, int id, CancellationToken ct)
        {
            if (!await OwnsCourseAsync(courseId)) return NotFound(CourseNotFound());

            var material = await _db.StudyMaterials
                .FirstOrDefaultAsync(m => m.Id == id && m.CourseId == courseId && m.UserId == CurrentUserId, ct);

            if (material is null) return NotFound(MaterialNotFound());

            var storedPath = material.StoredPath;

            _db.StudyMaterials.Remove(material);
            await _db.SaveChangesAsync(ct);

            await _storage.DeleteAsync(storedPath, ct);

            return NoContent();
        }

        /// <summary>How much of their storage quota the student has used.</summary>
        [HttpGet("/api/storage/usage")]
        public async Task<ActionResult<StorageUsageDto>> GetUsage()
        {
            var files = await _db.StudyMaterials
                .Where(m => m.UserId == CurrentUserId)
                .Select(m => m.SizeBytes)
                .ToListAsync();

            return Ok(new StorageUsageDto
            {
                UsedBytes = files.Sum(),
                QuotaBytes = _options.MaxUserQuotaBytes,
                FileCount = files.Count
            });
        }

        // ---------- helpers ----------

        private Task<bool> OwnsCourseAsync(int courseId) =>
            _db.Courses.AnyAsync(c => c.Id == courseId && c.UserId == CurrentUserId);

        private Task<bool> FolderExistsAsync(int courseId, int folderId) =>
            _db.MaterialFolders.AnyAsync(f =>
                f.Id == folderId && f.CourseId == courseId && f.UserId == CurrentUserId);

        private static ApiErrorDto CourseNotFound() =>
            new("CourseNotFound", "That course does not exist, or it does not belong to you.");

        private static ApiErrorDto MaterialNotFound() =>
            new("MaterialNotFound", "That file does not exist, or it does not belong to you.");

        private static StudyMaterialDto ToDto(StudyMaterial m) => new()
        {
            Id = m.Id,
            CourseId = m.CourseId,
            FolderId = m.FolderId,
            FileName = m.FileName,
            ContentType = m.ContentType,
            SizeBytes = m.SizeBytes,
            Description = m.Description,
            UploadedAt = m.UploadedAt
        };
    }
}
