using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PursuitHQ.API.Data;

namespace PursuitHQ.API.Services
{
    public interface ICreationNotifier
    {
        /// <summary>
        /// Emails the student that they added something, if they have asked to
        /// be told.
        ///
        /// Never throws. A controller calls this after the save has succeeded,
        /// and nothing about a confirmation email is worth failing a create
        /// over.
        /// </summary>
        Task ItemCreatedAsync(
            string userId, string kind, string title, string? detail, CancellationToken ct = default);
    }

    public class CreationNotifier : ICreationNotifier
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailQueue _queue;
        private readonly EmailOptions _options;
        private readonly ILogger<CreationNotifier> _logger;

        public CreationNotifier(
            ApplicationDbContext db,
            IEmailQueue queue,
            IOptions<EmailOptions> options,
            ILogger<CreationNotifier> logger)
        {
            _db = db;
            _queue = queue;
            _options = options.Value;
            _logger = logger;
        }

        public async Task ItemCreatedAsync(
            string userId, string kind, string title, string? detail, CancellationToken ct = default)
        {
            try
            {
                // One read, and no email work at all unless it is wanted. The
                // master switch is checked too, so turning everything off really
                // does mean everything.
                var wanted = await _db.NotificationPreferences
                    .AnyAsync(p => p.UserId == userId
                                   && p.EmailEnabled
                                   && p.CreationConfirmationsEnabled, ct);

                if (!wanted) return;

                var account = await _db.Users
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.Email, u.FirstName })
                    .FirstOrDefaultAsync(ct);

                if (string.IsNullOrWhiteSpace(account?.Email)) return;

                var name = string.IsNullOrWhiteSpace(account.FirstName) ? "there" : account.FirstName;

                var html =
                    $"<p>Hi {Encode(name)},</p>"
                    + $"<p>You added a {Encode(kind)}: <strong>{Encode(title)}</strong></p>"
                    + (string.IsNullOrWhiteSpace(detail)
                        ? ""
                        : $"<p style=\"color:#475569\">{Encode(detail!)}</p>");

                var text =
                    $"Hi {name},\n\n"
                    + $"You added a {kind}: {title}"
                    + (string.IsNullOrWhiteSpace(detail) ? "" : $"\n{detail}");

                _queue.Enqueue(new EmailMessage(
                    account.Email!,
                    name,
                    $"Added: {title}",
                    EmailLayout.Html("Added to PursuitHQ", html, _options.AppUrl),
                    EmailLayout.Text("Added to PursuitHQ", text, _options.AppUrl)));
            }
            catch (Exception ex)
            {
                // Swallowed deliberately. The record is already saved; losing
                // the confirmation is not worth turning a successful create into
                // a 500 the student has to puzzle over.
                _logger.LogWarning(ex, "Could not queue a creation confirmation for {UserId}.", userId);
            }
        }

        private static string Encode(string value) => WebUtility.HtmlEncode(value);
    }
}
