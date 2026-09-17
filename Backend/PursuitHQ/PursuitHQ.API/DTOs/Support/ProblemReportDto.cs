using System.ComponentModel.DataAnnotations;

namespace PursuitHQ.API.DTOs.Support
{
    /// <summary>What a student is telling us went wrong.</summary>
    public class ProblemReportDto
    {
        /// <summary>
        /// A short label so a full inbox can be triaged at a glance. Free text
        /// rather than an enum: a fixed list of categories is always missing the
        /// one somebody needs, and they pick the nearest wrong option instead.
        /// </summary>
        [Required]
        [StringLength(120, MinimumLength = 3)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [StringLength(4000, MinimumLength = 10)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// The page they were on, filled in by the browser rather than typed.
        /// The single most useful thing in a bug report and the thing people
        /// most reliably leave out.
        /// </summary>
        [StringLength(300)]
        public string? PageUrl { get; set; }
    }

    public class ProblemReportResponseDto
    {
        public string Message { get; set; } = string.Empty;
    }
}
