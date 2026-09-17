namespace PursuitHQ.API.Services
{
    /// <summary>Bound from the "FileStorage" section of configuration.</summary>
    public class FileStorageOptions
    {
        public const string SectionName = "FileStorage";

        /// <summary>
        /// "Local" or "S3". Anything else, or an "S3" with missing credentials,
        /// falls back to local disk rather than refusing to start - a
        /// misconfigured bucket should not take the whole app down with it.
        /// </summary>
        public string Provider { get; set; } = "Local";

        /// <summary>
        /// S3-compatible endpoint. For Cloudflare R2 this is
        /// https://&lt;account-id&gt;.r2.cloudflarestorage.com
        /// </summary>
        public string ServiceUrl { get; set; } = string.Empty;

        public string Bucket { get; set; } = string.Empty;
        public string AccessKeyId { get; set; } = string.Empty;

        /// <summary>A secret. Environment variable or user-secrets only.</summary>
        public string SecretAccessKey { get; set; } = string.Empty;

        /// <summary>
        /// True when Provider is S3 and the settings are good enough to build a
        /// client from.
        ///
        /// ServiceUrl is checked for being an actual absolute http(s) URL, not
        /// merely for being non-empty. That distinction cost a day: the value
        /// deployed was a bare Cloudflare account id rather than the endpoint it
        /// belongs to, this returned true, and the AWS SDK threw from its
        /// constructor. Because the client is a singleton, that failure did not
        /// appear at startup where it would have been obvious - it appeared as
        /// a 500 on every page that touches file storage, and on no others,
        /// which reads as four unrelated bugs rather than one setting.
        /// </summary>
        public bool UsesObjectStorage => WantsObjectStorage && ConfigurationProblem is null;

        /// <summary>
        /// Whether object storage was asked for at all.
        ///
        /// Separate from UsesObjectStorage, and both halves are needed:
        /// ConfigurationProblem is null for a local-disk setup too - there is
        /// nothing wrong with it - so "no problem" on its own would have meant
        /// every development machine trying to build an S3 client out of blank
        /// settings.
        /// </summary>
        public bool WantsObjectStorage =>
            string.Equals(Provider, "S3", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// What is wrong with the object storage settings, or null when nothing
        /// is - including when object storage was never asked for.
        ///
        /// Logged at startup. A misconfigured bucket falls back to local disk
        /// rather than refusing to start, which is the right call, but silently
        /// falling back on a host with an ephemeral disk means uploads that
        /// disappear on the next restart. That deserves a sentence in the log.
        /// </summary>
        public string? ConfigurationProblem
        {
            get
            {
                if (!WantsObjectStorage) return null;

                if (string.IsNullOrWhiteSpace(ServiceUrl)) return "ServiceUrl is not set";
                if (string.IsNullOrWhiteSpace(Bucket)) return "Bucket is not set";
                if (string.IsNullOrWhiteSpace(AccessKeyId)) return "AccessKeyId is not set";
                if (string.IsNullOrWhiteSpace(SecretAccessKey)) return "SecretAccessKey is not set";

                if (!Uri.TryCreate(ServiceUrl, UriKind.Absolute, out var endpoint)
                    || (endpoint.Scheme != Uri.UriSchemeHttps && endpoint.Scheme != Uri.UriSchemeHttp))
                {
                    return $"ServiceUrl is not an absolute http(s) URL: \"{ServiceUrl}\". "
                        + "For Cloudflare R2 it looks like "
                        + "https://<account-id>.r2.cloudflarestorage.com";
                }

                return null;
            }
        }

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
