using System.ComponentModel.DataAnnotations;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.DTOs.Assignments
{
    public class CreateAssignmentDto
    {
        [Required]
        public int CourseId { get; set; }

        [Required, MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        public AssignmentStatus Status { get; set; } = AssignmentStatus.NotStarted;
    }
}
