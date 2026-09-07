using PursuitHQ.API.Models;

namespace PursuitHQ.API.DTOs.Applications
{
    public class JobApplicationDto
    {
        public int Id { get; set; }
        public string Company { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public JobType Type { get; set; }
        public ApplicationStatus Status { get; set; }
        public DateTime? AppliedDate { get; set; }
        public string? Notes { get; set; }
        public ApplicationSource Source { get; set; }
        public string? SourceUrl { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>Days since applying, so stale applications are easy to spot.</summary>
        public int? DaysSinceApplied { get; set; }
    }
}
