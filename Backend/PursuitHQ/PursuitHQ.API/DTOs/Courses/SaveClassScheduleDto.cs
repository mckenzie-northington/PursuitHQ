using System.ComponentModel.DataAnnotations;

namespace PursuitHQ.API.DTOs.Courses
{
    /// <summary>Used for both creating and updating a meeting time.</summary>
    public class SaveClassScheduleDto
    {
        [Required]
        public DayOfWeek DayOfWeek { get; set; }

        [Required]
        public TimeOnly StartTime { get; set; }

        [Required]
        public TimeOnly EndTime { get; set; }

        [MaxLength(200)]
        public string? Location { get; set; }
    }
}
