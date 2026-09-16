namespace PursuitHQ.API.Models
{
    public enum ConnectionStatus
    {
        Pending = 0,
        Accepted = 1,
        Declined = 2,

        /// <summary>
        /// One side has blocked the other. Kept as a status on the same row
        /// rather than a list of its own so that "what is the state between
        /// these two people" is always one lookup with one answer - two places
        /// to check is how a blocked person ends up still able to message.
        /// </summary>
        Blocked = 3
    }

    /// <summary>
    /// The relationship between two students.
    ///
    /// This row is what unlocks everything social in the app: seeing a full
    /// profile, starting a chat, being added to a group. Nothing checks
    /// "are they a student" and stops there - it checks this.
    ///
    /// Direction matters while a request is pending (someone asked, someone
    /// else has to answer) and stops mattering once it is accepted, so every
    /// lookup has to consider the pair in both orders. ConnectionService is the
    /// only place that should be doing that.
    /// </summary>
    public class Connection
    {
        public int Id { get; set; }

        /// <summary>Who sent the request.</summary>
        public string RequesterId { get; set; } = string.Empty;
        public ApplicationUser? Requester { get; set; }

        /// <summary>Who has to answer it.</summary>
        public string AddresseeId { get; set; } = string.Empty;
        public ApplicationUser? Addressee { get; set; }

        public ConnectionStatus Status { get; set; } = ConnectionStatus.Pending;

        /// <summary>
        /// Who pressed block, which is not always the requester.
        ///
        /// Needed because blocking is one-sided: the blocked person should see
        /// nothing unusual, while the blocker keeps the ability to undo it.
        /// </summary>
        public string? BlockedById { get; set; }

        /// <summary>
        /// A short note sent with the request - "we met in CS 201".
        ///
        /// The one piece of text a stranger can put in front of someone who has
        /// not accepted them, which is why it is short and why it is the only
        /// one.
        /// </summary>
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? RespondedAt { get; set; }
    }
}
