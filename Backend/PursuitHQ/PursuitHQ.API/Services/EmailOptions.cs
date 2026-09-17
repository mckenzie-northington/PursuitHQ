namespace PursuitHQ.API.Services
{
    public class EmailOptions
    {
        public const string SectionName = "Email";

        /// <summary>
        /// Resend API key. Server-side only, and never in appsettings.json:
        /// dotnet user-secrets set "Email:ApiKey" "re_..."
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Must be an address on a domain verified with Resend. Anything else
        /// is rejected by the provider, not by us.
        /// </summary>
        public string FromAddress { get; set; } = string.Empty;

        public string FromName { get; set; } = "PursuitHQ";

        /// <summary>
        /// Where the app lives, for links in emails. A reminder with no way back
        /// to the thing it is reminding you about is half an email.
        /// </summary>
        public string AppUrl { get; set; } = "http://localhost:3000";

        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Where "report a problem" messages are delivered.
        ///
        /// Separate from FromAddress because the two do different jobs: the
        /// from address has to be on a domain Resend has verified, while this
        /// is simply an inbox somebody reads. Left blank, it falls back to the
        /// from address so the feature still works rather than silently
        /// dropping reports.
        /// </summary>
        public string SupportAddress { get; set; } = string.Empty;

        /// <summary>
        /// The support inbox, or the from address if none was configured.
        /// </summary>
        public string SupportInbox =>
            string.IsNullOrWhiteSpace(SupportAddress) ? FromAddress : SupportAddress;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(FromAddress);
    }
}
