using System.ComponentModel.DataAnnotations;

namespace PursuitHQ.API.Models
{
    public enum ReportReason
    {
        Harassment = 0,
        Spam = 1,
        Impersonation = 2,
        InappropriateContent = 3,

        /// <summary>
        /// Somebody may be in danger - themselves or someone else.
        ///
        /// Separated from the rest because it is the one reason where the right
        /// response is not "review this within a few days".
        /// </summary>
        SafetyConcern = 4,

        Other = 5
    }

    public enum ReportStatus
    {
        Open = 0,
        Reviewed = 1,
        ActionTaken = 2,
        Dismissed = 3
    }

    /// <summary>
    /// One student telling the operator that another student's behaviour needs
    /// looking at.
    ///
    /// Blocking already handles "leave me alone". This is the other half:
    /// "somebody should look at this". A messaging platform with no way to
    /// raise that leaves a student being harassed with nowhere to go, and
    /// leaves the operator with no record that they were ever told.
    ///
    /// Deliberately has no foreign keys or navigation properties, only ids.
    /// A safety record should not be deleted by a cascade when one of the two
    /// accounts is removed - and equally, it must not be the thing that blocks
    /// somebody from deleting their account. Ids alone satisfy both. If an
    /// account is gone the report is usually moot anyway, which is also why no
    /// copy of anyone's name is kept here.
    /// </summary>
    public class Report
    {
        public int Id { get; set; }

        public string ReporterId { get; set; } = string.Empty;
        public string ReportedUserId { get; set; } = string.Empty;

        /// <summary>Set when the report is about a specific message.</summary>
        public int? MessageId { get; set; }
        public int? ConversationId { get; set; }

        /// <summary>
        /// The message text as it read when it was reported.
        ///
        /// Captured on purpose: a message can be edited or deleted seconds
        /// after being sent, and a report that says "look at message 4821"
        /// about a message that now says nothing is a report nobody can act on.
        /// This is the only place PursuitHQ copies message content, and it only
        /// happens when a student explicitly asks for it to be looked at.
        /// </summary>
        [MaxLength(4000)]
        public string? MessageSnapshot { get; set; }

        public ReportReason Reason { get; set; }

        [MaxLength(2000)]
        public string? Details { get; set; }

        public ReportStatus Status { get; set; } = ReportStatus.Open;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReviewedAt { get; set; }

        [MaxLength(2000)]
        public string? ReviewNotes { get; set; }
    }
}
