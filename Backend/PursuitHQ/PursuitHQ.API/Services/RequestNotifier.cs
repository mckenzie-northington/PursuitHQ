using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PursuitHQ.API.Data;

namespace PursuitHQ.API.Services
{
    public interface IRequestNotifier
    {
        /// <summary>One student has asked to connect with another.</summary>
        Task ConnectionRequestedAsync(
            string addresseeId, string requesterId, CancellationToken ct = default);

        /// <summary>Somebody has been invited to a group.</summary>
        Task GroupInvitedAsync(
            int conversationId, string inviterId, IEnumerable<string> invitedIds,
            CancellationToken ct = default);
    }

    /// <summary>
    /// Emails about things waiting on an answer.
    ///
    /// No throttle, unlike message email: a request is a single event that
    /// happens rarely and needs a decision, and the API already refuses to let
    /// the same person ask twice while one is pending. Like every other email
    /// here it names who it is from and nothing more - the note somebody wrote
    /// with their request stays in the app.
    /// </summary>
    public class RequestNotifier : IRequestNotifier
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailQueue _queue;
        private readonly EmailOptions _options;
        private readonly ILogger<RequestNotifier> _logger;

        public RequestNotifier(
            ApplicationDbContext db,
            IEmailQueue queue,
            IOptions<EmailOptions> options,
            ILogger<RequestNotifier> logger)
        {
            _db = db;
            _queue = queue;
            _options = options.Value;
            _logger = logger;
        }

        public async Task ConnectionRequestedAsync(
            string addresseeId, string requesterId, CancellationToken ct = default)
        {
            try
            {
                var from = await NameOfAsync(requesterId, ct);

                await SendAsync(
                    new[] { addresseeId },
                    subject: $"{from} wants to connect on PursuitHQ",
                    html: $"<strong>{Encode(from)}</strong> sent you a connection request on PursuitHQ.",
                    text: $"{from} sent you a connection request on PursuitHQ.",
                    path: "/students",
                    ct);
            }
            catch (Exception ex)
            {
                // Swallowed: the request is saved, and losing the email about it
                // is not worth failing the request itself.
                _logger.LogWarning(ex, "Could not queue a connection request email.");
            }
        }

        public async Task GroupInvitedAsync(
            int conversationId, string inviterId, IEnumerable<string> invitedIds,
            CancellationToken ct = default)
        {
            try
            {
                var ids = invitedIds.Where(id => id != inviterId).Distinct().ToList();
                if (ids.Count == 0) return;

                var from = await NameOfAsync(inviterId, ct);

                var name = await _db.Conversations
                    .Where(c => c.Id == conversationId)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync(ct);

                var group = string.IsNullOrWhiteSpace(name) ? "a group" : name!;

                await SendAsync(
                    ids,
                    subject: $"{from} invited you to {group}",
                    html: $"<strong>{Encode(from)}</strong> invited you to join "
                          + $"<strong>{Encode(group)}</strong> on PursuitHQ.",
                    text: $"{from} invited you to join {group} on PursuitHQ.",
                    path: "/messages",
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not queue group invitation emails.");
            }
        }

        /// <param name="html">Already encoded. It goes into the mail as-is.</param>
        private async Task SendAsync(
            IEnumerable<string> userIds, string subject, string html, string text,
            string path, CancellationToken ct)
        {
            var ids = userIds.Distinct().ToList();
            if (ids.Count == 0) return;

            // Asked the other way round on purpose: who has switched this OFF.
            // A student who has never opened the settings page has no preference
            // row at all, and looking for rows that say yes would quietly leave
            // every one of them out of email they were never asked about.
            var off = await _db.NotificationPreferences
                .Where(p => ids.Contains(p.UserId)
                            && (!p.EmailEnabled || !p.RequestEmailsEnabled))
                .Select(p => p.UserId)
                .ToListAsync(ct);

            var wanted = ids.Where(id => !off.Contains(id)).ToList();
            if (wanted.Count == 0) return;

            var accounts = await _db.Users
                .Where(u => wanted.Contains(u.Id))
                .Select(u => new { u.Id, u.Email, u.FirstName })
                .ToListAsync(ct);

            var link = $"{_options.AppUrl.TrimEnd('/')}{path}";

            foreach (var account in accounts)
            {
                if (string.IsNullOrWhiteSpace(account.Email)) continue;

                var greeting = string.IsNullOrWhiteSpace(account.FirstName)
                    ? "there"
                    : account.FirstName;

                var body =
                    $"<p>Hi {Encode(greeting)},</p>"
                    + $"<p>{html}</p>"
                    + $"<p><a href=\"{Encode(link)}\" style=\"color:#4f46e5\">Open PursuitHQ</a></p>";

                var plain = $"Hi {greeting},\n\n{text}\nOpen PursuitHQ: {link}";

                _queue.Enqueue(new EmailMessage(
                    account.Email!,
                    greeting,
                    subject,
                    EmailLayout.Html("Waiting on you", body, _options.AppUrl),
                    EmailLayout.Text("Waiting on you", plain, _options.AppUrl)));
            }
        }

        private async Task<string> NameOfAsync(string userId, CancellationToken ct)
        {
            var person = await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => new { u.FirstName, u.LastName })
                .FirstOrDefaultAsync(ct);

            var name = $"{person?.FirstName} {person?.LastName}".Trim();
            return name.Length == 0 ? "Someone" : name;
        }

        private static string Encode(string value) => WebUtility.HtmlEncode(value);
    }
}
