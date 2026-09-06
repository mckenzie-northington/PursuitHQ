namespace PursuitHQ.API.Models
{
    /// <summary>A certification the student has earned.</summary>
    public class Certification
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? Issuer { get; set; }

        public DateTime DateEarned { get; set; }
        public DateTime? ExpiresOn { get; set; }

        public string? CredentialUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
