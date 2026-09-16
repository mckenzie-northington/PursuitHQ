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
    /// Requests between students, and what comes of them.
    ///
    /// A connection is the gate on everything social: full profile, direct
    /// messages, being added to a group. Nothing else in this feature decides
    /// for itself who may talk to whom.
    /// </summary>
    [Route("api/connections")]
    public class ConnectionsController : ApiControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IConnectionService _connections;

        public ConnectionsController(ApplicationDbContext db, IConnectionService connections)
        {
            _db = db;
            _connections = connections;
        }

        /// <summary>Everyone the student is connected to.</summary>
        [HttpGet]
        public async Task<ActionResult<List<StudentCardDto>>> GetConnections(CancellationToken ct)
        {
            var rows = await _db.Connections
                .Include(c => c.Requester)
                .Include(c => c.Addressee)
                .Where(c => c.Status == ConnectionStatus.Accepted
                            && (c.RequesterId == CurrentUserId || c.AddresseeId == CurrentUserId))
                .ToListAsync(ct);

            var cards = rows
                .Select(c =>
                {
                    var other = c.RequesterId == CurrentUserId ? c.Addressee : c.Requester;

                    return other is null
                        ? null
                        : StudentCardMapper.ToCard(other, ProfileVisibility.Full, c, CurrentUserId);
                })
                .Where(card => card is not null)
                .OrderBy(card => card!.LastName).ThenBy(card => card!.FirstName)
                .ToList();

            return Ok(cards!);
        }

        /// <summary>Requests waiting for this student to answer.</summary>
        [HttpGet("requests")]
        public Task<ActionResult<List<PendingConnectionDto>>> GetIncoming(CancellationToken ct) =>
            PendingAsync(incoming: true, ct);

        /// <summary>Requests this student has sent and not heard back on.</summary>
        [HttpGet("sent")]
        public Task<ActionResult<List<PendingConnectionDto>>> GetOutgoing(CancellationToken ct) =>
            PendingAsync(incoming: false, ct);

        [HttpPost]
        public async Task<ActionResult<StudentCardDto>> SendRequest(
            ConnectionRequestDto dto,
            [FromServices] IRequestNotifier notifier,
            CancellationToken ct)
        {
            if (dto.AddresseeId == CurrentUserId)
            {
                return BadRequest(new ApiErrorDto(
                    "InvalidRequest", "You cannot connect with yourself."));
            }

            var target = await _db.Users.FirstOrDefaultAsync(u => u.Id == dto.AddresseeId, ct);
            if (target is null) return NotFound(NotFoundError());

            var existing = await _connections.BetweenAsync(CurrentUserId, dto.AddresseeId, ct);

            if (existing is not null)
            {
                switch (existing.Status)
                {
                    case ConnectionStatus.Blocked:
                        // Same answer either way. To the blocked person this has
                        // to look like any other request that cannot be sent, not
                        // like a confirmation that they were blocked.
                        return NotFound(NotFoundError());

                    case ConnectionStatus.Accepted:
                        return BadRequest(new ApiErrorDto(
                            "AlreadyConnected", "You are already connected."));

                    case ConnectionStatus.Pending when existing.RequesterId == CurrentUserId:
                        return BadRequest(new ApiErrorDto(
                            "AlreadyRequested", "You have already sent this request."));

                    case ConnectionStatus.Pending:
                        // They asked first and this is effectively an answer, so
                        // treat it as one rather than opening a second request
                        // that neither side can tell apart from the first.
                        existing.Status = ConnectionStatus.Accepted;
                        existing.RespondedAt = DateTime.UtcNow;
                        await _db.SaveChangesAsync(ct);

                        return Ok(StudentCardMapper.ToCard(
                            target, ProfileVisibility.Full, existing, CurrentUserId));

                    case ConnectionStatus.Declined:
                        // Reusing the row rather than adding another keeps the
                        // pair to one row, which is what every lookup assumes.
                        existing.RequesterId = CurrentUserId;
                        existing.AddresseeId = dto.AddresseeId;
                        existing.Status = ConnectionStatus.Pending;
                        existing.Note = Clip(dto.Note);
                        existing.CreatedAt = DateTime.UtcNow;
                        existing.RespondedAt = null;
                        await _db.SaveChangesAsync(ct);

                        await notifier.ConnectionRequestedAsync(
                            dto.AddresseeId, CurrentUserId, ct);

                        return Ok(StudentCardMapper.ToCard(
                            target, ProfileVisibility.Card, existing, CurrentUserId));
                }
            }

            var connection = new Connection
            {
                RequesterId = CurrentUserId,
                AddresseeId = dto.AddresseeId,
                Status = ConnectionStatus.Pending,
                Note = Clip(dto.Note),
                CreatedAt = DateTime.UtcNow
            };

            _db.Connections.Add(connection);
            await _db.SaveChangesAsync(ct);

            // After the save, and it never throws.
            await notifier.ConnectionRequestedAsync(dto.AddresseeId, CurrentUserId, ct);

            return Ok(StudentCardMapper.ToCard(
                target, ProfileVisibility.Card, connection, CurrentUserId));
        }

        [HttpPost("{id:int}/accept")]
        public async Task<IActionResult> Accept(int id, CancellationToken ct)
        {
            // Only the addressee can accept - the person who sent it obviously
            // cannot answer on the other's behalf.
            var connection = await _db.Connections.FirstOrDefaultAsync(
                c => c.Id == id && c.AddresseeId == CurrentUserId
                     && c.Status == ConnectionStatus.Pending, ct);

            if (connection is null) return NotFound(NotFoundError());

            connection.Status = ConnectionStatus.Accepted;
            connection.RespondedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        [HttpPost("{id:int}/decline")]
        public async Task<IActionResult> Decline(int id, CancellationToken ct)
        {
            var connection = await _db.Connections.FirstOrDefaultAsync(
                c => c.Id == id && c.AddresseeId == CurrentUserId
                     && c.Status == ConnectionStatus.Pending, ct);

            if (connection is null) return NotFound(NotFoundError());

            connection.Status = ConnectionStatus.Declined;
            connection.RespondedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        /// <summary>Cancels a request, or removes an existing connection.</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Remove(int id, CancellationToken ct)
        {
            var connection = await _db.Connections.FirstOrDefaultAsync(
                c => c.Id == id
                     && (c.RequesterId == CurrentUserId || c.AddresseeId == CurrentUserId)
                     && c.Status != ConnectionStatus.Blocked, ct);

            if (connection is null) return NotFound(NotFoundError());

            _db.Connections.Remove(connection);
            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        [HttpPost("block")]
        public async Task<IActionResult> Block(BlockDto dto, CancellationToken ct)
        {
            if (dto.UserId == CurrentUserId) return BadRequest(NotFoundError());

            var connection = await _connections.BetweenAsync(CurrentUserId, dto.UserId, ct);

            if (connection is null)
            {
                connection = new Connection
                {
                    RequesterId = CurrentUserId,
                    AddresseeId = dto.UserId
                };

                _db.Connections.Add(connection);
            }

            connection.Status = ConnectionStatus.Blocked;
            connection.BlockedById = CurrentUserId;
            connection.RespondedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        [HttpPost("unblock")]
        public async Task<IActionResult> Unblock(BlockDto dto, CancellationToken ct)
        {
            var connection = await _connections.BetweenAsync(CurrentUserId, dto.UserId, ct);

            // Only whoever set the block can lift it.
            if (connection is null
                || connection.Status != ConnectionStatus.Blocked
                || connection.BlockedById != CurrentUserId)
            {
                return NotFound(NotFoundError());
            }

            // Back to nothing rather than back to connected. Unblocking should
            // not quietly restore a relationship that blocking ended.
            _db.Connections.Remove(connection);
            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        // ---------- helpers ----------

        private async Task<ActionResult<List<PendingConnectionDto>>> PendingAsync(
            bool incoming, CancellationToken ct)
        {
            var rows = await _db.Connections
                .Include(c => c.Requester)
                .Include(c => c.Addressee)
                .Where(c => c.Status == ConnectionStatus.Pending
                            && (incoming
                                ? c.AddresseeId == CurrentUserId
                                : c.RequesterId == CurrentUserId))
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync(ct);

            var pending = rows
                .Select(c =>
                {
                    var other = incoming ? c.Requester : c.Addressee;

                    return other is null
                        ? null
                        : new PendingConnectionDto
                        {
                            Id = c.Id,
                            Note = c.Note,
                            CreatedAt = c.CreatedAt,

                            // A card, not a full profile. A request is a reason
                            // to decide, not a reason to hand over the details.
                            Student = StudentCardMapper.ToCard(
                                other, ProfileVisibility.Card, c, CurrentUserId)
                        };
                })
                .Where(p => p is not null)
                .ToList();

            return Ok(pending!);
        }

        private static ApiErrorDto NotFoundError() =>
            new("ConnectionNotFound", "That request no longer exists.");

        private static string? Clip(string? note) =>
            string.IsNullOrWhiteSpace(note) ? null
            : note.Length <= 300 ? note.Trim()
            : note.Trim()[..300];
    }
}
