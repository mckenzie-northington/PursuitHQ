using System.Net;
using Microsoft.Extensions.Options;

namespace PursuitHQ.API.Services
{
    /// <summary>
    /// The two emails that mark the beginning and the end of an account.
    ///
    /// Unlike every other notifier here, these ignore the student's email
    /// preferences. That is deliberate. A confirmation that an account was
    /// created, and a confirmation that one was destroyed, are the record of
    /// something irreversible happening to the account itself - they are how
    /// somebody finds out that a stranger signed up with their address, or that
    /// their account really is gone. A preference switch marked "email me about
    /// things I add" was never consent to be kept in the dark about those.
    /// </summary>
    public interface IAccountNotifier
    {
        /// <summary>Welcomes a student who has just signed up. Never throws.</summary>
        Task AccountCreatedAsync(
            string email, string? firstName, CancellationToken ct = default);

        /// <summary>
        /// Confirms that an account is gone.
        ///
        /// Takes the address and name as plain values rather than a user, because
        /// by the time this is worth sending the row it would have read them from
        /// no longer exists. The caller captures them before deleting.
        /// </summary>
        Task AccountDeletedAsync(
            string email, string? firstName, CancellationToken ct = default);
    }

    public class AccountNotifier : IAccountNotifier
    {
        private readonly IEmailQueue _queue;
        private readonly EmailOptions _options;
        private readonly ILogger<AccountNotifier> _logger;

        public AccountNotifier(
            IEmailQueue queue,
            IOptions<EmailOptions> options,
            ILogger<AccountNotifier> logger)
        {
            _queue = queue;
            _options = options.Value;
            _logger = logger;
        }

        public Task AccountCreatedAsync(
            string email, string? firstName, CancellationToken ct = default)
        {
            var name = Greeting(firstName);
            var app = _options.AppUrl.TrimEnd('/');

            var html =
                $"<p>Hi {Encode(name)},</p>"
                + "<p>Your PursuitHQ account is ready. Everything starts from the dashboard: "
                + "add your courses, and assignments and deadlines follow from there.</p>"
                + $"<p><a href=\"{Encode(app)}/dashboard\" "
                + "style=\"display:inline-block;background:#4f46e5;color:#ffffff;text-decoration:none;"
                + "padding:10px 18px;border-radius:6px;font-weight:600\">Open PursuitHQ</a></p>"
                + "<p style=\"color:#475569\">You can choose which emails you get, or turn them all "
                + $"off, in <a href=\"{Encode(app)}/settings\">settings</a>.</p>"
                + "<p style=\"color:#475569\">If you did not create this account, reply to this "
                + "email and tell us - somebody has used your address by mistake.</p>";

            var text =
                $"Hi {name},\n\n"
                + "Your PursuitHQ account is ready. Everything starts from the dashboard: add your "
                + "courses, and assignments and deadlines follow from there.\n\n"
                + $"{app}/dashboard\n\n"
                + $"You can choose which emails you get, or turn them all off, at {app}/settings\n\n"
                + "If you did not create this account, reply to this email and tell us - somebody "
                + "has used your address by mistake.";

            return Send(
                email,
                name,
                "Welcome to PursuitHQ",
                "Welcome to PursuitHQ",
                html,
                text,
                ct);
        }

        public Task AccountDeletedAsync(
            string email, string? firstName, CancellationToken ct = default)
        {
            var name = Greeting(firstName);

            var html =
                $"<p>Hi {Encode(name)},</p>"
                + "<p>Your PursuitHQ account has been deleted, along with your courses, "
                + "assignments, messages, files and everything else stored with it. None of it "
                + "can be recovered.</p>"
                + "<p style=\"color:#475569\">Scheduled reminders and digests have stopped, so this "
                + "is the last email you will get from us.</p>"
                + "<p style=\"color:#475569\">If you did not delete this account, reply to this "
                + "email straight away.</p>";

            var text =
                $"Hi {name},\n\n"
                + "Your PursuitHQ account has been deleted, along with your courses, assignments, "
                + "messages, files and everything else stored with it. None of it can be "
                + "recovered.\n\n"
                + "Scheduled reminders and digests have stopped, so this is the last email you "
                + "will get from us.\n\n"
                + "If you did not delete this account, reply to this email straight away.";

            return Send(
                email,
                name,
                "Your PursuitHQ account has been deleted",
                "Account deleted",
                html,
                text,
                ct);
        }

        private Task Send(
            string email,
            string name,
            string subject,
            string heading,
            string html,
            string text,
            CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email)) return Task.CompletedTask;

                // showPreferences: false. The footer link invites the reader to
                // change which emails they get, which is meaningless on an
                // account that no longer exists and premature on one that was
                // created a second ago.
                _queue.Enqueue(new EmailMessage(
                    email,
                    name,
                    subject,
                    EmailLayout.Html(heading, html, _options.AppUrl, showPreferences: false),
                    EmailLayout.Text(heading, text, _options.AppUrl, showPreferences: false)));
            }
            catch (Exception ex)
            {
                // Swallowed on purpose, as everywhere else email is queued from
                // inside a request. The account was created, or destroyed,
                // before this ran; neither outcome should be reported as a
                // failure because an email could not be composed.
                _logger.LogWarning(ex, "Could not queue the \"{Subject}\" email.", subject);
            }

            return Task.CompletedTask;
        }

        private static string Greeting(string? firstName) =>
            string.IsNullOrWhiteSpace(firstName) ? "there" : firstName;

        private static string Encode(string value) => WebUtility.HtmlEncode(value);
    }
}
