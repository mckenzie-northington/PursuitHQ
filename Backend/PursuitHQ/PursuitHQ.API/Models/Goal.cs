namespace PursuitHQ.API.Models
{
    /// <summary>A career or academic goal with progress tracking.</summary>
    public class Goal
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        /// <summary>0-100.</summary>
        public int Progress { get; set; }

        public DateTime? TargetDate { get; set; }
        public bool IsCompleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
