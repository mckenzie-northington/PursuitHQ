namespace PursuitHQ.API.Services
{
    /// <summary>Bound from the "FileStorage" section of configuration.</summary>
    public class FileStorageOptions
    {
        public const string SectionName = "FileStorage";

        /// <summary>Folder for uploads, relative to the app root. Kept outside wwwroot on purpose.</summary>
        public string LocalPath { get; set; } = "storage/uploads";

        /// <summary>25 MB by default.</summary>
        public long MaxFileSizeBytes { get; set; } = 26_214_400;

        /// <summary>1 GB total per student by default.</summary>
        public long MaxUserQuotaBytes { get; set; } = 1_073_741_824;

        /// <summary>
        /// Extensions students may upload. Deliberately an allow-list, not a
        /// block-list: anything not named here is refused.
        /// </summary>
        public string[] AllowedExtensions { get; set; } =
        {
            ".pdf", ".docx", ".pptx", ".xlsx", ".txt", ".md",
            ".png", ".jpg", ".jpeg", ".gif"
        };

        public string[] AllowedContentTypes { get; set; } =
        {
            "application/pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "text/plain",
            "text/markdown",
            "image/png",
            "image/jpeg",
            "image/gif"
        };

        public bool IsAllowed(string fileName, string contentType)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();

            // Both the extension and the declared content type must be on the
            // list. Checking only one is easy to get around.
            return AllowedExtensions.Contains(ext)
                && AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);
        }
    }
}
