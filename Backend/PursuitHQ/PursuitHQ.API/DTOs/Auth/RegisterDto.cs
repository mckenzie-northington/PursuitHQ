using System.ComponentModel.DataAnnotations;

namespace PursuitHQ.API.DTOs.Auth
{
    /// <summary>What the client sends to create an account.</summary>
    public class RegisterDto
    {
        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(8)]
        public string Password { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Major { get; set; }

        [Range(1900, 2100)]
        public int? GraduationYear { get; set; }

        /// <summary>IANA time zone id. Determines when reminder emails are sent.</summary>
        public string TimeZone { get; set; } = "America/New_York";
    }
}
