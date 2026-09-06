using System.ComponentModel.DataAnnotations;

namespace PursuitHQ.API.DTOs.Auth
{
    public class UpdateProfileDto
    {
        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Major { get; set; }

        [Range(1900, 2100)]
        public int? GraduationYear { get; set; }

        public string TimeZone { get; set; } = "America/New_York";
    }
}
