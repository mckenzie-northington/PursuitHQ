namespace PursuitHQ.API.Models
{
    /// <summary>
    /// One person's one emoji on one message.
    ///
    /// A row per person per emoji rather than a count, because the interesting
    /// question is not "how many thumbs up" but "did I already react, and who
    /// else did" - and a count cannot answer either. The unique index on
    /// (MessageId, UserId, Emoji) is what makes the toggle safe: double-clicking
    /// cannot leave two of the same reaction behind.
    /// </summary>
    public class MessageReaction
    {
        public int Id { get; set; }

        public int MessageId { get; set; }
        public Message? Message { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        /// <summary>
        /// The emoji itself, not a name.
        ///
        /// Short, but not one character: a lot of emoji are several code points
        /// once skin tones and joiners are involved, and storing a name would
        /// mean maintaining a lookup table forever.
        /// </summary>
        public string Emoji { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// A file or image sent with a message.
    ///
    /// Its own table rather than columns on Message, because one message can
    /// carry several and because a message with no attachment - which is nearly
    /// all of them - should not pay for six unused columns.
    /// </summary>
    public class MessageAttachment
    {
        public int Id { get; set; }

        public int MessageId { get; set; }
        public Message? Message { get; set; }

        /// <summary>Storage key. Never sent to a client.</summary>
        public string StoragePath { get; set; } = string.Empty;

        /// <summary>
        /// What the sender called it, shown in the UI.
        ///
        /// Stored separately from the storage key on purpose: the key is a GUID
        /// so that nothing about the file system can be guessed from a name, and
        /// the name is only ever displayed, never used to open anything.
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;

        public long SizeBytes { get; set; }

        /// <summary>
        /// Whether this is safe to render in an img tag.
        ///
        /// Only set by the server, and only for files it decoded and re-encoded
        /// itself. A content type from an upload is a claim, not a fact, and
        /// rendering an attacker's claim inline is how a chat becomes an XSS
        /// hole.
        /// </summary>
        public bool IsImage { get; set; }
    }
}
