namespace PursuitHQ.API.Models
{
    /// <summary>
    /// A record of an email that was sent. Prevents duplicate reminders and
    /// gives the student a history. Bodies are deliberately not stored.
    /// </summary>
    public class Notification
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public NotificationType Type { get; set; }

        /// <summary>e.g. "Assignment". With RelatedEntityId, prevents sending the same reminder twice.</summary>
        public string? RelatedEntityType { get; set; }
        public int? RelatedEntityId { get; set; }

        public string Subject { get; set; } = string.Empty;

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public DeliveryStatus Status { get; set; } = DeliveryStatus.Sent;
        public string? ErrorMessage { get; set; }
    }
}
