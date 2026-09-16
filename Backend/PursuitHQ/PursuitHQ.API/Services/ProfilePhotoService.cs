using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace PursuitHQ.API.Services
{
    public record StoredPhoto(string Path, string ContentType);

    public interface IProfilePhotoService
    {
        /// <summary>
        /// Takes whatever was uploaded and stores a clean, square JPEG.
        /// Throws <see cref="InvalidImageException"/> if it is not an image.
        /// </summary>
        Task<StoredPhoto> SaveAsync(Stream upload, CancellationToken ct = default);
    }

    public class InvalidImageException : Exception
    {
        public InvalidImageException(string message) : base(message) { }
    }

    /// <summary>
    /// Profile photos, re-encoded rather than stored as uploaded.
    ///
    /// Re-encoding is the point of this class, and it is doing three jobs at
    /// once:
    ///
    /// 1. **It strips EXIF.** Phone photos routinely carry GPS coordinates. A
    ///    profile picture is shown to people the student has never met, and
    ///    handing out the location it was taken is a safety problem, not a
    ///    privacy nicety. Decoding and re-encoding drops every metadata block.
    /// 2. **It proves the file is an image.** A file can have a .jpg extension,
    ///    an image content type, and still be something else entirely. If
    ///    ImageSharp cannot decode it, it does not get stored.
    /// 3. **It bounds the size.** An avatar is displayed at 40 pixels and at
    ///    128 on a profile. Storing a 12-megapixel original serves nobody and
    ///    makes every page that shows it slow.
    /// </summary>
    public class ProfilePhotoService : IProfilePhotoService
    {
        /// <summary>Generous for a photo shown at 128px, small enough to send freely.</summary>
        private const int MaxDimension = 512;

        private const int Quality = 82;

        private readonly IFileStorageService _storage;

        public ProfilePhotoService(IFileStorageService storage)
        {
            _storage = storage;
        }

        public async Task<StoredPhoto> SaveAsync(Stream upload, CancellationToken ct = default)
        {
            Image image;

            try
            {
                image = await Image.LoadAsync(upload, ct);
            }
            catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
            {
                throw new InvalidImageException(
                    "That file could not be read as an image. JPEG, PNG and WebP all work.");
            }

            using (image)
            {
                // Crops to a square around the middle before resizing, so faces
                // are not squashed and every avatar in a list is the same shape.
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Crop,
                    Size = new Size(MaxDimension, MaxDimension),
                    Position = AnchorPositionMode.Center
                }));

                // Written to memory first so the storage service receives a
                // seekable stream of exactly the bytes being kept.
                using var encoded = new MemoryStream();
                await image.SaveAsJpegAsync(encoded, new JpegEncoder { Quality = Quality }, ct);
                encoded.Position = 0;

                var path = await _storage.SaveAsync(encoded, "avatar.jpg", ct);

                return new StoredPhoto(path, "image/jpeg");
            }
        }
    }
}
