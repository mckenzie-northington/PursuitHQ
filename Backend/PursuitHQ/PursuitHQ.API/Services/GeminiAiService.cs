using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PursuitHQ.API.Services
{
    /// <summary>
    /// Gemini implementation. The API key travels in a header, never in the
    /// query string, so it cannot leak through logs or browser history.
    /// </summary>
    public class GeminiAiService : IAiService
    {
        private const string Endpoint = "https://generativelanguage.googleapis.com/v1beta/interactions";

        private readonly HttpClient _http;
        private readonly AiOptions _options;
        private readonly ILogger<GeminiAiService> _logger;

        public GeminiAiService(
            HttpClient http, IOptions<AiOptions> options, ILogger<GeminiAiService> logger)
        {
            _http = http;
            _options = options.Value;
            _logger = logger;
        }

        public bool IsConfigured => _options.IsConfigured;

        public async Task<string> CompleteAsync(
            string prompt, string? model = null, CancellationToken ct = default)
        {
            var json = await SendAsync(prompt, model, useSearch: false, ct);
            return ExtractText(json);
        }

        public async Task<GroundedResult> CompleteWithSearchAsync(
            string prompt, string? model = null, CancellationToken ct = default)
        {
            var json = await SendAsync(prompt, model, useSearch: true, ct);
            return new GroundedResult(ExtractText(json), ExtractCitations(json));
        }

        /// <summary>
        /// How long to wait before each retry. Three attempts total, spread far
        /// enough apart to outlast a brief capacity spike without leaving the
        /// student staring at a spinner.
        /// </summary>
        private static readonly TimeSpan[] RetryDelays =
        {
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5)
        };

        /// <summary>
        /// Server-side failures worth retrying. A shared model gets busy, and
        /// the same request a few seconds later usually succeeds.
        ///
        /// 429 is deliberately NOT in this list. That is a quota, not a blip -
        /// retrying it just burns time before failing with the same message.
        /// </summary>
        private static bool IsTransient(int status) =>
            status is 500 or 502 or 503 or 504;

        private async Task<JsonDocument> SendAsync(
            string prompt, string? model, bool useSearch, CancellationToken ct)
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException(
                    "AI is not configured. Run: dotnet user-secrets set \"Ai:ApiKey\" \"<your Gemini key>\"");
            }

            var payload = new Dictionary<string, object?>
            {
                ["model"] = model ?? _options.SearchModel,
                ["input"] = prompt
            };

            if (useSearch)
            {
                payload["tools"] = new[] { new { type = "google_search" } };
            }

            var serialized = JsonSerializer.Serialize(payload);

            for (var attempt = 0; ; attempt++)
            {
                // A request message cannot be sent twice, so each attempt
                // builds its own.
                using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
                {
                    Content = new StringContent(serialized, Encoding.UTF8, "application/json")
                };

                request.Headers.Add("x-goog-api-key", _options.ApiKey);

                using var response = await _http.SendAsync(request, ct);
                var body = await response.Content.ReadAsStringAsync(ct);

                if (response.IsSuccessStatusCode) return JsonDocument.Parse(body);

                var status = (int)response.StatusCode;

                _logger.LogWarning("Gemini returned {Status} on attempt {Attempt}: {Body}",
                    status, attempt + 1, body.Length > 500 ? body[..500] : body);

                // Gemini explains itself in the response body - quota exceeded,
                // model overloaded, bad key. Passing that through beats a
                // generic message that leaves you guessing.
                var detail = ExtractApiError(body);

                if (IsTransient(status) && attempt < RetryDelays.Length)
                {
                    await Task.Delay(RetryDelays[attempt], ct);
                    continue;
                }

                if (status == 429)
                {
                    throw new HttpRequestException("Gemini rate limit or quota: " + detail);
                }

                if (IsTransient(status))
                {
                    // Said plainly, because there is nothing wrong with the
                    // request and nothing for the student to fix.
                    throw new HttpRequestException(
                        "Gemini is busy right now and did not answer after "
                        + $"{RetryDelays.Length + 1} tries. This is temporary - wait a minute and "
                        + $"try again. (It said: {detail})");
                }

                throw new HttpRequestException($"Gemini returned {status}: {detail}");
            }
        }

        /// <summary>Pulls the human-readable message out of an API error body.</summary>
        private static string ExtractApiError(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);

                if (doc.RootElement.TryGetProperty("error", out var error))
                {
                    var message = error.TryGetProperty("message", out var m)
                        ? m.GetString()
                        : null;

                    var status = error.TryGetProperty("status", out var st)
                        ? st.GetString()
                        : null;

                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        return status is null ? message! : $"{message} ({status})";
                    }
                }
            }
            catch (JsonException)
            {
                // Not JSON - fall through to the raw text.
            }

            return body.Length > 300 ? body[..300] : body;
        }

        /// <summary>
        /// Pulls the model's text out of the response. The shape varies a
        /// little between API versions, so several known locations are tried
        /// before giving up.
        /// </summary>
        private static string ExtractText(JsonDocument doc)
        {
            var root = doc.RootElement;

            if (root.TryGetProperty("output_text", out var direct) &&
                direct.ValueKind == JsonValueKind.String)
            {
                return direct.GetString() ?? string.Empty;
            }

            var sb = new StringBuilder();

            if (root.TryGetProperty("output", out var output) &&
                output.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in output.EnumerateArray())
                {
                    if (!item.TryGetProperty("content", out var content)) continue;
                    if (content.ValueKind != JsonValueKind.Array) continue;

                    foreach (var part in content.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                        {
                            sb.Append(t.GetString());
                        }
                    }
                }
            }

            if (sb.Length > 0) return sb.ToString();

            // Older candidates/parts shape.
            if (root.TryGetProperty("candidates", out var candidates) &&
                candidates.ValueKind == JsonValueKind.Array)
            {
                foreach (var candidate in candidates.EnumerateArray())
                {
                    if (!candidate.TryGetProperty("content", out var content)) continue;
                    if (!content.TryGetProperty("parts", out var parts)) continue;

                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var t))
                        {
                            sb.Append(t.GetString());
                        }
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Collects url_citation annotations. These are the URLs the model
        /// actually retrieved, which is why they are trustworthy in a way that
        /// links written into the prose are not.
        /// </summary>
        private static List<Citation> ExtractCitations(JsonDocument doc)
        {
            var citations = new List<Citation>();
            Walk(doc.RootElement);
            return citations;

            void Walk(JsonElement el)
            {
                switch (el.ValueKind)
                {
                    case JsonValueKind.Object:
                        if (el.TryGetProperty("url", out var url) &&
                            url.ValueKind == JsonValueKind.String)
                        {
                            var link = url.GetString();
                            if (!string.IsNullOrWhiteSpace(link) &&
                                link.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                                !citations.Any(c => c.Url == link))
                            {
                                var title = el.TryGetProperty("title", out var t)
                                    ? t.GetString() ?? link
                                    : link;

                                citations.Add(new Citation(link, title));
                            }
                        }

                        foreach (var prop in el.EnumerateObject()) Walk(prop.Value);
                        break;

                    case JsonValueKind.Array:
                        foreach (var item in el.EnumerateArray()) Walk(item);
                        break;
                }
            }
        }
    }
}
