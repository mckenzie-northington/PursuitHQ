using System.ComponentModel.DataAnnotations;

namespace PursuitHQ.API.DTOs.Courses
{
    public class UpdateCourseDto
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Professor { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string Semester { get; set; } = string.Empty;

        /// <summary>First and last day the course meets. Optional.</summary>
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }

        [Range(0, 12)]
        public int? CreditHours { get; set; }

        [MaxLength(9)]
        public string? ColorHex { get; set; }
    }
}
