namespace PursuitHQ.API.Models
{
    /// <summary>An internship or job the student has applied to or saved.</summary>
    public class JobApplication
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public string Company { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;

        public JobType Type { get; set; } = JobType.Internship;
        public ApplicationStatus Status { get; set; } = ApplicationStatus.Saved;

        public DateTime? AppliedDate { get; set; }
        public string? Notes { get; set; }

        /// <summary>Whether this was entered by hand or saved from job search.</summary>
        public ApplicationSource Source { get; set; } = ApplicationSource.Manual;

        /// <summary>Id from the job-search provider, when sourced from search.</summary>
        public string? ExternalJobId { get; set; }

        /// <summary>Link to the original posting. The "Apply" button opens this.</summary>
        public string? SourceUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
