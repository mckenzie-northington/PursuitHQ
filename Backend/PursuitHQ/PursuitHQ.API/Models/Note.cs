namespace PursuitHQ.API.Models
{
    /// <summary>A note typed directly in the app, stored as text.</summary>
    public class Note
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public int CourseId { get; set; }
        public Course? Course { get; set; }

        public int? FolderId { get; set; }
        public MaterialFolder? Folder { get; set; }

        public string Title { get; set; } = string.Empty;

        /// <summary>Markdown or plain text.</summary>
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
