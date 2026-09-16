using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PursuitHQ.API.Data;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.Services
{
    public interface IMessageNotifier
    {
        /// <summary>
        /// Emails the other people in a conversation that a message arrived.
        ///
        /// Never throws. A controller calls this after the message is safely
        /// saved, and an email provider having a bad minute must not turn a sent
        /// message into a 500.
        /// </summary>
        Task MessageSentAsync(int conversationId, string senderId, CancellationToken ct = default);
    }

    /// <summary>
    /// Who gets told, and how often.
    ///
    /// The email deliberately carries the sender and the group and nothing else.
    /// Putting the message text in it would hand what two students said to each
    /// other to a mail provider, leave it sitting in an inbox that may be read
    /// over somebody's shoulder, and make "delete for everyone" a lie.
    /// </summary>
    public class MessageNotifier : IMessageNotifier
    {
        /// <summary>
        /// At most one email per conversation per person in this long. A chat is
        /// a back-and-forth; without this a five minute conversation would put
        /// thirty emails in an inbox and teach its owner to filter the lot.
        /// </summary>
        private static readonly TimeSpan Quiet = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Somebody whose last read is this recent has the thread open in front
        /// of them - the messages page marks itself read every few seconds - so
        /// emailing them about a message they are watching arrive is noise.
        /// </summary>
        private static readonly TimeSpan Watching = TimeSpan.FromSeconds(90);

        private readonly ApplicationDbContext _db;
        private readonly IEmailQueue _queue;
        private readonly EmailOptions _options;
        private readonly ILogger<MessageNotifier> _logger;

        public MessageNotifier(
            ApplicationDbContext db,
            IEmailQueue queue,
            IOptions<EmailOptions> options,
            ILogger<MessageNotifier> logger)
        {
            _db = db;
            _queue = queue;
            _options = options.Value;
            _logger = logger;
        }

        public async Task MessageSentAsync(
            int conversationId, string senderId, CancellationToken ct = default)
        {
            try
            {
                var now = DateTime.UtcNow;

                var conversation = await _db.Conversations
                    .Include(c => c.Members)
                    .FirstOrDefaultAsync(c => c.Id == conversationId, ct);

                if (conversation is null) return;

                // Muting silences the email as well as the unread dot. Someone
                // who has told the app to leave them alone about a group has
                // said it once, and should not have to say it again per channel.
                var candidates = conversation.Members
                    .Where(m => m.UserId != senderId
                                && m.Status == MembershipStatus.Active
                                && !m.IsMuted
                                && (m.LastReadAt is null || now - m.LastReadAt > Watching)
                                && (m.LastMessageEmailAt is null
                                    || now - m.LastMessageEmailAt > Quiet))
                    .ToList();

                if (candidates.Count == 0) return;

                // Stamped for everyone who got this far, whether or not an email
                // actually goes out. Someone with email switched off should not
                // be re-examined on every single message.
                foreach (var member in candidates) member.LastMessageEmailAt = now;
                await _db.SaveChangesAsync(ct);

                var ids = candidates.Select(m => m.UserId).ToList();

                // Asked the other way round on purpose: who has switched this
                // OFF. A student who has never opened the settings page has no
                // preference row at all, and looking for rows that say yes would
                // quietly leave every one of them out of email that is meant to
                // be on by default. The master switch is in here too, so turning
                // everything off really does mean everything.
                var off = await _db.NotificationPreferences
                    .Where(p => ids.Contains(p.UserId)
                                && (!p.EmailEnabled || !p.MessageEmailsEnabled))
                    .Select(p => p.UserId)
                    .ToListAsync(ct);

                var wanted = ids.Where(userId => !off.Contains(userId)).ToList();
                if (wanted.Count == 0) return;

                var accounts = await _db.Users
                    .Where(u => wanted.Contains(u.Id))
                    .Select(u => new { u.Id, u.Email, u.FirstName })
                    .ToListAsync(ct);

                var sender = await _db.Users
                    .Where(u => u.Id == senderId)
                    .Select(u => new { u.FirstName, u.LastName })
                    .FirstOrDefaultAsync(ct);

                var senderName = $"{sender?.FirstName} {sender?.LastName}".Trim();
                if (senderName.Length == 0) senderName = "Someone";

                // Name, not Title: Title is what the DTO calls it once a direct
                // chat has been named after the other person. On the row itself
                // only groups have a name at all.
                var inGroup = conversation.IsGroup && !string.IsNullOrWhiteSpace(conversation.Name)
                    ? $" in {conversation.Name}"
                    : string.Empty;

                var link = $"{_options.AppUrl.TrimEnd('/')}/messages";

                foreach (var account in accounts)
                {
                    if (string.IsNullOrWhiteSpace(account.Email)) continue;

                    var name = string.IsNullOrWhiteSpace(account.FirstName)
                        ? "there"
                        : account.FirstName;

                    var html =
                        $"<p>Hi {Encode(name)},</p>"
                        + $"<p><strong>{Encode(senderName)}</strong> sent you a message"
                        + $"{Encode(inGroup)} on PursuitHQ.</p>"
                        + $"<p><a href=\"{Encode(link)}\" style=\"color:#4f46e5\">Open your messages</a></p>";

                    var text =
                        $"Hi {name},\n\n"
                        + $"{senderName} sent you a message{inGroup} on PursuitHQ.\n"
                        + $"Open your messages: {link}";

                    _queue.Enqueue(new EmailMessage(
                        account.Email!,
                        name,
                        $"New message from {senderName}{inGroup}",
                        EmailLayout.Html("New message", html, _options.AppUrl),
                        EmailLayout.Text("New message", text, _options.AppUrl)));
                }
            }
            catch (Exception ex)
            {
                // Swallowed deliberately. The message is already saved and the
                // recipient will see it in the app; losing the email about it is
                // not worth turning a successful send into an error.
                _logger.LogWarning(
                    ex, "Could not queue message emails for conversation {ConversationId}.",
                    conversationId);
            }
        }

        private static string Encode(string value) => WebUtility.HtmlEncode(value);
    }
}
