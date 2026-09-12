namespace PursuitHQ.API.Services
{
    /// <summary>
    /// Prints the email to the API terminal instead of sending it.
    ///
    /// Used whenever no API key is set, which means the whole reminder pipeline
    /// - the schedule, the duplicate checks, the wording of every email - can be
    /// built and tested before a domain exists or a penny is spent. The same
    /// approach password reset already uses for its link.
    /// </summary>
    public class ConsoleEmailService : IEmailService
    {
        private readonly ILogger<ConsoleEmailService> _logger;

        public ConsoleEmailService(ILogger<ConsoleEmailService> logger) => _logger = logger;

        /// <summary>
        /// Deliberately false. This does not send email, and saying otherwise
        /// would let the settings page promise something that will not happen.
        /// </summary>
        public bool IsConfigured => false;

        public Task<EmailResult> SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            _logger.LogInformation(
                "EMAIL (not sent - no provider configured)\n  To:      {Name} <{Address}>\n  Subject: {Subject}\n\n{Body}",
                message.ToName, message.ToAddress, message.Subject, message.TextBody);

            return Task.FromResult(EmailResult.Ok("console"));
        }
    }
}
