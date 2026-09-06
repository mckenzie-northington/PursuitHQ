namespace PursuitHQ.API.Models
{
    /// <summary>
    /// Metadata for one uploaded file. The bytes live in file storage; only
    /// this record lives in the database.
    /// </summary>
    public class StudyMaterial
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public int CourseId { get; set; }
        public Course? Course { get; set; }

        public int? FolderId { get; set; }
        public MaterialFolder? Folder { get; set; }

        /// <summary>Original name, shown to the student and used on download.</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>Internal storage key (GUID-based). Never exposed to the client.</summary>
        public string StoredPath { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }

        public string? Description { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
