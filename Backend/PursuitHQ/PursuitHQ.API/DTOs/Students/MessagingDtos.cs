using System.ComponentModel.DataAnnotations;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.DTOs.Students
{
    /// <summary>One person in a group, with what they are allowed to do.</summary>
    public class ConversationMemberDto
    {
        public StudentCardDto Student { get; set; } = new();
        public ConversationRole Role { get; set; }
        public DateTime JoinedAt { get; set; }

        /// <summary>Set on a pending invitation, so the list can say who asked them.</summary>
        public string? InvitedByName { get; set; }
    }

    public class ConversationSummaryDto
    {
        public int Id { get; set; }
        public bool IsGroup { get; set; }

        /// <summary>The group's name, or the other person's name in a direct chat.</summary>
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }
        public bool HasPhoto { get; set; }

        /// <summary>Everyone active, not counting the viewer.</summary>
        public List<StudentCardDto> Members { get; set; } = new();

        public string? LastMessage { get; set; }
        public string? LastMessageSender { get; set; }
        public DateTime LastMessageAt { get; set; }

        public int UnreadCount { get; set; }
        public bool IsMuted { get; set; }
        public bool IsPinned { get; set; }

        /// <summary>Drives the order of the pinned block. Null when unpinned.</summary>
        public DateTime? PinnedAt { get; set; }

        /// <summary>The viewer's own role, which decides what the UI offers.</summary>
        public ConversationRole MyRole { get; set; }
    }

    /// <summary>A group someone has been asked to join but has not answered.</summary>
    public class GroupInvitationDto
    {
        public int ConversationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool HasPhoto { get; set; }
        public string InvitedByName { get; set; } = string.Empty;
        public int MemberCount { get; set; }
        public DateTime InvitedAt { get; set; }
    }

    /// <summary>
    /// One emoji on a message, already tallied.
    ///
    /// Counted on the server rather than shipping every row, because the
    /// client only ever needs the total and whether it is one of them.
    /// </summary>
    public class MessageReactionDto
    {
        public string Emoji { get; set; } = string.Empty;
        public int Count { get; set; }

        /// <summary>Drives the highlight, and makes the tap a toggle.</summary>
        public bool Mine { get; set; }
    }

    public class MessageAttachmentDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }

        /// <summary>
        /// Safe to put in an img tag. Only ever true for a file the server
        /// decoded and re-encoded itself.
        /// </summary>
        public bool IsImage { get; set; }
    }

    /// <summary>One other person in the conversation, right now.</summary>
    public class PresencePersonDto
    {
        public string Id { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;

        /// <summary>
        /// How far they have read. The client compares this against each
        /// message's SentAt rather than being told per message, which keeps this
        /// one small response the same size whatever the history looks like.
        /// </summary>
        public DateTime? LastReadAt { get; set; }
    }

    /// <summary>
    /// Who is typing and who has caught up - the two things that change far
    /// faster than the messages themselves, so they are fetched on their own.
    /// </summary>
    public class ConversationPresenceDto
    {
        public List<PresencePersonDto> Typing { get; set; } = new();
        public List<PresencePersonDto> Readers { get; set; } = new();
    }

    public class ReactDto
    {
        [Required]
        [MaxLength(20)]
        public string Emoji { get; set; } = string.Empty;
    }

    public class MessageDto
    {
        public int Id { get; set; }
        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public MessageKind Kind { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsEdited { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsMine { get; set; }

        /// <summary>A short preview of what this answers, when it answers something.</summary>
        public int? ReplyToId { get; set; }
        public string? ReplyToSender { get; set; }
        public string? ReplyToBody { get; set; }

        public List<MessageReactionDto> Reactions { get; set; } = new();
        public List<MessageAttachmentDto> Attachments { get; set; } = new();
    }

    public class SendMessageDto
    {
        [Required(ErrorMessage = "Type something first.")]
        [MaxLength(4000, ErrorMessage = "That message is too long.")]
        public string Body { get; set; } = string.Empty;

        public int? ReplyToMessageId { get; set; }
    }

    public class EditMessageDto
    {
        [Required(ErrorMessage = "A message cannot be empty.")]
        [MaxLength(4000)]
        public string Body { get; set; } = string.Empty;
    }

    public class StartDirectChatDto
    {
        [Required]
        public string UserId { get; set; } = string.Empty;
    }

    public class CreateGroupDto
    {
        [Required(ErrorMessage = "Give the group a name.")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Description { get; set; }

        /// <summary>
        /// Who to invite. They are invited, not added: being put into a room
        /// without being asked is how group chats become something people resent.
        /// </summary>
        public List<string> MemberIds { get; set; } = new();
    }

    public class InviteMembersDto
    {
        public List<string> MemberIds { get; set; } = new();
    }

    public class UpdateGroupDto
    {
        [Required(ErrorMessage = "Give the group a name.")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Description { get; set; }
    }

    public class ChangeRoleDto
    {
        /// <summary>
        /// Setting somebody to Owner hands the group over, and demotes the
        /// current owner to Admin in the same move - there is only ever one.
        /// </summary>
        public ConversationRole Role { get; set; }
    }

    public class MuteDto
    {
        public bool Muted { get; set; }
    }

    public class PinDto
    {
        public bool Pinned { get; set; }
    }

    /// <summary>One message that matched a search, with enough to find it again.</summary>
    public class MessageSearchHitDto
    {
        public int ConversationId { get; set; }
        public string ConversationTitle { get; set; } = string.Empty;
        public bool IsGroup { get; set; }

        public int MessageId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
    }

    /// <summary>Drives the dot on the messages icon.</summary>
    public class UnreadDto
    {
        public int Total { get; set; }
        public int Conversations { get; set; }
        public int ConnectionRequests { get; set; }
        public int GroupInvitations { get; set; }
    }
}
