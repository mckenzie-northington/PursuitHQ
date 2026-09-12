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

            // The extension is the real gate, and it is an allow-list.
            if (!AllowedExtensions.Contains(ext)) return false;

            // A missing or generic content type is accepted when the extension
            // is already on the list.
            //
            // This used to require both to match, which refused real coursework:
            // Windows reports plenty of ordinary files as
            // application/octet-stream - a .pptx saved out of Teams, a .docx
            // pulled from a download - and the upload came back as "that file
            // type is not allowed" for a perfectly valid PowerPoint.
            //
            // Very little is given up. The browser only repeats what the
            // operating system told it, so this header is trivially spoofable
            // and was never really a boundary. What actually protects the app is
            // unchanged: the extension allow-list above, GUID filenames, storage
            // outside wwwroot, and downloads served as attachments through an
            // authorized endpoint.
            if (string.IsNullOrWhiteSpace(contentType)) return true;

            if (contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // A type that is present but says something else entirely - a .pdf
            // announcing itself as text/html - is still refused. That is a
            // contradiction worth noticing rather than waving through.
            return AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);
        }
    }
}
