namespace PursuitHQ.API.Models
{
    /// <summary>
    /// What someone may do in a group.
    ///
    /// Ordered so that a numeric comparison works: anything above Member can
    /// invite and remove, and only Owner can change roles. Do not renumber -
    /// these are stored.
    /// </summary>
    public enum ConversationRole
    {
        Member = 0,
        Admin = 1,

        /// <summary>
        /// Exactly one per group, and it cannot be removed by anybody.
        ///
        /// Having a single unremovable role is what stops a group reaching a
        /// state where nothing can be administered: two admins removing each
        /// other is a nuisance, a group with no one able to act is a dead room
        /// nobody can leave tidily.
        /// </summary>
        Owner = 2
    }

    /// <summary>Where someone stands with a conversation.</summary>
    public enum MembershipStatus
    {
        /// <summary>Asked, not answered. Sees the invitation, not the messages.</summary>
        Invited = 0,

        Active = 1,

        /// <summary>Left or was removed. The row stays so history still reads.</summary>
        Left = 2
    }

    /// <summary>
    /// Ordinary talk, or the app narrating a change to the group.
    /// </summary>
    public enum MessageKind
    {
        Text = 0,

        /// <summary>
        /// "Sarah added Marcus", "Marcus left".
        ///
        /// Stored as messages rather than derived, because they belong in the
        /// timeline in the order they happened. Without them people appear and
        /// vanish from a group with no explanation, which reads as a bug.
        /// </summary>
        System = 1
    }

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

        /// <summary>Groups only. One line about what the group is for.</summary>
        public string? Description { get; set; }

        /// <summary>
        /// Storage key for the group picture, never sent to a client.
        ///
        /// Goes through the same pipeline as a profile photo, which re-encodes
        /// it - so a group picture cannot carry the GPS coordinates of wherever
        /// it was taken into a room of people who were not there.
        /// </summary>
        public string? PhotoPath { get; set; }

        public string? PhotoContentType { get; set; }

        /// <summary>
        /// Who made it. Kept even after they hand ownership on or leave, because
        /// it is history rather than permission - the Owner role is what grants
        /// anything.
        /// </summary>
        public string CreatedById { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Denormalised so the conversation list can be ordered and paged
        /// without touching the messages table.
        ///
        /// Worth the duplication: this is the most-run query in the feature - it
        /// runs on every poll for the unread dot - and the alternative is a
        /// correlated MAX(SentAt) per conversation.
        /// </summary>
        public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

        public List<ConversationMember> Members { get; set; } = new();
    }

    /// <summary>
    /// One person's place in one conversation.
    ///
    /// Every read and write of a message checks for an Active row here. That
    /// check is the whole security model for messaging: unlike the rest of
    /// PursuitHQ, where a missing filter shows you an error, a missing
    /// membership check quietly shows you someone else's conversation.
    /// </summary>
    public class ConversationMember
    {
        public int Id { get; set; }

        public int ConversationId { get; set; }
        public Conversation? Conversation { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public ConversationRole Role { get; set; } = ConversationRole.Member;

        /// <summary>
        /// Invited, Active or Left - one row through the whole lifecycle.
        ///
        /// Folding invitations into membership rather than giving them a table
        /// of their own keeps the unique index on (ConversationId, UserId)
        /// meaningful, so somebody cannot hold an invitation and a membership at
        /// the same time and end up in a group twice.
        /// </summary>
        public MembershipStatus Status { get; set; } = MembershipStatus.Active;

        /// <summary>Who asked them in. Shown on the invitation.</summary>
        public string? InvitedById { get; set; }

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Everything after this is unread. Null means nothing read yet.</summary>
        public DateTime? LastReadAt { get; set; }

        /// <summary>
        /// Stops this conversation counting toward the unread dot.
        ///
        /// Per member, not per conversation: muting a busy group is one person's
        /// decision and must not quieten it for everybody else.
        /// </summary>
        public bool IsMuted { get; set; }

        /// <summary>
        /// Keeps this conversation at the top of the list.
        ///
        /// Per member, like muting: which conversations matter is one
        /// person's judgement, and pinning a group for everybody in it would
        /// be somebody else deciding what you look at first.
        /// </summary>
        public bool IsPinned { get; set; }

        /// <summary>
        /// When this chat was pinned. Null when it is not pinned.
        ///
        /// Pinned chats are then ordered by this, earliest first, so a new pin
        /// lands underneath the ones already there. Ordering them by their last
        /// message instead would let any pinned chat jump over the others the
        /// moment somebody typed in it, which makes a deliberately arranged
        /// list rearrange itself behind your back.
        /// </summary>
        public DateTime? PinnedAt { get; set; }

        /// <summary>
        /// Last time this person was seen typing here.
        ///
        /// A timestamp rather than a flag, so it expires on its own. A boolean
        /// would stay true forever the moment somebody closed the tab mid-word,
        /// and every reader would be told they were still typing.
        /// </summary>
        public DateTime? LastTypingAt { get; set; }

        /// <summary>
        /// When this member was last emailed about a message here.
        ///
        /// The whole throttle. A chat is a back-and-forth, and without a record
        /// of the last one a five minute conversation would put thirty emails
        /// in somebody's inbox. Stamped whenever they qualified for an email,
        /// sent or not, so the same rows are not re-examined on every message.
        /// </summary>
        public DateTime? LastMessageEmailAt { get; set; }

        /// <summary>
        /// When they left or were removed. Status is what the code checks; this
        /// is for showing "left on the 3rd" and for ordering rejoins.
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

        public MessageKind Kind { get; set; } = MessageKind.Text;

        /// <summary>The message this one answers, if any.</summary>
        public int? ReplyToMessageId { get; set; }
        public Message? ReplyToMessage { get; set; }

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Set when the text has been changed since sending.
        ///
        /// Shown in the UI. An edit that leaves no trace is a way to rewrite what
        /// somebody remembers being said.
        /// </summary>
        public DateTime? EditedAt { get; set; }

        /// <summary>
        /// Soft delete: the row stays, the body is cleared, the UI says so.
        ///
        /// Removing it outright leaves a hole in a conversation two people are
        /// reading at once, and makes "did they say that?" unanswerable.
        /// </summary>
        public DateTime? DeletedAt { get; set; }

        public List<MessageReaction> Reactions { get; set; } = new();

        public List<MessageAttachment> Attachments { get; set; } = new();
    }
}
