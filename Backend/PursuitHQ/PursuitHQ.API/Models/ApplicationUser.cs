using Microsoft.AspNetCore.Identity;

namespace PursuitHQ.API.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Major { get; set; }
        public int? GraduationYear { get; set; }
        public string TimeZone { get; set; } = "America/New_York";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}