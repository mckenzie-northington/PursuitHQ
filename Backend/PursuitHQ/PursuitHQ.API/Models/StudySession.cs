namespace PursuitHQ.API.Models
{
    /// <summary>A planned block of study time, created by hand or by the AI planner.</summary>
    public class StudySession
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public int? CourseId { get; set; }
        public Course? Course { get; set; }

        public string Title { get; set; } = string.Empty;

        public DateOnly ScheduledDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }

        public StudySessionStatus Status { get; set; } = StudySessionStatus.Planned;
        public string? Notes { get; set; }

        /// <summary>True when suggested by the AI study planner rather than the student.</summary>
        public bool IsAiGenerated { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
