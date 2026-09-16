using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.Services;

namespace PursuitHQ.API.Controllers
{
    /// <summary>
    /// The signed-in student's own profile photo.
    ///
    /// Reading somebody's photo lives on StudentsController, behind the same
    /// visibility check as their profile. Only writing your own is here.
    /// </summary>
    [Route("api/profile/photo")]
    public class ProfilePhotoController : ApiControllerBase
    {
        /// <summary>
        /// Generous for a phone photo, and small enough that a slow connection
        /// is not left uploading for minutes before being told no.
        /// </summary>
        private const long MaxBytes = 8 * 1024 * 1024;

        private readonly ApplicationDbContext _db;
        private readonly IProfilePhotoService _photos;
        private readonly IFileStorageService _storage;
        private readonly ILogger<ProfilePhotoController> _logger;

        public ProfilePhotoController(
            ApplicationDbContext db,
            IProfilePhotoService photos,
            IFileStorageService storage,
            ILogger<ProfilePhotoController> logger)
        {
            _db = db;
            _photos = photos;
            _storage = storage;
            _logger = logger;
        }

        [HttpPost]
        [RequestSizeLimit(MaxBytes)]
        public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest(new ApiErrorDto("NoFile", "Choose a photo first."));
            }

            if (file.Length > MaxBytes)
            {
                return BadRequest(new ApiErrorDto(
                    "FileTooLarge", "That photo is over 8MB. Try a smaller one."));
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId, ct);
            if (user is null) return Unauthorized();

            var previous = user.PhotoPath;

            try
            {
                await using var upload = file.OpenReadStream();

                // Re-encoded rather than stored as sent: that is what strips the
                // EXIF - phone photos carry GPS coordinates, and this one is
                // shown to people the student has not met.
                var stored = await _photos.SaveAsync(upload, ct);

                user.PhotoPath = stored.Path;
                user.PhotoContentType = stored.ContentType;

                await _db.SaveChangesAsync(ct);
            }
            catch (InvalidImageException ex)
            {
                return BadRequest(new ApiErrorDto("InvalidImage", ex.Message));
            }

            // Only after the new one is safely recorded. Deleting first would
            // leave the student with no photo at all if the upload then failed.
            await TryDeleteAsync(previous, ct);

            return NoContent();
        }

        [HttpDelete]
        public async Task<IActionResult> Remove(CancellationToken ct)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId, ct);
            if (user is null) return Unauthorized();

            var previous = user.PhotoPath;

            user.PhotoPath = null;
            user.PhotoContentType = null;
            await _db.SaveChangesAsync(ct);

            await TryDeleteAsync(previous, ct);

            return NoContent();
        }

        /// <summary>
        /// A file left behind is clutter; an exception here would undo a save
        /// that already succeeded.
        /// </summary>
        private async Task TryDeleteAsync(string? path, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                await _storage.DeleteAsync(path, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete the old profile photo at {Path}.", path);
            }
        }
    }
}
