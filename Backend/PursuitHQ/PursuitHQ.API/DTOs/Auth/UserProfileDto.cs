using PursuitHQ.API.Models;

namespace PursuitHQ.API.DTOs.Auth
{
    /// <summary>
    /// The safe, public shape of a user. Note what is absent: PasswordHash,
    /// SecurityStamp, lockout fields. Entities never leave the API directly.
    /// </summary>
    public class UserProfileDto
    {
        public string Id { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Major { get; set; }
        public int? GraduationYear { get; set; }
        public string TimeZone { get; set; } = string.Empty;
        public string? School { get; set; }
        public EducationLevel EducationLevel { get; set; }
        public bool IsDiscoverable { get; set; }

        /// <summary>
        /// Whether there is a photo to fetch. The storage key itself never
        /// leaves the server.
        /// </summary>
        public bool HasPhoto { get; set; }

        /// <summary>
        /// False until the walkthrough has been finished or skipped. The website
        /// reads this to decide whether to start it.
        /// </summary>
        public bool HasSeenTour { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
