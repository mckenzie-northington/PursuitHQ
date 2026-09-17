using System.ComponentModel.DataAnnotations;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.DTOs.Students
{
    public class CreateReportDto
    {
        [Required]
        public string ReportedUserId { get; set; } = string.Empty;

        /// <summary>Set when reporting one message rather than the person generally.</summary>
        public int? MessageId { get; set; }

        [Required]
        public ReportReason Reason { get; set; }

        [MaxLength(2000)]
        public string? Details { get; set; }
    }

    /// <summary>
    /// What the reporter is told back. Never anything about the other account.
    /// </summary>
    public class ReportCreatedDto
    {
        public string Message { get; set; } = string.Empty;
    }
}
