using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PursuitHQ.API.Services
{
    /// <summary>
    /// Sends through Resend.
    ///
    /// Resend will only accept a from-address on a domain you have verified with
    /// DNS records. That is the whole reason a domain is needed to email anyone
    /// but yourself: the records are what prove the mail is really from you, and
    /// without them receiving servers have no reason to believe it.
    /// </summary>
    public class ResendEmailService : IEmailService
    {
        private readonly HttpClient _http;
        private readonly EmailOptions _options;
        private readonly ILogger<ResendEmailService> _logger;

        public ResendEmailService(
            HttpClient http, IOptions<EmailOptions> options, ILogger<ResendEmailService> logger)
        {
            _http = http;
            _options = options.Value;
            _logger = logger;

            if (_options.IsConfigured)
            {
                _http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            }
        }

        public bool IsConfigured => _options.IsConfigured;

        public async Task<EmailResult> SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                return EmailResult.Failed("Email is not configured on this server.");
            }

            var payload = new
            {
                from = $"{_options.FromName} <{_options.FromAddress}>",
                to = new[] { message.ToAddress },
                subject = message.Subject,
                html = message.HtmlBody,
                text = message.TextBody
            };

            HttpResponseMessage response;
            try
            {
                response = await _http.PostAsJsonAsync("emails", payload, ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                return EmailResult.Failed("The email provider timed out.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Could not reach the email provider");
                return EmailResult.Failed("Could not reach the email provider.");
            }

            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                // Resend's message says exactly what is wrong - an unverified
                // domain, a malformed address - and that is far more useful to
                // read in a log than "400 Bad Request".
                var detail = ReadError(body) ?? body;

                _logger.LogWarning(
                    "Resend rejected an email with {Status}: {Detail}", (int)response.StatusCode, detail);

                return EmailResult.Failed($"The email provider returned {(int)response.StatusCode}: {detail}");
            }

            return EmailResult.Ok(ReadId(body));
        }

        private static string? ReadError(string body) => ReadString(body, "message");

        private static string? ReadId(string body) => ReadString(body, "id");

        private static string? ReadString(string body, string property)
        {
            try
            {
                using var document = JsonDocument.Parse(body);

                return document.RootElement.TryGetProperty(property, out var value)
                    ? value.GetString()
                    : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
