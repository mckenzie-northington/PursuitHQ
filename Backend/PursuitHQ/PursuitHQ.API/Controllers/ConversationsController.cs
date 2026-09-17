using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.Students;
using PursuitHQ.API.Models;
using PursuitHQ.API.Services;

namespace PursuitHQ.API.Controllers
{
    /// <summary>
    /// Direct and group chats.
    ///
    /// Every endpoint begins by proving the caller is an *active* member. That
    /// check is the entire security model for messaging, and it is different in
    /// kind from the rest of PursuitHQ: elsewhere a missing filter shows you an
    /// error page, here it shows you two other people's conversation. There is
    /// one helper, <see cref="MemberAsync"/>, and nothing reads or writes a
    /// message without going through it.
    ///
    /// Groups have one owner and any number of admins. The owner cannot be
    /// removed and is the only one who can change roles, which is what stops a
    /// group reaching a state nobody can administer.
    /// </summary>
    [Route("api/conversations")]
    public class ConversationsController : ApiControllerBase
    {
        private const int PageSize = 50;
        private const int MaxGroupMembers = 100;
        private const int MaxAttachmentBytes = 15 * 1024 * 1024;

        /// <summary>
        /// How long after their last keystroke somebody still counts as typing.
        ///
        /// Comfortably longer than the client's ping interval, so a steady
        /// typist never flickers, and short enough that somebody who walks away
        /// mid-sentence stops being announced.
        /// </summary>
        private const int TypingWindowSeconds = 6;

        /// <summary>Per message. The frontend copy of this is MAX_ATTACHMENTS.</summary>
        private const int MaxAttachmentsPerMessage = 10;

        /// <summary>
        /// The whole upload. Ten files at the per-file limit would be 150MB,
        /// which nobody needs; this is the point at which the request is
        /// refused by Kestrel before it is read into the process at all.
        /// </summary>
        private const int MaxUploadBytes = 60 * 1024 * 1024;

        /// <summary>Re-encoded on the way in, so these can render inline.</summary>
        private static readonly string[] ImageExtensions =
            { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

        /// <summary>Stored as sent, and always served as a download.</summary>
        private static readonly string[] FileExtensions =
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".txt", ".md", ".csv", ".zip"
        };

        private readonly ApplicationDbContext _db;
        private readonly IConnectionService _connections;
        private readonly IFileStorageService _storage;

        public ConversationsController(
            ApplicationDbContext db,
            IConnectionService connections,
            IFileStorageService storage)
        {
            _db = db;
            _connections = connections;
            _storage = storage;
        }

        // ------------------------------------------------------------- listing

        [HttpGet]
        public async Task<ActionResult<List<ConversationSummaryDto>>> GetAll(CancellationToken ct)
        {
            var mine = await _db.ConversationMembers
                .Where(m => m.UserId == CurrentUserId && m.Status == MembershipStatus.Active)
                .Select(m => new
                {
                    m.ConversationId,
                    m.LastReadAt,
                    m.Role,
                    m.IsMuted,
                    m.IsPinned,
                    m.PinnedAt
                })
                .ToListAsync(ct);

            var ids = mine.Select(m => m.ConversationId).ToList();

            var conversations = await _db.Conversations
                .Where(c => ids.Contains(c.Id))
                .Include(c => c.Members.Where(m => m.Status == MembershipStatus.Active))
                    .ThenInclude(m => m.User)
                .OrderByDescending(c => c.LastMessageAt)
                .ToListAsync(ct);

            // EF cannot put an Include after a grouping projection, so the newest
            // message ids come back first and the rows are fetched by id.
            var lastIds = await _db.Messages
                .Where(m => ids.Contains(m.ConversationId))
                .GroupBy(m => m.ConversationId)
                .Select(g => g.Max(m => m.Id))
                .ToListAsync(ct);

            var latest = await _db.Messages
                .Include(m => m.Sender)
                .Where(m => lastIds.Contains(m.Id))
                .ToListAsync(ct);

            var unread = await UnreadPerConversationAsync(includeMuted: true, ct);

            var summaries = new List<ConversationSummaryDto>();

            foreach (var conversation in conversations)
            {
                var membership = mine.First(m => m.ConversationId == conversation.Id);
                var last = latest.FirstOrDefault(m => m.ConversationId == conversation.Id);

                var others = conversation.Members
                    .Where(m => m.UserId != CurrentUserId && m.User is not null)
                    .Select(m => StudentCardMapper.ToCard(
                        m.User!, ProfileVisibility.Card, null, CurrentUserId))
                    .ToList();

                summaries.Add(new ConversationSummaryDto
                {
                    Id = conversation.Id,
                    IsGroup = conversation.IsGroup,
                    Title = Title(conversation, others),
                    Description = conversation.Description,
                    HasPhoto = !string.IsNullOrEmpty(conversation.PhotoPath),
                    Members = others,
                    LastMessage = Preview(last),
                    LastMessageSender = last?.Kind == MessageKind.System
                        ? null
                        : last is not null && last.SenderId is null
                            ? "Deleted account"
                            : last?.Sender?.FirstName,
                    LastMessageAt = conversation.LastMessageAt,
                    IsMuted = membership.IsMuted,
                    IsPinned = membership.IsPinned,
                    PinnedAt = membership.PinnedAt,
                    MyRole = membership.Role,
                    UnreadCount = unread
                        .FirstOrDefault(u => u.ConversationId == conversation.Id)?.Count ?? 0
                });
            }

            // Pinned first, then most recent. Sorted here rather than in SQL
            // because the pin lives on the membership and the timestamp on the
            // conversation, and the list is short enough that one ordering pass
            // costs nothing.
            //
            // Within the pinned block the order is when they were pinned,
            // earliest first, so a new pin goes underneath the existing ones and
            // an arrangement somebody made on purpose stays put. Anything pinned
            // before that was recorded sorts to the top of the block.
            return Ok(summaries
                .OrderByDescending(s => s.IsPinned)
                .ThenBy(s => s.PinnedAt ?? DateTime.MinValue)
                .ThenByDescending(s => s.LastMessageAt)
                .ToList());
        }

        /// <summary>Drives the dot on the messages icon.</summary>
        [HttpGet("unread")]
        public async Task<ActionResult<UnreadDto>> Unread(CancellationToken ct)
        {
            var counts = await UnreadPerConversationAsync(includeMuted: false, ct);

            return Ok(new UnreadDto
            {
                Total = counts.Sum(c => c.Count),
                Conversations = counts.Count(c => c.Count > 0),

                // Requests and invitations share the dot. To the person looking
                // at it they are the same thing: somebody is waiting on you.
                ConnectionRequests = await _db.Connections.CountAsync(
                    c => c.AddresseeId == CurrentUserId && c.Status == ConnectionStatus.Pending, ct),

                GroupInvitations = await _db.ConversationMembers.CountAsync(
                    m => m.UserId == CurrentUserId && m.Status == MembershipStatus.Invited, ct)
            });
        }

