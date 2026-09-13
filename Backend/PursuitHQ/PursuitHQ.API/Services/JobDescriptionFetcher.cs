using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace PursuitHQ.API.Services
{
    public record FetchedPage(bool Ok, string Text, string? Error);

    public interface IJobDescriptionFetcher
    {
        /// <summary>
        /// Downloads a job posting and returns its readable text.
        ///
        /// Never throws, and never returns half a result: a page that cannot be
        /// read comes back as Ok=false with something worth showing the student,
        /// because the answer is always "paste the text instead".
        /// </summary>
        Task<FetchedPage> FetchAsync(string url, CancellationToken ct = default);
    }

    /// <summary>
    /// Fetches a job posting from a URL.
    ///
    /// Two things make this harder than it looks.
    ///
    /// The first is safety. This takes a URL from a user and asks the server to
    /// open it, which is the exact shape of a server-side request forgery: point
    /// it at 127.0.0.1, or at a cloud provider's metadata address, and the
    /// server would happily fetch something only it can reach and hand back the
    /// contents. So every address is resolved and checked before anything is
    /// opened, and redirects are followed by hand so each hop is checked too -
    /// a public URL that redirects to localhost is the usual way past a naive
    /// check.
    ///
    /// The second is that plenty of job boards will not work. Workday,
    /// Greenhouse, LinkedIn and Indeed render postings in the browser or block
    /// anything that is not one, so what comes back is a login wall or an empty
    /// shell. That is not fixable from here, which is why pasting the text is
    /// offered alongside and why failure says so plainly.
    /// </summary>
    public class JobDescriptionFetcher : IJobDescriptionFetcher
    {
        /// <summary>A job posting past this is a page that is not a job posting.</summary>
        private const int MaxBytes = 2 * 1024 * 1024;

        private const int MaxRedirects = 5;

        /// <summary>Below this there is nothing worth matching against.</summary>
        private const int MinUsefulCharacters = 200;

        private readonly HttpClient _http;
        private readonly ILogger<JobDescriptionFetcher> _logger;

        public JobDescriptionFetcher(HttpClient http, ILogger<JobDescriptionFetcher> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<FetchedPage> FetchAsync(string url, CancellationToken ct = default)
        {
            var current = url?.Trim() ?? string.Empty;

            if (!current.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !current.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                current = "https://" + current;
            }

            try
            {
                for (var hop = 0; hop <= MaxRedirects; hop++)
                {
                    if (!Uri.TryCreate(current, UriKind.Absolute, out var uri))
                    {
                        return Fail("That does not look like a web address.");
                    }

                    var refusal = await CheckAddressAsync(uri, ct);
                    if (refusal is not null) return Fail(refusal);

                    using var request = new HttpRequestMessage(HttpMethod.Get, uri);

                    // Some sites serve nothing useful without these. It is not a
                    // disguise - the request is exactly what it looks like - but
                    // a missing User-Agent is enough for many to refuse outright.
                    request.Headers.TryAddWithoutValidation(
                        "User-Agent",
                        "Mozilla/5.0 (compatible; PursuitHQ/1.0; +https://pursuit-hq.com)");
                    request.Headers.TryAddWithoutValidation("Accept", "text/html,text/plain;q=0.9");

                    using var response = await _http.SendAsync(
                        request, HttpCompletionOption.ResponseHeadersRead, ct);

                    if (IsRedirect(response.StatusCode))
                    {
                        var location = response.Headers.Location;
                        if (location is null) return Fail("That page redirected somewhere unreadable.");

                        // Re-checked on the next pass, which is the point of
                        // following these by hand.
                        current = location.IsAbsoluteUri
                            ? location.ToString()
                            : new Uri(uri, location).ToString();

                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        return Fail(
                            $"That page returned {(int)response.StatusCode}. Many job boards block "
                            + "automated readers - copying the text in works every time.");
                    }

                    var mediaType = response.Content.Headers.ContentType?.MediaType ?? "";

                    if (!mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)
                        && !mediaType.Contains("text", StringComparison.OrdinalIgnoreCase))
                    {
                        return Fail(
                            "That link is not a web page. If the posting is a PDF, upload the file instead.");
                    }

                    var html = await ReadCappedAsync(response, ct);
                    var text = ToPlainText(html);

                    if (text.Length < MinUsefulCharacters)
                    {
                        return Fail(
                            "That page loaded but had almost no readable text. Job boards that build "
                            + "the posting in your browser look empty from here - paste the text in instead.");
                    }

                    return new FetchedPage(true, text, null);
                }

                return Fail("That page redirected too many times.");
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                return Fail("That page took too long to respond.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogInformation(ex, "Could not fetch job description from {Url}", url);
                return Fail("That page could not be reached.");
            }
        }

        private static FetchedPage Fail(string error) => new(false, string.Empty, error);

        private static bool IsRedirect(HttpStatusCode status) =>
            (int)status is 301 or 302 or 303 or 307 or 308;

        /// <summary>
        /// Refuses anything that is not a public address.
        ///
        /// The host is resolved first and every address it answers with is
        /// checked, because a name that looks ordinary can point anywhere - and
        /// pointing it at the server's own network is the whole attack.
        /// </summary>
        private async Task<string?> CheckAddressAsync(Uri uri, CancellationToken ct)
        {
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                return "Only http and https links can be read.";
            }

            IPAddress[] addresses;
            try
            {
                addresses = IPAddress.TryParse(uri.Host, out var literal)
                    ? new[] { literal }
                    : await Dns.GetHostAddressesAsync(uri.Host, ct);
            }
            catch (SocketException)
            {
                return "That address could not be found.";
            }

            if (addresses.Length == 0) return "That address could not be found.";

            foreach (var address in addresses)
            {
                if (IsPrivate(address)) return "That address is not a public website.";
            }

            return null;
        }

        private static bool IsPrivate(IPAddress address)
        {
            if (IPAddress.IsLoopback(address)) return true;

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = address.GetAddressBytes();

                return b[0] switch
                {
                    10 => true,                                   // 10.0.0.0/8
                    127 => true,                                  // loopback
                    0 => true,                                    // "this network"
                    169 when b[1] == 254 => true,                 // link-local, incl. cloud metadata
                    172 when b[1] >= 16 && b[1] <= 31 => true,    // 172.16.0.0/12
                    192 when b[1] == 168 => true,                 // 192.168.0.0/16
                    _ => b[0] >= 224                              // multicast and reserved
                };
            }

            return address.IsIPv6LinkLocal
                   || address.IsIPv6SiteLocal
                   || address.IsIPv6UniqueLocal
                   || address.Equals(IPAddress.IPv6Any);
        }

        /// <summary>Reads at most MaxBytes, so a huge page cannot exhaust memory.</summary>
        private static async Task<string> ReadCappedAsync(HttpResponseMessage response, CancellationToken ct)
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);

            var buffer = new byte[8192];
            var builder = new MemoryStream();

            int read;
            while (builder.Length < MaxBytes
                   && (read = await stream.ReadAsync(buffer, ct)) > 0)
            {
                builder.Write(buffer, 0, read);
            }

            return Encoding.UTF8.GetString(builder.ToArray());
        }

        /// <summary>
        /// Strips HTML down to the words.
        ///
        /// Regex rather than a parser: the goal is not a faithful document tree,
        /// it is a block of prose to hand to a model. Script and style blocks go
        /// first - their contents are text too, and a page's JavaScript would
        /// otherwise drown the posting itself.
        /// </summary>
        public static string ToPlainText(string html)
        {
            var text = Regex.Replace(html, @"<(script|style|noscript|svg)\b[^>]*>.*?</\1>",
                " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            text = Regex.Replace(text, @"<!--.*?-->", " ", RegexOptions.Singleline);

            // Block-level tags become line breaks so lists and paragraphs do not
            // run together into one wall of words.
            text = Regex.Replace(text, @"<(br|/p|/div|/li|/h[1-6]|/tr)\s*/?>", "\n",
                RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<li\b[^>]*>", "\n- ", RegexOptions.IgnoreCase);

            text = Regex.Replace(text, @"<[^>]+>", " ");
            text = WebUtility.HtmlDecode(text);

            text = Regex.Replace(text, @"[ \t\f\v]+", " ");
            text = Regex.Replace(text, @"\n\s*\n\s*\n+", "\n\n");
            text = Regex.Replace(text, @"^[ \t]+|[ \t]+$", "", RegexOptions.Multiline);

            return text.Trim();
        }
    }
}
