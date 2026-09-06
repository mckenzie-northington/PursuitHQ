namespace PursuitHQ.API.Models
{
    /// <summary>A piece of work due for a course.</summary>
    public class Assignment
    {
        public int Id { get; set; }

        public int CourseId { get; set; }
        public Course? Course { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        public DateTime DueDate { get; set; }

        public AssignmentStatus Status { get; set; } = AssignmentStatus.NotStarted;

        /// <summary>Recorded after grading, e.g. "94" or "A-". Free text on purpose.</summary>
        public string? Grade { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
