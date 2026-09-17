using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.Students;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.Controllers
{
    /// <summary>
    /// Reporting another student.
    ///
    /// Write-only from the app's point of view. There is no admin interface and
    /// no endpoint that lists reports, because there is no administrator role -
    /// adding one that any signed-in account could reach by guessing a URL would
    /// be worse than having none. Reports are read directly from the database
    /// by whoever runs PursuitHQ.
    /// </summary>
    [Route("api/reports")]
    public class ReportsController : ApiControllerBase
    {
        /// <summary>
        /// Reports one student may file in a day.
        ///
        /// The reporting system is itself something that can be used to harass
        /// somebody, so it needs a bound of its own. High enough that a genuine
        /// bad week is not cut off.
        /// </summary>
        private const int MaxReportsPerDay = 20;

        private readonly ApplicationDbContext _db;

        public ReportsController(ApplicationDbContext db) => _db = db;

        [HttpPost]
        public async Task<ActionResult<ReportCreatedDto>> Create(
            CreateReportDto dto, CancellationToken ct)
        {
            if (dto.ReportedUserId == CurrentUserId)
            {
                return BadRequest(new ApiErrorDto(
                    "InvalidReport", "You cannot report yourself."));
            }

            if (!await _db.Users.AnyAsync(u => u.Id == dto.ReportedUserId, ct))
            {
                return NotFound(NotFoundError());
            }

            var since = DateTime.UtcNow.AddDays(-1);

            var recent = await _db.Reports.CountAsync(
                r => r.ReporterId == CurrentUserId && r.CreatedAt > since, ct);

            if (recent >= MaxReportsPerDay)
            {
                return BadRequest(new ApiErrorDto(
                    "TooManyReports",
                    "You have filed a lot of reports today. Please email support if something urgent is happening."));
            }

            string? snapshot = null;
            int? conversationId = null;

            if (dto.MessageId is int messageId)
            {
                var message = await _db.Messages
                    .Where(m => m.Id == messageId)
                    .Select(m => new { m.ConversationId, m.Body, m.SenderId })
                    .FirstOrDefaultAsync(ct);

                if (message is null) return NotFound(NotFoundError());

                // The same membership rule as everywhere else in messaging. A
                // report is not a way to reach into a conversation you are not
                // in, and a message id you were never shown must look exactly
                // like a message id that does not exist.
                var isMember = await _db.ConversationMembers.AnyAsync(
                    m => m.ConversationId == message.ConversationId
                         && m.UserId == CurrentUserId
                         && m.Status == MembershipStatus.Active, ct);

                if (!isMember) return NotFound(NotFoundError());

                // The person reported has to be the person who sent it -
                // otherwise a report attaches somebody else's words to their
                // name, and the record is worse than useless.
                if (message.SenderId != dto.ReportedUserId)
                {
                    return BadRequest(new ApiErrorDto(
                        "InvalidReport", "That message was not sent by the student you are reporting."));
                }

                snapshot = message.Body;
                conversationId = message.ConversationId;
            }

            _db.Reports.Add(new Report
            {
                ReporterId = CurrentUserId,
                ReportedUserId = dto.ReportedUserId,
                MessageId = dto.MessageId,
                ConversationId = conversationId,
                MessageSnapshot = snapshot,
                Reason = dto.Reason,
                Details = string.IsNullOrWhiteSpace(dto.Details) ? null : dto.Details.Trim(),
                Status = ReportStatus.Open,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(ct);

            // Deliberately says nothing about the other account - not whether
            // they have been reported before, not what happens next. A reporter
            // learning anything about the subject of their report turns the
            // report button into a way of probing somebody.
            return Ok(new ReportCreatedDto
            {
                Message = "Thanks - this has been sent to whoever runs PursuitHQ. "
                          + "If you want this person to stop contacting you, block them as well."
            });
        }

        private static ApiErrorDto NotFoundError() =>
            new("NotFound", "We could not find that.");
    }
}
