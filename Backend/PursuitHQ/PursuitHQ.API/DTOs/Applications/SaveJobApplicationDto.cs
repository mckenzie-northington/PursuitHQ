using System.ComponentModel.DataAnnotations;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.DTOs.Applications
{
    /// <summary>Used for both creating and updating an application.</summary>
    public class SaveJobApplicationDto
    {
        [Required, MaxLength(200)]
        public string Company { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Role { get; set; } = string.Empty;

        public JobType Type { get; set; } = JobType.Internship;
        public ApplicationStatus Status { get; set; } = ApplicationStatus.Saved;

        public DateTime? AppliedDate { get; set; }

        [MaxLength(4000)]
        public string? Notes { get; set; }

        [MaxLength(1000)]
        public string? SourceUrl { get; set; }
    }
}
