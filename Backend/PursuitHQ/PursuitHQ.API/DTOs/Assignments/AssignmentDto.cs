using PursuitHQ.API.Models;

namespace PursuitHQ.API.DTOs.Assignments
{
    public class AssignmentDto
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime DueDate { get; set; }
        public AssignmentStatus Status { get; set; }
        public string? Grade { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>True when past due and not yet completed.</summary>
        public bool IsOverdue { get; set; }
    }
}
