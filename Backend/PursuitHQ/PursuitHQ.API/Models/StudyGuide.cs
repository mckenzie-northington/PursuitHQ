namespace PursuitHQ.API.Models
{
    /// <summary>A condensed summary generated from uploaded material.</summary>
    public class StudyGuide
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public int? CourseId { get; set; }
        public Course? Course { get; set; }

        public int? SourceMaterialId { get; set; }
        public StudyMaterial? SourceMaterial { get; set; }

        public int? SourceNoteId { get; set; }
        public Note? SourceNote { get; set; }

        public string Title { get; set; } = string.Empty;

        /// <summary>Markdown.</summary>
        public string Content { get; set; } = string.Empty;

        public bool IsAiGenerated { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
