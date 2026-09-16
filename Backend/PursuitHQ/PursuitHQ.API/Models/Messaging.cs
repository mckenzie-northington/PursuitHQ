namespace PursuitHQ.API.Models
{
    /// <summary>
    /// A direct chat or a group chat. The only difference is IsGroup and how
    /// membership is allowed to change.
    /// </summary>
    public class Conversation
    {
        public int Id { get; set; }

        public bool IsGroup { get; set; }

        /// <summary>Groups only. A direct chat is named after the other person.</summary>
        public string? Name { get; set; }

        public string CreatedById { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Denormalised so the conversation list can be ordered and paged
        /// without touching the messages table.
        ///
        /// Worth the duplication: this is the single most-run query in the
        /// feature - it runs on every poll for the unread dot - and the
        /// alternative is a correlated MAX(SentAt) per conversation.
        /// </summary>
        public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

        public List<ConversationMember> Members { get; set; } = new();
    }

    /// <summary>
    /// One person's place in one conversation.
    ///
    /// Every read and write of a message checks for a row here. That check is
    /// the whole security model for messaging: unlike the rest of PursuitHQ,
    /// where a missing filter shows you an error, a missing membership check
    /// quietly shows you someone else's conversation.
    /// </summary>
    public class ConversationMember
    {
        public int Id { get; set; }

        public int ConversationId { get; set; }
        public Conversation? Conversation { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Everything after this is unread. Null means nothing has been read.
        /// </summary>
        public DateTime? LastReadAt { get; set; }

        /// <summary>Can rename the group and add or remove people.</summary>
        public bool IsAdmin { get; set; }

        /// <summary>
        /// Set instead of deleting the row when someone leaves.
        ///
        /// Their messages stay readable to everyone else - a group chat with
        /// half its sentences missing is worse than one that says who left -
        /// and it stops a removed member being silently re-added by an old
        /// client still holding the conversation id.
        /// </summary>
        public DateTime? LeftAt { get; set; }
    }

    public class Message
    {
        public int Id { get; set; }

        public int ConversationId { get; set; }
        public Conversation? Conversation { get; set; }

        public string SenderId { get; set; } = string.Empty;
        public ApplicationUser? Sender { get; set; }

        public string Body { get; set; } = string.Empty;

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Soft delete: the row stays, the body is cleared, and the UI shows
        /// "message deleted".
        ///
        /// Removing it outright leaves a hole in a conversation two people are
        /// reading at once, and makes "did they say that?" unanswerable.
        /// </summary>
        public DateTime? DeletedAt { get; set; }
    }
}
