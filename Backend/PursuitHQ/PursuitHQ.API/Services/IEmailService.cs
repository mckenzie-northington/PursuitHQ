namespace PursuitHQ.API.Services
{
    /// <summary>One email, already rendered.</summary>
    public record EmailMessage(
        string ToAddress,
        string ToName,
        string Subject,
        string HtmlBody,
        string TextBody);

    /// <summary>
    /// What happened.
    ///
    /// A result rather than an exception, because a failed send is a normal
    /// outcome here, not a bug: the caller records it against the student and
    /// carries on with everyone else's reminders rather than the whole run
    /// dying because one address bounced.
    /// </summary>
    public record EmailResult(bool Sent, string? ProviderId, string? Error)
    {
        public static EmailResult Ok(string? id) => new(true, id, null);
        public static EmailResult Failed(string error) => new(false, null, error);
    }

    public interface IEmailService
    {
        /// <summary>
        /// False when no API key or from-address is set. The settings page says
        /// so plainly rather than letting someone switch reminders on and wait.
        /// </summary>
        bool IsConfigured { get; }

        Task<EmailResult> SendAsync(EmailMessage message, CancellationToken ct = default);
    }
}
