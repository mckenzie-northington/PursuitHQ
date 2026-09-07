namespace PursuitHQ.API.DTOs.Materials
{
    public class StudyMaterialDto
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public int? FolderId { get; set; }

        /// <summary>The name the student sees. StoredPath is never exposed.</summary>
        public string FileName { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string? Description { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}
