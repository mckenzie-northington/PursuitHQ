namespace PursuitHQ.API.Models
{
    /// <summary>
    /// One resume the student maintains. Content is a single structured field
    /// so it can be handed to the AI for review as a whole.
    /// </summary>
    public class Resume
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public string Title { get; set; } = string.Empty;

        /// <summary>JSON or structured text: summary, education, experience, projects, skills.</summary>
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