        // --------------------------------------------------------- invitations

        [HttpGet("invitations")]
        public async Task<ActionResult<List<GroupInvitationDto>>> GetInvitations(CancellationToken ct)
        {
            var rows = await _db.ConversationMembers
                .Where(m => m.UserId == CurrentUserId && m.Status == MembershipStatus.Invited)
                .Include(m => m.Conversation)
                .OrderByDescending(m => m.JoinedAt)
                .ToListAsync(ct);

            var inviterIds = rows.Select(r => r.InvitedById).Where(id => id != null).ToList();

            var inviters = await _db.Users
                .Where(u => inviterIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName })
                .ToListAsync(ct);

            var invitations = new List<GroupInvitationDto>();

            foreach (var row in rows.Where(r => r.Conversation is not null))
            {
                var inviter = inviters.FirstOrDefault(u => u.Id == row.InvitedById);

                invitations.Add(new GroupInvitationDto
                {
                    ConversationId = row.ConversationId,
                    Name = row.Conversation!.Name ?? "Group",
                    Description = row.Conversation.Description,
                    HasPhoto = !string.IsNullOrEmpty(row.Conversation.PhotoPath),
                    InvitedByName = inviter is null
                        ? "Someone"
                        : $"{inviter.FirstName} {inviter.LastName}".Trim(),
                    MemberCount = await _db.ConversationMembers.CountAsync(
                        m => m.ConversationId == row.ConversationId
                             && m.Status == MembershipStatus.Active, ct),
                    InvitedAt = row.JoinedAt
                });
            }

            return Ok(invitations);
        }

