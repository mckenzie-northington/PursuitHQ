using System.Net;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.Support;
using PursuitHQ.API.Services;

namespace PursuitHQ.API.Controllers
{
    /// <summary>
    /// "Report a problem" - a student telling us the app is broken.
    ///
    /// Signed in only. An open form would be an email relay that anybody on the
    /// internet could point at the support inbox, and knowing which account sent
    /// a report is most of what makes one worth reading.
    /// </summary>
    [Route("api/support")]
    public class SupportController : ApiControllerBase
    {
        /// <summary>
        /// Reports one account may send in a day. Generous for somebody having
        /// a genuinely bad day with the app, low enough that a script cannot
        /// bury the inbox.
        /// </summary>
        private const int MaxPerDay = 10;

        /// <summary>
        /// Counted in memory rather than stored. A problem report is not a
        /// record worth a database table - the email is the record - and a
        /// counter that resets when the server restarts is a fine trade for
        /// not adding a migration to ship a contact form.
        /// </summary>
        private static readonly Dictionary<string, (DateTime Day, int Count)> Sent = new();

        private readonly ApplicationDbContext _db;
        private readonly IEmailQueue _queue;
        private readonly EmailOptions _options;
        private readonly ILogger<SupportController> _logger;

        public SupportController(
            ApplicationDbContext db,
            IEmailQueue queue,
            IOptions<EmailOptions> options,
            ILogger<SupportController> logger)
        {
            _db = db;
            _queue = queue;
            _options = options.Value;
            _logger = logger;
        }

        [HttpPost("problem")]
        public async Task<ActionResult<ProblemReportResponseDto>> ReportProblem(
            ProblemReportDto dto, CancellationToken ct)
        {
            if (!UnderDailyLimit(CurrentUserId))
            {
                return BadRequest(new ApiErrorDto(
                    "TooManyReports",
                    "You have sent several reports today. Please reply to one of them instead "
                    + "so everything stays in one thread."));
            }

            if (string.IsNullOrWhiteSpace(_options.SupportInbox))
            {
                // Told plainly rather than pretending it was sent. A contact
                // form that silently discards what somebody wrote is worse than
                // one that admits it is not set up.
                _logger.LogError("A problem report was submitted but no support inbox is configured.");

                return StatusCode(StatusCodes.Status503ServiceUnavailable, new ApiErrorDto(
                    "SupportUnavailable",
                    "Reporting is not available right now. Sorry - please try again later."));
            }

            var account = await _db.Users
                .Where(u => u.Id == CurrentUserId)
                .Select(u => new { u.Email, u.FirstName, u.LastName })
                .FirstOrDefaultAsync(ct);

            var who = $"{account?.FirstName} {account?.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(who)) who = "Unknown";

            var html =
                $"<p><strong>{Encode(dto.Subject)}</strong></p>"
                + $"<p style=\"white-space:pre-wrap\">{Encode(dto.Description)}</p>"
                + "<hr style=\"border:none;border-top:1px solid #e2e8f0;margin:20px 0\">"
                + $"<p style=\"color:#475569;font-size:13px\">From: {Encode(who)} "
                + $"&lt;{Encode(account?.Email ?? "no address")}&gt;<br>"
                + $"Account: {Encode(CurrentUserId)}<br>"
                + $"Page: {Encode(string.IsNullOrWhiteSpace(dto.PageUrl) ? "not given" : dto.PageUrl!)}<br>"
                + $"Sent: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</p>";

            var text =
                $"{dto.Subject}\n\n"
                + $"{dto.Description}\n\n"
                + "----\n"
                + $"From: {who} <{account?.Email ?? "no address"}>\n"
                + $"Account: {CurrentUserId}\n"
                + $"Page: {(string.IsNullOrWhiteSpace(dto.PageUrl) ? "not given" : dto.PageUrl)}\n"
                + $"Sent: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC";

            // Addressed to the support inbox, not to the student. The subject
            // line carries their name so a reply lands in the right place
            // without anyone having to open the message first.
            _queue.Enqueue(new EmailMessage(
                _options.SupportInbox,
                "PursuitHQ support",
                $"[Problem] {dto.Subject}",
                EmailLayout.Html("Problem report", html, _options.AppUrl, showPreferences: false),
                EmailLayout.Text("Problem report", text, _options.AppUrl, showPreferences: false)));

            // A copy back to the student, so they have a record of what they
            // said and something to reply to.
            if (!string.IsNullOrWhiteSpace(account?.Email))
            {
                var name = string.IsNullOrWhiteSpace(account.FirstName) ? "there" : account.FirstName;

                var copyHtml =
                    $"<p>Hi {Encode(name)},</p>"
                    + "<p>Thanks - your report reached us. Here is what you sent:</p>"
                    + $"<p><strong>{Encode(dto.Subject)}</strong></p>"
                    + $"<p style=\"color:#475569;white-space:pre-wrap\">{Encode(dto.Description)}</p>"
                    + "<p>PursuitHQ is run by one person, so a reply may take a few days.</p>";

                var copyText =
                    $"Hi {name},\n\n"
                    + "Thanks - your report reached us. Here is what you sent:\n\n"
                    + $"{dto.Subject}\n\n{dto.Description}\n\n"
                    + "PursuitHQ is run by one person, so a reply may take a few days.";

                _queue.Enqueue(new EmailMessage(
                    account.Email!,
                    name,
                    "We got your report",
                    EmailLayout.Html("We got your report", copyHtml, _options.AppUrl, showPreferences: false),
                    EmailLayout.Text("We got your report", copyText, _options.AppUrl, showPreferences: false)));
            }

            return Ok(new ProblemReportResponseDto
            {
                Message = "Thanks - your report is on its way. We have emailed you a copy."
            });
        }

        /// <summary>
        /// Counts today's reports for one account, rolling over at UTC midnight.
        /// </summary>
        private static bool UnderDailyLimit(string userId)
        {
            var today = DateTime.UtcNow.Date;

            lock (Sent)
            {
                if (Sent.TryGetValue(userId, out var entry) && entry.Day == today)
                {
                    if (entry.Count >= MaxPerDay) return false;

                    Sent[userId] = (today, entry.Count + 1);
                    return true;
                }

                Sent[userId] = (today, 1);
                return true;
            }
        }

        private static string Encode(string value) => WebUtility.HtmlEncode(value);
    }
}
