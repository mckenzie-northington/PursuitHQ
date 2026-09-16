using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
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

        /// <summary>
        /// The same treatment for a picture shared in a conversation, but
        /// without the square crop - a screenshot is not a portrait.
        /// </summary>
        Task<StoredPhoto> SaveSharedImageAsync(Stream upload, CancellationToken ct = default);
    }

    public class InvalidImageException : Exception
    {
        public InvalidImageException(string message) : base(message) { }
    }

    /// <summary>
    /// Images, re-encoded rather than stored as uploaded.
    ///
    /// Re-encoding is the point of this class, and it does three jobs at once:
    ///
    /// 1. **It strips EXIF.** Phone photos routinely carry GPS coordinates. A
    ///    profile picture is shown to people the student has never met, and a
    ///    photo dropped into a group chat reaches everybody in the room -
    ///    handing out where it was taken is a safety problem, not a privacy
    ///    nicety. Decoding and re-encoding drops every metadata block.
    /// 2. **It proves the file is an image.** A file can carry a .jpg extension
    ///    and an image content type and still be something else entirely. If
    ///    ImageSharp cannot decode it, it is not stored - and only a file that
    ///    made it through here is ever marked safe to render in an img tag.
    /// 3. **It bounds the size.** Nothing here is displayed above about 1600
    ///    pixels, and a 12-megapixel original serves nobody.
    /// </summary>
    public class ProfilePhotoService : IProfilePhotoService
    {
        /// <summary>Generous for a photo shown at 128px, small enough to send freely.</summary>
        private const int AvatarSize = 512;

        /// <summary>Roughly a laptop screenshot at full width, and no larger.</summary>
        private const int SharedMaxSize = 1600;

        private const int Quality = 82;

        private readonly IFileStorageService _storage;

        public ProfilePhotoService(IFileStorageService storage)
        {
            _storage = storage;
        }

        public Task<StoredPhoto> SaveAsync(Stream upload, CancellationToken ct = default) =>
            StoreAsync(upload, AvatarSize, crop: true, ct);

        public Task<StoredPhoto> SaveSharedImageAsync(Stream upload, CancellationToken ct = default) =>
            StoreAsync(upload, SharedMaxSize, crop: false, ct);

        private async Task<StoredPhoto> StoreAsync(
            Stream upload, int maxSize, bool crop, CancellationToken ct)
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
                // PNG in, PNG out. Screenshots of code and slides are the most
                // common thing a student shares, and JPEG turns small text into
                // a smear. Everything else becomes JPEG, which is far smaller
                // for photographs.
                var wasPng = string.Equals(
                    image.Metadata.DecodedImageFormat?.Name, "PNG", StringComparison.OrdinalIgnoreCase);

                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    // Crop squares a portrait around the middle so every avatar
                    // in a list matches. Max keeps the shape and only shrinks
                    // what is oversized, which is what a shared picture wants.
                    Mode = crop ? ResizeMode.Crop : ResizeMode.Max,
                    Size = new Size(maxSize, maxSize),
                    Position = AnchorPositionMode.Center
                }));

                // Written to memory first so the storage service receives a
                // seekable stream of exactly the bytes being kept.
                using var encoded = new MemoryStream();

                if (wasPng)
                {
                    await image.SaveAsPngAsync(encoded, new PngEncoder(), ct);
                }
                else
                {
                    await image.SaveAsJpegAsync(encoded, new JpegEncoder { Quality = Quality }, ct);
                }

                encoded.Position = 0;

                var contentType = wasPng ? "image/png" : "image/jpeg";
                var name = wasPng ? "image.png" : "image.jpg";

                return new StoredPhoto(await _storage.SaveAsync(encoded, name, ct), contentType);
            }
        }
    }
}