        [HttpPost("{id:int}/invitations/accept")]
        public async Task<IActionResult> AcceptInvitation(int id, CancellationToken ct)
        {
            var invitation = await _db.ConversationMembers.FirstOrDefaultAsync(
                m => m.ConversationId == id && m.UserId == CurrentUserId
                     && m.Status == MembershipStatus.Invited, ct);

            if (invitation is null) return NotFound(NotFoundError());

            invitation.Status = MembershipStatus.Active;
            invitation.JoinedAt = DateTime.UtcNow;
            invitation.LastReadAt = DateTime.UtcNow;

            await SystemMessageAsync(id, $"{await NameOfAsync(CurrentUserId, ct)} joined", ct);
            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        [HttpPost("{id:int}/invitations/decline")]
        public async Task<IActionResult> DeclineInvitation(int id, CancellationToken ct)
        {
            var invitation = await _db.ConversationMembers.FirstOrDefaultAsync(
                m => m.ConversationId == id && m.UserId == CurrentUserId
                     && m.Status == MembershipStatus.Invited, ct);

            if (invitation is null) return NotFound(NotFoundError());

            // Removed rather than marked Left. Declining is not a membership that
            // ended, and leaving the row would make a later re-invitation look
            // like a rejoin in the group's history.
            _db.ConversationMembers.Remove(invitation);
            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        // -------------------------------------------------------------- direct

        [HttpPost("direct")]
        public async Task<ActionResult<ConversationSummaryDto>> StartDirect(
            StartDirectChatDto dto, CancellationToken ct)
        {
            if (dto.UserId == CurrentUserId)
            {
                return BadRequest(new ApiErrorDto(
                    "InvalidRequest", "You cannot message yourself."));
            }

            // The rule that makes connection requests mean something. Checked
            // here rather than on the client, because the client is not what is
            // being defended against.
            if (!await _connections.AreConnectedAsync(CurrentUserId, dto.UserId, ct))
            {
                return StatusCode(403, new ApiErrorDto(
                    "NotConnected",
                    "You can only message students you are connected with."));
            }

            var existing = await _db.Conversations
                .Where(c => !c.IsGroup
                            && c.Members.Any(m => m.UserId == CurrentUserId)
                            && c.Members.Any(m => m.UserId == dto.UserId))
                .Include(c => c.Members).ThenInclude(m => m.User)
                .FirstOrDefaultAsync(ct);

            if (existing is not null)
            {
                // Re-opening a conversation you removed from your list puts it
                // back, rather than failing on the next send because the
                // membership still says you left.
                var mine = existing.Members.First(m => m.UserId == CurrentUserId);

                if (mine.Status != MembershipStatus.Active)
                {
                    mine.Status = MembershipStatus.Active;
                    mine.LeftAt = null;
                    mine.LastReadAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync(ct);
                }

                return Ok(await SummaryAsync(existing, ct));
            }

            var conversation = new Conversation
            {
                IsGroup = false,
                CreatedById = CurrentUserId,
                CreatedAt = DateTime.UtcNow,
                LastMessageAt = DateTime.UtcNow,
                Members =
                {
                    new ConversationMember { UserId = CurrentUserId },
                    new ConversationMember { UserId = dto.UserId }
                }
            };

            _db.Conversations.Add(conversation);
            await _db.SaveChangesAsync(ct);

            return Ok(await SummaryAsync(conversation, ct));
        }

        // -------------------------------------------------------------- groups

        [HttpPost("group")]
        public async Task<ActionResult<ConversationSummaryDto>> CreateGroup(
            CreateGroupDto dto, CancellationToken ct)
        {
            var wanted = dto.MemberIds.Distinct().Where(id => id != CurrentUserId).ToList();

            if (wanted.Count == 0)
            {
                return BadRequest(new ApiErrorDto("NoMembers", "Invite at least one person."));
            }

            if (wanted.Count + 1 > MaxGroupMembers)
            {
                return BadRequest(new ApiErrorDto(
                    "TooManyMembers", $"A group can hold {MaxGroupMembers} people."));
            }

            if (!await AllConnectedAsync(wanted, ct))
            {
                return StatusCode(403, new ApiErrorDto(
                    "NotConnected", "You can only invite students you are connected with."));
            }

            var conversation = new Conversation
            {
                IsGroup = true,
                Name = dto.Name.Trim(),
                Description = Clip(dto.Description, 300),
                CreatedById = CurrentUserId,
                CreatedAt = DateTime.UtcNow,
                LastMessageAt = DateTime.UtcNow,
                Members =
                {
                    new ConversationMember
                    {
                        UserId = CurrentUserId,
                        Role = ConversationRole.Owner,
                        Status = MembershipStatus.Active,
                        LastReadAt = DateTime.UtcNow
                    }
                }
            };

            // Invited, not added. Being dropped into a room without being asked
            // is how group chats become something people resent.
            foreach (var userId in wanted)
            {
                conversation.Members.Add(new ConversationMember
                {
                    UserId = userId,
                    Status = MembershipStatus.Invited,
                    InvitedById = CurrentUserId
                });
            }

            _db.Conversations.Add(conversation);
            await _db.SaveChangesAsync(ct);

            return Ok(await SummaryAsync(conversation, ct));
        }

        [HttpGet("{id:int}/members")]
        public async Task<ActionResult<List<ConversationMemberDto>>> GetMembers(
            int id, CancellationToken ct)
        {
            if (await MemberAsync(id, ct) is null) return NotFound(NotFoundError());

            var rows = await _db.ConversationMembers
                .Where(m => m.ConversationId == id && m.Status != MembershipStatus.Left)
                .Include(m => m.User)
                .ToListAsync(ct);

            var inviterIds = rows.Select(r => r.InvitedById).Where(x => x != null).ToList();
            var inviters = await _db.Users
                .Where(u => inviterIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName })
                .ToListAsync(ct);

            var members = rows
                .Where(m => m.User is not null)
                .Select(m => new ConversationMemberDto
                {
                    Student = StudentCardMapper.ToCard(
                        m.User!, ProfileVisibility.Card, null, CurrentUserId),
                    Role = m.Role,
                    JoinedAt = m.JoinedAt,

                    // Only set while the invitation is outstanding, which is what
                    // tells the list to show them as pending rather than present.
                    InvitedByName = m.Status == MembershipStatus.Invited
                        ? inviters.FirstOrDefault(u => u.Id == m.InvitedById)?.FirstName ?? "Someone"
                        : null
                })
                .OrderByDescending(m => m.Role)
                .ThenBy(m => m.Student.LastName)
                .ToList();

            return Ok(members);
        }

        [HttpPost("{id:int}/invitations")]
        public async Task<IActionResult> Invite(
            int id,
            InviteMembersDto dto,
            [FromServices] IRequestNotifier notifier,
            CancellationToken ct)
        {
            var membership = await MemberAsync(id, ct);
            if (membership is null || membership.Role < ConversationRole.Admin)
            {
                return NotFound(NotFoundError());
            }

            var conversation = await _db.Conversations
                .Include(c => c.Members)
                .FirstOrDefaultAsync(c => c.Id == id && c.IsGroup, ct);

            if (conversation is null) return NotFound(NotFoundError());

            var wanted = dto.MemberIds.Distinct().ToList();

            // Every one of them, not most. A group is otherwise a way to put a
            // message in front of somebody who never accepted you.
            if (!await AllConnectedAsync(wanted, ct))
            {
                return StatusCode(403, new ApiErrorDto(
                    "NotConnected", "You can only invite students you are connected with."));
            }

            // Only the people actually newly invited are emailed. Somebody
            // already in the group, or already holding an invitation, is skipped
            // by the branches below and must not be told again.
            var invited = new List<string>();

            foreach (var userId in wanted)
            {
                var existing = conversation.Members.FirstOrDefault(m => m.UserId == userId);

                if (existing is null)
                {
                    if (conversation.Members.Count(m => m.Status == MembershipStatus.Active)
                        >= MaxGroupMembers)
                    {
                        return BadRequest(new ApiErrorDto(
                            "TooManyMembers", $"A group can hold {MaxGroupMembers} people."));
                    }

                    conversation.Members.Add(new ConversationMember
                    {
                        UserId = userId,
                        Status = MembershipStatus.Invited,
                        InvitedById = CurrentUserId,
                        JoinedAt = DateTime.UtcNow
                    });

                    invited.Add(userId);
                }
                else if (existing.Status == MembershipStatus.Left)
                {
                    // Re-invited rather than re-added: somebody who left chooses
                    // again rather than being dragged back.
                    existing.Status = MembershipStatus.Invited;
                    existing.InvitedById = CurrentUserId;
                    existing.JoinedAt = DateTime.UtcNow;
                    existing.LeftAt = null;
                    existing.Role = ConversationRole.Member;

                    invited.Add(userId);
                }
            }

            await _db.SaveChangesAsync(ct);

            await notifier.GroupInvitedAsync(id, CurrentUserId, invited, ct);

            return NoContent();
        }

        /// <summary>Leave a group, or remove somebody from one.</summary>
        [HttpDelete("{id:int}/members/{userId}")]
        public async Task<IActionResult> RemoveMember(int id, string userId, CancellationToken ct)
        {
            var membership = await MemberAsync(id, ct);
            if (membership is null) return NotFound(NotFoundError());

            if (userId == "me") userId = CurrentUserId;

            var leaving = userId == CurrentUserId;

            var target = await _db.ConversationMembers
                .Include(m => m.User)
                .FirstOrDefaultAsync(
                    m => m.ConversationId == id && m.UserId == userId
                         && m.Status != MembershipStatus.Left, ct);

            if (target is null) return NotFound(NotFoundError());

            if (!leaving)
            {
                if (membership.Role < ConversationRole.Admin) return NotFound(NotFoundError());

                // The owner cannot be removed, and an admin cannot remove another
                // admin - otherwise two admins can fight over a group, and the
                // one who clicks first wins.
                if (target.Role == ConversationRole.Owner
                    || (target.Role == ConversationRole.Admin
                        && membership.Role != ConversationRole.Owner))
                {
                    return StatusCode(403, new ApiErrorDto(
                        "CannotRemove", "You cannot remove that person."));
                }
            }

            var name = target.User is null
                ? "Someone"
                : $"{target.User.FirstName} {target.User.LastName}".Trim();

            target.Status = MembershipStatus.Left;
            target.LeftAt = DateTime.UtcNow;

            // Cleared so that being re-invited later starts them as a plain
            // member rather than quietly restoring the rank they used to hold.
            target.Role = ConversationRole.Member;

            var conversation = await _db.Conversations
                .Include(c => c.Members)
                .FirstAsync(c => c.Id == id, ct);

            if (conversation.IsGroup)
            {
                await SystemMessageAsync(
                    id,
                    leaving ? $"{name} left" : $"{name} was removed",
                    ct);

                // Called every time rather than only when the owner left. It
                // checks for itself whether an owner is still present, and the
                // role above has already been cleared - so testing target.Role
                // here would look right and never be true.
                HandOverIfOwnerLeft(conversation, userId);
            }

            await _db.SaveChangesAsync(ct);

            // The last person out closes the room. An empty conversation is a row
            // nobody can ever see again.
            var remaining = conversation.Members
                .Count(m => m.Status != MembershipStatus.Left);

            if (remaining == 0)
            {
                _db.Conversations.Remove(conversation);
                await _db.SaveChangesAsync(ct);
            }

            return NoContent();
        }

        [HttpPut("{id:int}/members/{userId}/role")]
        public async Task<IActionResult> ChangeRole(
            int id, string userId, ChangeRoleDto dto, CancellationToken ct)
        {
            var membership = await MemberAsync(id, ct);

            // Only the owner. Letting admins promote each other means the role
            // has no teeth: anyone who reaches Admin can make themselves Owner.
            if (membership is null || membership.Role != ConversationRole.Owner)
            {
                return NotFound(NotFoundError());
            }

            if (userId == CurrentUserId)
            {
                return BadRequest(new ApiErrorDto(
                    "InvalidRequest", "Hand the group to someone else to step down."));
            }

            var target = await _db.ConversationMembers
                .Include(m => m.User)
                .FirstOrDefaultAsync(
                    m => m.ConversationId == id && m.UserId == userId
                         && m.Status == MembershipStatus.Active, ct);

            if (target is null) return NotFound(NotFoundError());

            var name = target.User?.FirstName ?? "They";

            if (dto.Role == ConversationRole.Owner)
            {
                // A handover, not a second owner. Both sides move in one step so
                // the group is never briefly ownerless or briefly double-owned.
                membership.Role = ConversationRole.Admin;
                target.Role = ConversationRole.Owner;

                await SystemMessageAsync(id, $"{name} is now the owner", ct);
            }
            else
            {
                target.Role = dto.Role;

                await SystemMessageAsync(
                    id,
                    dto.Role == ConversationRole.Admin
                        ? $"{name} is now an admin"
                        : $"{name} is no longer an admin",
                    ct);
            }

            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateGroup(
            int id, UpdateGroupDto dto, CancellationToken ct)
        {
            var membership = await MemberAsync(id, ct);
            if (membership is null || membership.Role < ConversationRole.Admin)
            {
                return NotFound(NotFoundError());
            }

            var conversation = await _db.Conversations.FirstOrDefaultAsync(
                c => c.Id == id && c.IsGroup, ct);

            if (conversation is null) return NotFound(NotFoundError());

            var name = dto.Name.Trim();

            if (conversation.Name != name)
            {
                await SystemMessageAsync(id, $"Group renamed to “{name}”", ct);
            }

            conversation.Name = name;
            conversation.Description = Clip(dto.Description, 300);

            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        /// <summary>
        /// The group picture, behind membership rather than in a public folder.
        /// </summary>
        [HttpGet("{id:int}/photo")]
        public async Task<IActionResult> GetPhoto(
            int id, [FromServices] IFileStorageService storage, CancellationToken ct)
        {
            // Invitees can see it too, so an invitation is not a blind decision -
            // hence the direct query rather than MemberAsync.
            var allowed = await _db.ConversationMembers.AnyAsync(
                m => m.ConversationId == id && m.UserId == CurrentUserId
                     && m.Status != MembershipStatus.Left, ct);

            if (!allowed) return NotFound();

            var conversation = await _db.Conversations
                .Where(c => c.Id == id)
                .Select(c => new { c.PhotoPath, c.PhotoContentType })
                .FirstOrDefaultAsync(ct);

            if (conversation?.PhotoPath is null) return NotFound();

            try
            {
                var stream = await storage.OpenAsync(conversation.PhotoPath, ct);
                Response.Headers.CacheControl = "private, max-age=300";

                return File(stream, conversation.PhotoContentType ?? "image/jpeg");
            }
            catch (FileNotFoundException)
            {
                // The row outlived the file. Not worth a 500 over an avatar.
                return NotFound();
            }
        }

        [HttpPost("{id:int}/photo")]
        [RequestSizeLimit(8 * 1024 * 1024)]
        public async Task<IActionResult> SetPhoto(
            int id,
            IFormFile file,
            [FromServices] IProfilePhotoService photos,
            [FromServices] IFileStorageService storage,
            CancellationToken ct)
        {
            var membership = await MemberAsync(id, ct);
            if (membership is null || membership.Role < ConversationRole.Admin)
            {
                return NotFound(NotFoundError());
            }

            if (file is null || file.Length == 0)
            {
                return BadRequest(new ApiErrorDto("NoFile", "Choose a photo first."));
            }

            var conversation = await _db.Conversations.FirstOrDefaultAsync(
                c => c.Id == id && c.IsGroup, ct);

            if (conversation is null) return NotFound(NotFoundError());

            var previous = conversation.PhotoPath;

            try
            {
                await using var upload = file.OpenReadStream();

                // Same pipeline as a profile photo, so a group picture cannot
                // carry the GPS coordinates of where it was taken into a room of
                // people who were not there.
                var stored = await photos.SaveAsync(upload, ct);

                conversation.PhotoPath = stored.Path;
                conversation.PhotoContentType = stored.ContentType;

                await _db.SaveChangesAsync(ct);
            }
            catch (InvalidImageException ex)
            {
                return BadRequest(new ApiErrorDto("InvalidImage", ex.Message));
            }

            if (!string.IsNullOrEmpty(previous))
            {
                try
                {
                    await storage.DeleteAsync(previous, ct);
                }
                catch
                {
                    // A leftover file is clutter; throwing here would undo a save
                    // that already succeeded.
                }
            }

            return NoContent();
        }

        [HttpPost("{id:int}/mute")]
        public async Task<IActionResult> Mute(int id, MuteDto dto, CancellationToken ct)
        {
            var membership = await MemberAsync(id, ct);
            if (membership is null) return NotFound(NotFoundError());

            membership.IsMuted = dto.Muted;
            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        // ------------------------------------------------------------ messages

        [HttpGet("{id:int}/messages")]
        public async Task<ActionResult<List<MessageDto>>> GetMessages(
            int id, [FromQuery] int? before, CancellationToken ct)
        {
            if (await MemberAsync(id, ct) is null) return NotFound(NotFoundError());

            var query = _db.Messages
                .Include(m => m.Sender)
                .Include(m => m.Reactions)
                .Include(m => m.Attachments)
                .Include(m => m.ReplyToMessage).ThenInclude(r => r!.Sender)
                .Where(m => m.ConversationId == id);

            // Paged by id rather than offset: an offset shifts under you every
            // time somebody sends a message while you are scrolling back.
            if (before is int cursor) query = query.Where(m => m.Id < cursor);

            var messages = await query
                .OrderByDescending(m => m.Id)
                .Take(PageSize)
                .ToListAsync(ct);

            messages.Reverse();

            return Ok(messages.Select(ToDto).ToList());
        }

        [HttpPost("{id:int}/messages")]
        public async Task<ActionResult<MessageDto>> Send(
            int id,
            SendMessageDto dto,
            [FromServices] IMessageNotifier notifier,
            CancellationToken ct)
        {
            var membership = await MemberAsync(id, ct);
            if (membership is null) return NotFound(NotFoundError());

            var body = dto.Body.Trim();
            if (body.Length == 0)
            {
                return BadRequest(new ApiErrorDto("EmptyMessage", "Type something first."));
            }

            var conversation = await _db.Conversations
                .Include(c => c.Members)
                .FirstAsync(c => c.Id == id, ct);

            // A direct conversation's connection can be removed or blocked after
            // the conversation exists, and the old thread must not stay open as a
            // back door.
            if (!conversation.IsGroup && !await StillConnectedAsync(conversation, ct))
            {
                return StatusCode(403, new ApiErrorDto(
                    "NotConnected", "You are no longer connected with this person."));
            }

            // Checked rather than trusted: a reply id from another conversation
            // would quote a message the reader is not allowed to see.
            if (dto.ReplyToMessageId is int replyId)
            {
                var exists = await _db.Messages.AnyAsync(
                    m => m.Id == replyId && m.ConversationId == id, ct);

                if (!exists)
                {
                    return BadRequest(new ApiErrorDto(
                        "InvalidReply", "That message is not in this conversation."));
                }
            }

            var message = new Message
            {
                ConversationId = id,
                SenderId = CurrentUserId,
                Body = body,
                Kind = MessageKind.Text,
                ReplyToMessageId = dto.ReplyToMessageId,
                SentAt = DateTime.UtcNow
            };

            _db.Messages.Add(message);
            conversation.LastMessageAt = message.SentAt;

            // Removing a direct conversation hides it; it does not block the
            // other person. Writing again has to put it back on their list, or
            // the message lands somewhere they will never look. Groups are
            // deliberately not treated this way - leaving one is a decision.
            if (!conversation.IsGroup)
            {
                foreach (var other in conversation.Members
                             .Where(m => m.UserId != CurrentUserId
                                         && m.Status == MembershipStatus.Left))
                {
                    other.Status = MembershipStatus.Active;
                    other.LeftAt = null;
                }
            }

            // Sending counts as reading: nobody wants their own message back as
            // an unread one.
            membership.LastReadAt = message.SentAt;

            await _db.SaveChangesAsync(ct);

            // After the save, and it never throws: an email provider having a
            // bad minute must not turn a sent message into an error.
            await notifier.MessageSentAsync(id, CurrentUserId, ct);

            await _db.Entry(message).Reference(m => m.Sender).LoadAsync(ct);
            if (message.ReplyToMessageId is not null)
            {
                await _db.Entry(message).Reference(m => m.ReplyToMessage).LoadAsync(ct);
                if (message.ReplyToMessage is not null)
                {
                    await _db.Entry(message.ReplyToMessage).Reference(r => r.Sender).LoadAsync(ct);
                }
            }

            return Ok(ToDto(message));
        }

        [HttpPut("{id:int}/messages/{messageId:int}")]
        public async Task<ActionResult<MessageDto>> EditMessage(
            int id, int messageId, EditMessageDto dto, CancellationToken ct)
        {
            if (await MemberAsync(id, ct) is null) return NotFound(NotFoundError());

            var message = await _db.Messages
                .Include(m => m.Sender)
                .Include(m => m.Reactions)
                .Include(m => m.Attachments)
                .Include(m => m.ReplyToMessage).ThenInclude(r => r!.Sender)
                .FirstOrDefaultAsync(
                    m => m.Id == messageId && m.ConversationId == id
                         && m.SenderId == CurrentUserId
                         && m.Kind == MessageKind.Text
                         && m.DeletedAt == null, ct);

            if (message is null) return NotFound(NotFoundError());

            var body = dto.Body.Trim();
            if (body.Length == 0)
            {
                return BadRequest(new ApiErrorDto("EmptyMessage", "A message cannot be empty."));
            }

            message.Body = body;

            // Marked, always. An edit that leaves no trace is a way to rewrite
            // what somebody remembers being said.
            message.EditedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);

            return Ok(ToDto(message));
        }

        [HttpDelete("{id:int}/messages/{messageId:int}")]
        public async Task<IActionResult> DeleteMessage(int id, int messageId, CancellationToken ct)
        {
            var membership = await MemberAsync(id, ct);
            if (membership is null) return NotFound(NotFoundError());

            var message = await _db.Messages.FirstOrDefaultAsync(
                m => m.Id == messageId && m.ConversationId == id
                     && m.Kind == MessageKind.Text, ct);

            if (message is null) return NotFound(NotFoundError());

            // Your own always; anybody's if you run the group. A group with no
            // way to take down what somebody posted is a group with no way to
            // deal with a problem.
            var allowed = message.SenderId == CurrentUserId
                          || membership.Role >= ConversationRole.Admin;

            if (!allowed) return NotFound(NotFoundError());

            // Soft: the row stays, so the conversation does not develop a hole
            // that two people reading it at once have to reconcile.
            message.DeletedAt = DateTime.UtcNow;
            message.Body = string.Empty;

            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        [HttpPost("{id:int}/pin")]
        public async Task<IActionResult> Pin(int id, PinDto dto, CancellationToken ct)
        {
            var membership = await MemberAsync(id, ct);
            if (membership is null) return NotFound(NotFoundError());

            membership.IsPinned = dto.Pinned;

            // Stamped only when it is newly pinned, so re-pinning something
            // already pinned does not quietly move it to the bottom.
            if (dto.Pinned) membership.PinnedAt ??= DateTime.UtcNow;
            else membership.PinnedAt = null;

            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        /// <summary>
        /// Puts the unread marker back behind the newest message.
        ///
        /// Set to just before the last message rather than to null: null means
        /// "never opened", which would mark the entire history unread instead
        /// of the one thing you wanted to come back to.
        /// </summary>
        [HttpPost("{id:int}/unread")]
        public async Task<IActionResult> MarkUnread(int id, CancellationToken ct)
        {
            var membership = await MemberAsync(id, ct);
            if (membership is null) return NotFound(NotFoundError());

            var last = await _db.Messages
                .Where(m => m.ConversationId == id
                            && m.Kind == MessageKind.Text
                            && m.SenderId != CurrentUserId)
                .OrderByDescending(m => m.SentAt)
                .Select(m => (DateTime?)m.SentAt)
                .FirstOrDefaultAsync(ct);

            // Nothing from anybody else means there is nothing to be unread.
            if (last is null) return NoContent();

            membership.LastReadAt = last.Value.AddSeconds(-1);
            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        /// <summary>
        /// Searches every conversation the student is in.
        ///
        /// Scoped by membership in the same query rather than filtered after,
        /// so there is no arrangement of parameters that reaches a message from
        /// a conversation they are not in.
        /// </summary>
        [HttpGet("search")]
        public async Task<ActionResult<List<MessageSearchHitDto>>> SearchMessages(
            [FromQuery] string? q, CancellationToken ct)
        {
            var query = (q ?? string.Empty).Trim();
            if (query.Length < 2) return Ok(new List<MessageSearchHitDto>());

            var mine = await _db.ConversationMembers
                .Where(m => m.UserId == CurrentUserId && m.Status == MembershipStatus.Active)
                .Select(m => m.ConversationId)
                .ToListAsync(ct);

            var pattern = $"%{query.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";

            var hits = await _db.Messages
                .Include(m => m.Sender)
                .Include(m => m.Conversation)!.ThenInclude(c => c!.Members).ThenInclude(m => m.User)
                .Where(m => mine.Contains(m.ConversationId)
                            && m.Kind == MessageKind.Text
                            && m.DeletedAt == null
                            && EF.Functions.ILike(m.Body, pattern))
                .OrderByDescending(m => m.SentAt)
                .Take(50)
                .ToListAsync(ct);

            return Ok(hits.Select(m => new MessageSearchHitDto
            {
                ConversationId = m.ConversationId,
                IsGroup = m.Conversation?.IsGroup ?? false,
                ConversationTitle = SearchTitle(m.Conversation),
                MessageId = m.Id,
                SenderName = m.SenderId is null
                    ? "Deleted account"
                    : m.Sender is null ? "Someone" : m.Sender.FirstName,
                Body = m.Body,
                SentAt = m.SentAt
            }).ToList());
        }

        [HttpDelete("{id:int}/photo")]
        public async Task<IActionResult> RemovePhoto(int id, CancellationToken ct)
        {
            var membership = await MemberAsync(id, ct);
            if (membership is null || membership.Role < ConversationRole.Admin)
            {
                return NotFound(NotFoundError());
            }

            var conversation = await _db.Conversations.FirstOrDefaultAsync(
                c => c.Id == id && c.IsGroup, ct);

            if (conversation is null) return NotFound(NotFoundError());

            var previous = conversation.PhotoPath;

            conversation.PhotoPath = null;
            conversation.PhotoContentType = null;
            await _db.SaveChangesAsync(ct);

            if (!string.IsNullOrEmpty(previous))
            {
                try
                {
                    await _storage.DeleteAsync(previous, ct);
                }
                catch
                {
                    // A leftover file is clutter; throwing would undo a save that
                    // already succeeded.
                }
            }

            return NoContent();
        }

        /// <summary>
        /// "I am still typing." Called on a throttle from the composer.
        ///
        /// Deliberately the cheapest write in the feature: one timestamp, no
        /// reads beyond the membership check. It is the most frequent call the
        /// API will take, and it must never become more than that.
        /// </summary>
        [HttpPost("{id:int}/typing")]
        public async Task<IActionResult> Typing(int id, CancellationToken ct)
        {
            var membership = await MemberAsync(id, ct);
            if (membership is null) return NotFound(NotFoundError());

            membership.LastTypingAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        /// <summary>
        /// Who is typing, and how far everybody else has read.
        ///
        /// Separate from the messages endpoint because these change every second
        /// or two while the messages do not, and pulling the whole history that
        /// often to learn one timestamp would be wasteful. Only the viewer's own
        /// conversation is ever reported on, and only to its members.
        /// </summary>
        [HttpGet("{id:int}/presence")]
        public async Task<ActionResult<ConversationPresenceDto>> Presence(
            int id, CancellationToken ct)
        {
            if (await MemberAsync(id, ct) is null) return NotFound(NotFoundError());

            var cutoff = DateTime.UtcNow.AddSeconds(-TypingWindowSeconds);

            var others = await _db.ConversationMembers
                .Where(m => m.ConversationId == id
                            && m.UserId != CurrentUserId
                            && m.Status == MembershipStatus.Active)
                .Select(m => new
                {
                    m.UserId,
                    m.User!.FirstName,
                    m.LastReadAt,
                    m.LastTypingAt
                })
                .ToListAsync(ct);

            return Ok(new ConversationPresenceDto
            {
                Typing = others
                    .Where(o => o.LastTypingAt != null && o.LastTypingAt > cutoff)
                    .Select(o => new PresencePersonDto
                    {
                        Id = o.UserId,
                        FirstName = string.IsNullOrWhiteSpace(o.FirstName) ? "Someone" : o.FirstName
                    })
                    .ToList(),

                Readers = others
                    .Select(o => new PresencePersonDto
                    {
                        Id = o.UserId,
                        FirstName = string.IsNullOrWhiteSpace(o.FirstName) ? "Someone" : o.FirstName,
                        LastReadAt = o.LastReadAt
                    })
                    .ToList()
            });
        }

        [HttpPost("{id:int}/read")]
        public async Task<IActionResult> MarkRead(int id, CancellationToken ct)
        {
            var membership = await MemberAsync(id, ct);
            if (membership is null) return NotFound(NotFoundError());

            membership.LastReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        /// <summary>
        /// Adds or removes one emoji on one message.
        ///
        /// A toggle rather than separate add and remove endpoints: tapping the
        /// same emoji twice is the only gesture there is, and two endpoints
        /// would mean the client deciding which one applies from state it may
        /// have fetched five seconds ago.
        /// </summary>
        [HttpPost("{id:int}/messages/{messageId:int}/reactions")]
        public async Task<IActionResult> React(
            int id, int messageId, ReactDto dto, CancellationToken ct)
        {
            if (await MemberAsync(id, ct) is null) return NotFound(NotFoundError());

            var emoji = dto.Emoji.Trim();
            if (emoji.Length == 0) return BadRequest(new ApiErrorDto("NoEmoji", "Pick an emoji."));

            var exists = await _db.Messages.AnyAsync(
                m => m.Id == messageId && m.ConversationId == id && m.DeletedAt == null, ct);

            if (!exists) return NotFound(NotFoundError());

            var mine = await _db.MessageReactions.FirstOrDefaultAsync(
                r => r.MessageId == messageId && r.UserId == CurrentUserId && r.Emoji == emoji, ct);

            if (mine is null)
            {
                _db.MessageReactions.Add(new MessageReaction
                {
                    MessageId = messageId,
                    UserId = CurrentUserId,
                    Emoji = emoji,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                _db.MessageReactions.Remove(mine);
            }

            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        /// <summary>
        /// Sends up to <see cref="MaxAttachmentsPerMessage"/> files or images,
        /// with an optional caption, as one message.
        ///
        /// One endpoint rather than "upload, then send with an id", because a
        /// two-step version leaves orphaned uploads behind every time somebody
        /// changes their mind between the two.
        /// </summary>
        [HttpPost("{id:int}/messages/attachment")]
        [RequestSizeLimit(MaxUploadBytes)]
        public async Task<ActionResult<MessageDto>> SendAttachment(
            int id,
            [FromForm] string? body,
            [FromServices] IProfilePhotoService images,
            [FromServices] IMessageNotifier notifier,
            CancellationToken ct)
        {
            var membership = await MemberAsync(id, ct);
            if (membership is null) return NotFound(NotFoundError());

            // Read off the request rather than bound to a parameter, so one file
            // and ten arrive by exactly the same path and no field name has to
            // agree between the two sides beyond "there are files here".
            var files = Request.Form.Files;

            if (files.Count == 0)
            {
                return BadRequest(new ApiErrorDto("NoFile", "Choose a file first."));
            }

            if (files.Count > MaxAttachmentsPerMessage)
            {
                return BadRequest(new ApiErrorDto(
                    "TooManyFiles",
                    $"Up to {MaxAttachmentsPerMessage} files can go in one message."));
            }

            // Every file is checked before any of them is written. A batch that
            // failed halfway would otherwise leave stored files belonging to a
            // message that never existed, and the student would see one refusal
            // with no idea which file caused it.
            foreach (var candidate in files)
            {
                if (candidate.Length == 0)
                {
                    return BadRequest(new ApiErrorDto(
                        "EmptyFile", $"\"{candidate.FileName}\" is empty."));
                }

                if (candidate.Length > MaxAttachmentBytes)
                {
                    return BadRequest(new ApiErrorDto(
                        "FileTooLarge", $"\"{candidate.FileName}\" is over the 15MB limit."));
                }

                // The extension is the gate, not the content type. A content
                // type arrives from the uploader and is a claim, not a fact.
                var candidateExtension = Path.GetExtension(candidate.FileName).ToLowerInvariant();

                if (!ImageExtensions.Contains(candidateExtension)
                    && !FileExtensions.Contains(candidateExtension))
                {
                    return BadRequest(new ApiErrorDto(
                        "UnsupportedFile",
                        $"\"{candidate.FileName}\" cannot be shared here. Images, PDFs, Office documents, text and zip files all work."));
                }
            }

            var conversation = await _db.Conversations
                .Include(c => c.Members)
                .FirstAsync(c => c.Id == id, ct);

            if (!conversation.IsGroup && !await StillConnectedAsync(conversation, ct))
            {
                return StatusCode(403, new ApiErrorDto(
                    "NotConnected", "You are no longer connected with this person."));
            }

            var message = new Message
            {
                ConversationId = id,
                SenderId = CurrentUserId,
                Body = (body ?? string.Empty).Trim(),
                Kind = MessageKind.Text,
                SentAt = DateTime.UtcNow
            };

            foreach (var file in files)
            {
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var looksLikeImage = ImageExtensions.Contains(extension);

                string storagePath;
                string contentType;
                var isImage = false;

                await using (var upload = file.OpenReadStream())
                {
                    if (looksLikeImage)
                    {
                        try
                        {
                            // Decoded and re-encoded, which strips the EXIF a
                            // phone photo carries and proves the file really is
                            // an image. Only a file that survives this is ever
                            // marked safe to render inline.
                            var stored = await images.SaveSharedImageAsync(upload, ct);

                            storagePath = stored.Path;
                            contentType = stored.ContentType;
                            isImage = true;
                        }
                        catch (InvalidImageException ex)
                        {
                            return BadRequest(new ApiErrorDto(
                                "InvalidImage", $"\"{file.FileName}\": {ex.Message}"));
                        }
                    }
                    else
                    {
                        storagePath = await _storage.SaveAsync(upload, file.FileName, ct);

                        // Recorded for the download header only. It is never
                        // used to decide whether something renders inline.
                        contentType = "application/octet-stream";
                    }
                }

                message.Attachments.Add(new MessageAttachment
                {
                    StoragePath = storagePath,
                    FileName = SafeName(file.FileName),
                    ContentType = contentType,
                    SizeBytes = file.Length,
                    IsImage = isImage
                });
            }

            _db.Messages.Add(message);
            conversation.LastMessageAt = message.SentAt;
            membership.LastReadAt = message.SentAt;

            if (!conversation.IsGroup)
            {
                foreach (var other in conversation.Members
                             .Where(m => m.UserId != CurrentUserId
                                         && m.Status == MembershipStatus.Left))
                {
                    other.Status = MembershipStatus.Active;
                    other.LeftAt = null;
                }
            }

            await _db.SaveChangesAsync(ct);

            await notifier.MessageSentAsync(id, CurrentUserId, ct);

            await _db.Entry(message).Reference(m => m.Sender).LoadAsync(ct);

            return Ok(ToDto(message));
        }

        [HttpGet("{id:int}/attachments/{attachmentId:int}")]
        public async Task<IActionResult> GetAttachment(
            int id, int attachmentId, CancellationToken ct)
        {
            if (await MemberAsync(id, ct) is null) return NotFound();

            var attachment = await _db.MessageAttachments
                .Include(a => a.Message)
                .FirstOrDefaultAsync(
                    a => a.Id == attachmentId && a.Message!.ConversationId == id, ct);

            if (attachment is null) return NotFound();

            try
            {
                var stream = await _storage.OpenAsync(attachment.StoragePath, ct);

                Response.Headers.CacheControl = "private, max-age=300";

                // Images render inline because the server made them itself.
                // Everything else downloads, whatever it claims to be - serving
                // an uploaded file inline is how a chat becomes an XSS hole.
                if (attachment.IsImage) return File(stream, attachment.ContentType);

                return File(stream, "application/octet-stream", attachment.FileName);
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }
        }

        // ------------------------------------------------------------- helpers

        /// <summary>
        /// The caller's active membership, or null.
        ///
        /// The single gate on this controller. It returns null for a conversation
        /// that does not exist, one the caller was never in, one they have left,
        /// and one they have only been invited to - all four become the same 404,
        /// so the endpoint never reveals a conversation exists to somebody
        /// outside it.
        /// </summary>
        private Task<ConversationMember?> MemberAsync(int conversationId, CancellationToken ct) =>
            _db.ConversationMembers.FirstOrDefaultAsync(
                m => m.ConversationId == conversationId
                     && m.UserId == CurrentUserId
                     && m.Status == MembershipStatus.Active,
                ct);

        private async Task<bool> AllConnectedAsync(List<string> userIds, CancellationToken ct)
        {
            foreach (var id in userIds)
            {
                if (id == CurrentUserId) continue;
                if (!await _connections.AreConnectedAsync(CurrentUserId, id, ct)) return false;
            }

            return true;
        }

        private async Task<bool> StillConnectedAsync(Conversation conversation, CancellationToken ct)
        {
            var other = conversation.Members.FirstOrDefault(m => m.UserId != CurrentUserId);

            return other is not null
                   && await _connections.AreConnectedAsync(CurrentUserId, other.UserId, ct);
        }

        /// <summary>
        /// Keeps a group owned when its owner walks out.
        ///
        /// Longest-serving admin, or longest-serving member if there are no
        /// admins. The alternative is an ownerless group nobody can rename, add
        /// to or clean up - which is worse than any choice this makes.
        /// </summary>
        private static void HandOverIfOwnerLeft(Conversation conversation, string leavingUserId)
        {
            var stillOwned = conversation.Members.Any(
                m => m.Status == MembershipStatus.Active && m.Role == ConversationRole.Owner);

            if (stillOwned) return;

            var heir = conversation.Members
                .Where(m => m.Status == MembershipStatus.Active && m.UserId != leavingUserId)
                .OrderByDescending(m => m.Role)
                .ThenBy(m => m.JoinedAt)
                .FirstOrDefault();

            if (heir is not null) heir.Role = ConversationRole.Owner;
        }

        // includeMuted is true for the conversation list, which marks a muted
        // chat quietly but still marks it - muting means "stop shouting at me",
        // not "hide that anything happened". It is false for the icon in the nav
        // bar, where a muted chat must not light the dot at all.
        private async Task<List<UnreadCount>> UnreadPerConversationAsync(
            bool includeMuted, CancellationToken ct) =>
            await _db.ConversationMembers
                .Where(m => m.UserId == CurrentUserId
                            && m.Status == MembershipStatus.Active
                            && (includeMuted || !m.IsMuted))
                .Select(m => new UnreadCount
                {
                    ConversationId = m.ConversationId,

                    // System messages do not count. "Sarah renamed the group" is
                    // worth showing in the timeline and not worth a red dot.
                    Count = _db.Messages.Count(x =>
                        x.ConversationId == m.ConversationId
                        && x.Kind == MessageKind.Text
                        && x.SenderId != CurrentUserId
                        && (m.LastReadAt == null || x.SentAt > m.LastReadAt))
                })
                .ToListAsync(ct);

        private sealed class UnreadCount
        {
            public int ConversationId { get; set; }
            public int Count { get; set; }
        }

        private async Task<string> NameOfAsync(string userId, CancellationToken ct)
        {
            var user = await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => new { u.FirstName, u.LastName })
                .FirstOrDefaultAsync(ct);

            return user is null ? "Someone" : $"{user.FirstName} {user.LastName}".Trim();
        }

        /// <summary>
        /// Adds the app's own narration to the timeline. Not saved here - the
        /// caller saves, so the message and the change it describes land together
        /// or not at all.
        /// </summary>
        private async Task SystemMessageAsync(int conversationId, string text, CancellationToken ct)
        {
            _db.Messages.Add(new Message
            {
                ConversationId = conversationId,
                SenderId = CurrentUserId,
                Body = text,
                Kind = MessageKind.System,
                SentAt = DateTime.UtcNow
            });

            var conversation = await _db.Conversations.FirstAsync(c => c.Id == conversationId, ct);
            conversation.LastMessageAt = DateTime.UtcNow;
        }

        private async Task<ConversationSummaryDto> SummaryAsync(
            Conversation conversation, CancellationToken ct)
        {
            await _db.Entry(conversation)
                .Collection(c => c.Members).Query()
                .Include(m => m.User).LoadAsync(ct);

            var others = conversation.Members
                .Where(m => m.UserId != CurrentUserId
                            && m.Status == MembershipStatus.Active
                            && m.User is not null)
                .Select(m => StudentCardMapper.ToCard(
                    m.User!, ProfileVisibility.Card, null, CurrentUserId))
                .ToList();

            var mine = conversation.Members.FirstOrDefault(m => m.UserId == CurrentUserId);

            return new ConversationSummaryDto
            {
                Id = conversation.Id,
                IsGroup = conversation.IsGroup,
                Title = Title(conversation, others),
                Description = conversation.Description,
                HasPhoto = !string.IsNullOrEmpty(conversation.PhotoPath),
                Members = others,
                LastMessageAt = conversation.LastMessageAt,
                IsMuted = mine?.IsMuted ?? false,
                MyRole = mine?.Role ?? ConversationRole.Member
            };
        }

        /// <summary>
        /// A name for a conversation in search results.
        ///
        /// A direct chat has no name of its own, so it borrows the other
        /// person's - which means finding them among the loaded members rather
        /// than the title the list would have used.
        /// </summary>
        private string SearchTitle(Conversation? conversation)
        {
            if (conversation is null) return "Conversation";
            if (conversation.IsGroup) return conversation.Name ?? "Group";

            var other = conversation.Members
                .FirstOrDefault(m => m.UserId != CurrentUserId && m.User is not null);

            return other?.User is null
                ? "Conversation"
                : $"{other.User.FirstName} {other.User.LastName}".Trim();
        }

        private static string? Preview(Message? message) =>
            message is null ? null
            : message.DeletedAt is not null ? "Message deleted"
            : message.Body;

        private static string Title(Conversation conversation, List<StudentCardDto> others) =>
            conversation.IsGroup
                ? conversation.Name ?? "Group"
                : others.Count > 0
                    ? $"{others[0].FirstName} {others[0].LastName}"

                    // The other person deleted their account, or left. Better a
                    // plain label than a blank row in the list.
                    : "Conversation";

        private MessageDto ToDto(Message message) => new()
        {
            Id = message.Id,

            // Empty rather than null: the client compares this to its own id,
            // and a missing field is a different kind of bug to find than an
            // id that matches nobody.
            SenderId = message.SenderId ?? string.Empty,

            // A null SenderId means the account was deleted. A null Sender with
            // an id present means it simply was not loaded, which is a
            // different thing and says so differently.
            SenderName = message.SenderId is null
                ? "Deleted account"
                : message.Sender is null
                    ? "Someone"
                    : $"{message.Sender.FirstName} {message.Sender.LastName}".Trim(),
            Body = message.DeletedAt is null ? message.Body : string.Empty,
            Kind = message.Kind,
            SentAt = message.SentAt,
            IsEdited = message.EditedAt is not null,
            IsDeleted = message.DeletedAt is not null,
            IsMine = message.SenderId == CurrentUserId,
            ReplyToId = message.ReplyToMessageId,
            ReplyToSender = message.ReplyToMessage is null
                ? null
                : message.ReplyToMessage.SenderId is null
                    ? "Deleted account"
                    : message.ReplyToMessage.Sender?.FirstName,
            ReplyToBody = message.ReplyToMessage is null ? null
                : message.ReplyToMessage.DeletedAt is not null ? "Message deleted"
                : Clip(message.ReplyToMessage.Body, 120),

            // Tallied here rather than shipping every row: the client only
            // needs the total and whether one of them is yours.
            Reactions = message.Reactions
                .GroupBy(r => r.Emoji)
                .Select(g => new MessageReactionDto
                {
                    Emoji = g.Key,
                    Count = g.Count(),
                    Mine = g.Any(r => r.UserId == CurrentUserId)
                })
                .OrderByDescending(r => r.Count)
                .ThenBy(r => r.Emoji)
                .ToList(),

            Attachments = message.Attachments
                .Select(a => new MessageAttachmentDto
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    SizeBytes = a.SizeBytes,
                    IsImage = a.IsImage
                })
                .ToList()
        };

        /// <summary>
        /// A display name with the path stripped out of it.
        ///
        /// The name is only ever shown, never used to open anything - the
        /// storage key is a GUID - but a name like "../../x" appearing in a
        /// download header is still worth not doing.
        /// </summary>
        private static string SafeName(string fileName)
        {
            var name = Path.GetFileName(fileName ?? string.Empty).Trim();

            return string.IsNullOrEmpty(name) ? "file"
                : name.Length <= 200 ? name
                : name[..200];
        }

        private static ApiErrorDto NotFoundError() =>
            new("ConversationNotFound", "That conversation does not exist, or you are not in it.");

        private static string? Clip(string? text, int max) =>
            string.IsNullOrWhiteSpace(text) ? null
            : text.Trim().Length <= max ? text.Trim()
            : text.Trim()[..max];
    }
}
