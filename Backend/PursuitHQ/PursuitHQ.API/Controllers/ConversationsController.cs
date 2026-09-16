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
    /// Every endpoint here begins by proving the caller is a current member of
    /// the conversation. That check is the entire security model for messaging,
    /// and it is different in kind from the rest of PursuitHQ: elsewhere a
    /// missing filter shows you an error page, here it shows you two other
    /// people's conversation. There is one helper, <see cref="MemberAsync"/>,
    /// and nothing reads or writes a message without going through it.
    /// </summary>
    [Route("api/conversations")]
    public class ConversationsController : ApiControllerBase
    {
        private const int PageSize = 50;
        private const int MaxGroupMembers = 100;

        private readonly ApplicationDbContext _db;
        private readonly IConnectionService _connections;

        public ConversationsController(ApplicationDbContext db, IConnectionService connections)
        {
            _db = db;
            _connections = connections;
        }

        [HttpGet]
        public async Task<ActionResult<List<ConversationSummaryDto>>> GetAll(CancellationToken ct)
        {
            var mine = await _db.ConversationMembers
                .Where(m => m.UserId == CurrentUserId && m.LeftAt == null)
                .Select(m => new { m.ConversationId, m.LastReadAt, m.IsAdmin })
                .ToListAsync(ct);

            var ids = mine.Select(m => m.ConversationId).ToList();

            var conversations = await _db.Conversations
                .Where(c => ids.Contains(c.Id))
                .Include(c => c.Members.Where(m => m.LeftAt == null))
                    .ThenInclude(m => m.User)
                .OrderByDescending(c => c.LastMessageAt)
                .ToListAsync(ct);

            // Two queries, not one. EF cannot put an Include after a grouping
            // projection: it compiles and then throws at runtime, which is how
            // this reached a 500 rather than a red squiggle. So the ids come
            // back first, then the rows are fetched by id with the sender.
            var lastIds = await _db.Messages
                .Where(m => ids.Contains(m.ConversationId))
                .GroupBy(m => m.ConversationId)
                .Select(g => g.Max(m => m.Id))
                .ToListAsync(ct);

            var latest = await _db.Messages
                .Include(m => m.Sender)
                .Where(m => lastIds.Contains(m.Id))
                .ToListAsync(ct);

            // Unread per conversation as one correlated query. Counting inside
            // the loop below was a round trip per conversation, every time the
            // list was opened.
            var unread = await _db.ConversationMembers
                .Where(m => m.UserId == CurrentUserId && m.LeftAt == null)
                .Select(m => new
                {
                    m.ConversationId,
                    Count = _db.Messages.Count(x =>
                        x.ConversationId == m.ConversationId
                        && x.SenderId != CurrentUserId
                        && (m.LastReadAt == null || x.SentAt > m.LastReadAt))
                })
                .ToListAsync(ct);

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
                    Members = others,
                    LastMessage = last is null ? null
                        : last.DeletedAt is not null ? "Message deleted" : last.Body,
                    LastMessageSender = last?.Sender?.FirstName,
                    LastMessageAt = conversation.LastMessageAt,
                    IsAdmin = membership.IsAdmin,

                    // Counted from the reader's own marker, so two people in the
                    // same group can have different unread counts.
                    UnreadCount = unread
                        .FirstOrDefault(u => u.ConversationId == conversation.Id)?.Count ?? 0
                });
            }

            return Ok(summaries);
        }

        /// <summary>Drives the dot on the messages icon.</summary>
        [HttpGet("unread")]
        public async Task<ActionResult<UnreadDto>> Unread(CancellationToken ct)
        {
            // One correlated query. Every open tab polls this every thirty
            // seconds, so a round trip per conversation is not a cost worth
            // paying for a number that drives a single red dot.
            var counts = await _db.ConversationMembers
                .Where(m => m.UserId == CurrentUserId && m.LeftAt == null)
                .Select(m => _db.Messages.Count(x =>
                    x.ConversationId == m.ConversationId
                    && x.SenderId != CurrentUserId
                    && (m.LastReadAt == null || x.SentAt > m.LastReadAt)))
                .ToListAsync(ct);

            var total = counts.Sum();
            var withUnread = counts.Count(c => c > 0);

            return Ok(new UnreadDto
            {
                Total = total,
                Conversations = withUnread,

                // Requests share the dot. They are the same kind of thing to the
                // person looking at it: someone is waiting on you.
                ConnectionRequests = await _db.Connections.CountAsync(
                    c => c.AddresseeId == CurrentUserId && c.Status == ConnectionStatus.Pending, ct)
            });
        }

        /// <summary>
        /// Opens the chat with one person, creating it if this is the first time.
        /// </summary>
        [HttpPost("direct")]
        public async Task<ActionResult<ConversationSummaryDto>> StartDirect(
            StartDirectChatDto dto, CancellationToken ct)
        {
            if (dto.UserId == CurrentUserId)
            {
                return BadRequest(new ApiErrorDto(
                    "InvalidRequest", "You cannot message yourself."));
            }

            // The rule that makes requests mean something: no connection, no
            // conversation. Checked here rather than on the client, because the
            // client is not what is being defended against.
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

            if (existing is not null) return Ok(await SummaryAsync(existing, ct));

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

        [HttpPost("group")]
        public async Task<ActionResult<ConversationSummaryDto>> CreateGroup(
            CreateGroupDto dto, CancellationToken ct)
        {
            var wanted = dto.MemberIds.Distinct().Where(id => id != CurrentUserId).ToList();

            if (wanted.Count == 0)
            {
                return BadRequest(new ApiErrorDto(
                    "NoMembers", "Add at least one other person."));
            }

            if (wanted.Count + 1 > MaxGroupMembers)
            {
                return BadRequest(new ApiErrorDto(
                    "TooManyMembers", $"A group can hold {MaxGroupMembers} people."));
            }

            // Every one of them, not most of them. A group is otherwise a way to
            // put a message in front of somebody who never accepted you.
            foreach (var id in wanted)
            {
                if (!await _connections.AreConnectedAsync(CurrentUserId, id, ct))
                {
                    return StatusCode(403, new ApiErrorDto(
                        "NotConnected",
                        "You can only add students you are connected with."));
                }
            }

            var conversation = new Conversation
            {
                IsGroup = true,
                Name = dto.Name.Trim(),
                CreatedById = CurrentUserId,
                CreatedAt = DateTime.UtcNow,
                LastMessageAt = DateTime.UtcNow,
                Members = { new ConversationMember { UserId = CurrentUserId, IsAdmin = true } }
            };

            foreach (var id in wanted)
            {
                conversation.Members.Add(new ConversationMember { UserId = id });
            }

            _db.Conversations.Add(conversation);
            await _db.SaveChangesAsync(ct);

            return Ok(await SummaryAsync(conversation, ct));
        }

        [HttpGet("{id:int}/messages")]
        public async Task<ActionResult<List<MessageDto>>> GetMessages(
            int id, [FromQuery] int? before, CancellationToken ct)
        {
            if (await MemberAsync(id, ct) is null) return NotFound(NotFoundError());

            var query = _db.Messages
                .Include(m => m.Sender)
                .Where(m => m.ConversationId == id);

            // Paging by id rather than by offset: an offset shifts under you
            // every time somebody sends a message while you are scrolling back.
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
            int id, SendMessageDto dto, CancellationToken ct)
        {
            var membership = await MemberAsync(id, ct);
            if (membership is null) return NotFound(NotFoundError());

            var body = dto.Body.Trim();
            if (body.Length == 0)
            {
                return BadRequest(new ApiErrorDto("EmptyMessage", "Type something first."));
            }

            // In a direct chat the connection can be removed or blocked after the
            // conversation exists, and the old conversation must not stay open as
            // a back door.
            if (!await CanStillPostAsync(id, ct))
            {
                return StatusCode(403, new ApiErrorDto(
                    "NotConnected", "You are no longer connected with this person."));
            }

            var message = new Message
            {
                ConversationId = id,
                SenderId = CurrentUserId,
                Body = body,
                SentAt = DateTime.UtcNow
            };

            _db.Messages.Add(message);

            var conversation = await _db.Conversations.FirstAsync(c => c.Id == id, ct);
            conversation.LastMessageAt = message.SentAt;

            // Sending counts as reading: nobody wants their own message to come
            // back as an unread one.
            membership.LastReadAt = message.SentAt;

            await _db.SaveChangesAsync(ct);

            await _db.Entry(message).Reference(m => m.Sender).LoadAsync(ct);

            return Ok(ToDto(message));
        }

        [HttpDelete("{id:int}/messages/{messageId:int}")]
        public async Task<IActionResult> DeleteMessage(int id, int messageId, CancellationToken ct)
        {
            if (await MemberAsync(id, ct) is null) return NotFound(NotFoundError());

            var message = await _db.Messages.FirstOrDefaultAsync(
                m => m.Id == messageId && m.ConversationId == id
                     && m.SenderId == CurrentUserId, ct);

            if (message is null) return NotFound(NotFoundError());

            // Soft: the row stays so the conversation does not develop a hole
            // that two people reading it at once have to reconcile.
            message.DeletedAt = DateTime.UtcNow;
            message.Body = string.Empty;

            await _db.SaveChangesAsync(ct);

            return NoContent();
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

        [HttpPost("{id:int}/members")]
        public async Task<IActionResult> AddMembers(
            int id, AddMembersDto dto, CancellationToken ct)
        {
            var membership = await MemberAsync(id, ct);
            if (membership is null || !membership.IsAdmin) return NotFound(NotFoundError());

            var conversation = await _db.Conversations
                .Include(c => c.Members)
                .FirstOrDefaultAsync(c => c.Id == id && c.IsGroup, ct);

            if (conversation is null) return NotFound(NotFoundError());

            foreach (var userId in dto.MemberIds.Distinct())
            {
                if (!await _connections.AreConnectedAsync(CurrentUserId, userId, ct))
                {
                    return StatusCode(403, new ApiErrorDto(
                        "NotConnected", "You can only add students you are connected with."));
                }

                var already = conversation.Members.FirstOrDefault(m => m.UserId == userId);

                if (already is null)
                {
                    if (conversation.Members.Count(m => m.LeftAt == null) >= MaxGroupMembers)
                    {
                        return BadRequest(new ApiErrorDto(
                            "TooManyMembers", $"A group can hold {MaxGroupMembers} people."));
                    }

                    conversation.Members.Add(new ConversationMember { UserId = userId });
                }
                else if (already.LeftAt is not null)
                {
                    // Rejoining clears the marker rather than adding a second row.
                    already.LeftAt = null;
                    already.JoinedAt = DateTime.UtcNow;
                    already.LastReadAt = DateTime.UtcNow;
                }
            }

            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        /// <summary>Leave a group, or remove somebody from one as an admin.</summary>
        [HttpDelete("{id:int}/members/{userId}")]
        public async Task<IActionResult> RemoveMember(int id, string userId, CancellationToken ct)
        {
            var membership = await MemberAsync(id, ct);
            if (membership is null) return NotFound(NotFoundError());

            // "me" so that leaving does not require the client to know its own
            // id, which it otherwise never needs for this.
            if (userId == "me") userId = CurrentUserId;

            if (userId != CurrentUserId && !membership.IsAdmin) return NotFound(NotFoundError());

            var target = await _db.ConversationMembers.FirstOrDefaultAsync(
                m => m.ConversationId == id && m.UserId == userId && m.LeftAt == null, ct);

            if (target is null) return NotFound(NotFoundError());

            target.LeftAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        [HttpPut("{id:int}/name")]
        public async Task<IActionResult> Rename(int id, RenameGroupDto dto, CancellationToken ct)
        {
            var membership = await MemberAsync(id, ct);
            if (membership is null || !membership.IsAdmin) return NotFound(NotFoundError());

            var conversation = await _db.Conversations.FirstOrDefaultAsync(
                c => c.Id == id && c.IsGroup, ct);

            if (conversation is null) return NotFound(NotFoundError());

            conversation.Name = dto.Name.Trim();
            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        // ---------- helpers ----------

        /// <summary>
        /// The caller's current membership, or null.
        ///
        /// The single gate on this whole controller. It returns null for a
        /// conversation that does not exist and for one the caller was never in
        /// or has left - all three become the same 404, so the endpoint never
        /// reveals that a conversation exists to somebody outside it.
        /// </summary>
        private Task<ConversationMember?> MemberAsync(int conversationId, CancellationToken ct) =>
            _db.ConversationMembers.FirstOrDefaultAsync(
                m => m.ConversationId == conversationId
                     && m.UserId == CurrentUserId
                     && m.LeftAt == null,
                ct);

        /// <summary>
        /// Whether a direct conversation is still backed by a connection.
        ///
        /// Groups are not re-checked per message: membership is managed by the
        /// admin and leaving is how you get out.
        /// </summary>
        private async Task<bool> CanStillPostAsync(int conversationId, CancellationToken ct)
        {
            var conversation = await _db.Conversations
                .Include(c => c.Members)
                .FirstAsync(c => c.Id == conversationId, ct);

            if (conversation.IsGroup) return true;

            var other = conversation.Members.FirstOrDefault(m => m.UserId != CurrentUserId);

            return other is not null
                   && await _connections.AreConnectedAsync(CurrentUserId, other.UserId, ct);
        }

        private async Task<ConversationSummaryDto> SummaryAsync(
            Conversation conversation, CancellationToken ct)
        {
            await _db.Entry(conversation)
                .Collection(c => c.Members).Query()
                .Include(m => m.User).LoadAsync(ct);

            var others = conversation.Members
                .Where(m => m.UserId != CurrentUserId && m.LeftAt == null && m.User is not null)
                .Select(m => StudentCardMapper.ToCard(
                    m.User!, ProfileVisibility.Card, null, CurrentUserId))
                .ToList();

            return new ConversationSummaryDto
            {
                Id = conversation.Id,
                IsGroup = conversation.IsGroup,
                Title = Title(conversation, others),
                Members = others,
                LastMessageAt = conversation.LastMessageAt,
                IsAdmin = conversation.Members
                    .Any(m => m.UserId == CurrentUserId && m.IsAdmin)
            };
        }

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
            SenderId = message.SenderId,
            SenderName = message.Sender is null
                ? "Someone"
                : $"{message.Sender.FirstName} {message.Sender.LastName}".Trim(),
            Body = message.DeletedAt is null ? message.Body : string.Empty,
            SentAt = message.SentAt,
            IsDeleted = message.DeletedAt is not null,
            IsMine = message.SenderId == CurrentUserId
        };

        private static ApiErrorDto NotFoundError() =>
            new("ConversationNotFound", "That conversation does not exist, or you are not in it.");
    }
}
