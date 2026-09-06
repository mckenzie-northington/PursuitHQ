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
        public DateTime CreatedAt { get; set; }
    }
}
