using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PursuitHQ.API.DTOs.JobSearch;

namespace PursuitHQ.API.Services
{
    /// <summary>
    /// Job search backed by Adzuna: a real aggregator feed, free tier, working
    /// links to live postings.
    ///
    /// LinkedIn and Indeed do not offer open job-search APIs and scraping them
    /// breaks their terms, which is why this uses an aggregator rather than the
    /// sites students name first.
    /// </summary>
    public class AdzunaJobSearchService : IJobSearchService
    {
        private readonly HttpClient _http;
        private readonly JobSearchOptions _options;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AdzunaJobSearchService> _logger;

        public AdzunaJobSearchService(
            HttpClient http,
            IOptions<JobSearchOptions> options,
            IMemoryCache cache,
            ILogger<AdzunaJobSearchService> logger)
        {
            _http = http;
            _options = options.Value;
            _cache = cache;
            _logger = logger;
        }

        public bool IsConfigured => _options.IsConfigured;

        public async Task<JobSearchResponseDto> SearchAsync(
            string query, string? location, string? contractTime, int page = 1,
            CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException(
                    "Job search is not configured. Set JobSearch:AppId and JobSearch:AppKey.");
            }

            if (page < 1) page = 1;

            // Adzuna's contract_time filter only knows full_time and part_time.
            // "Internship" is not a contract type there, so it is folded into
            // the search term instead - which is what actually narrows results.
            var effectiveQuery = query;
            if (contractTime == "internship"
                && !query.Contains("intern", StringComparison.OrdinalIgnoreCase))
            {
                effectiveQuery = query + " internship";
            }

            var cacheKey = $"jobs:{_options.Country}:{effectiveQuery}:{location}:{contractTime}:{page}";

            if (_cache.TryGetValue(cacheKey, out JobSearchResponseDto? cached) && cached is not null)
            {
                return cached;
            }

            // Adzuna authenticates with query-string credentials rather than a
            // header, so this URL must never be logged.
            var url =
                $"https://api.adzuna.com/v1/api/jobs/{_options.Country}/search/{page}" +
                $"?app_id={Uri.EscapeDataString(_options.AppId)}" +
                $"&app_key={Uri.EscapeDataString(_options.AppKey)}" +
                $"&results_per_page={_options.ResultsPerPage}" +
                $"&what={Uri.EscapeDataString(effectiveQuery)}";

            if (!string.IsNullOrWhiteSpace(location))
            {
                url += $"&where={Uri.EscapeDataString(location)}";
            }

            if (contractTime is "full_time" or "part_time")
            {
                url += $"&{contractTime}=1";
            }

            using var response = await _http.GetAsync(url, ct);

            // Adzuna sends a Content-Type whose charset .NET refuses to parse,
            // and ReadAsStringAsync throws on it before returning any content.
            // Reading the raw bytes and decoding as UTF-8 sidesteps the header
            // entirely - the body itself is ordinary UTF-8 JSON.
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            var json = System.Text.Encoding.UTF8.GetString(bytes);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Adzuna returned {Status} for query {Query}: {Body}",
                    response.StatusCode, query, json.Length > 300 ? json[..300] : json);

                var hint = ((int)response.StatusCode is 401 or 403)
                    ? "Check that your Adzuna app id and key are correct."
                    : "Try again in a moment.";

                throw new HttpRequestException(
                    $"The job board returned {(int)response.StatusCode}. {hint}");
            }

            JobSearchResponseDto result;
            try
            {
                result = Parse(json, page);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not parse Adzuna response: {Snippet}",
                    json.Length > 500 ? json[..500] : json);

                throw new InvalidOperationException(
                    "The job board returned data in an unexpected format.", ex);
            }

            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(_options.CacheMinutes));

            return result;
        }

        private static JobSearchResponseDto Parse(string json, int page)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var response = new JobSearchResponseDto
            {
                Page = page,
                // The count can come back as a floating-point number, which
                // GetInt64 refuses, so read it as a double and convert.
                TotalResults = root.TryGetProperty("count", out var count)
                    && count.ValueKind == JsonValueKind.Number
                        ? (long)count.GetDouble()
                        : 0
            };

            if (!root.TryGetProperty("results", out var results)
                || results.ValueKind != JsonValueKind.Array)
            {
                return response;
            }

            foreach (var item in results.EnumerateArray())
            {
                response.Results.Add(new JobSearchResultDto
                {
                    ExternalId = Str(item, "id"),
                    Title = Str(item, "title"),
                    Company = item.TryGetProperty("company", out var c) ? Str(c, "display_name") : "",
                    Location = item.TryGetProperty("location", out var l) ? Str(l, "display_name") : "",
                    Description = Str(item, "description"),
                    SalaryMin = Dec(item, "salary_min"),
                    SalaryMax = Dec(item, "salary_max"),
                    ContractTime = item.TryGetProperty("contract_time", out var ctime)
                        && ctime.ValueKind == JsonValueKind.String
                            ? ctime.GetString()
                            : null,
                    PostedAt = item.TryGetProperty("created", out var created)
                        && created.ValueKind == JsonValueKind.String
                        && DateTime.TryParse(created.GetString(), out var dt)
                            ? dt
                            : null,
                    ApplyUrl = Str(item, "redirect_url")
                });
            }

            return response;
        }

        private static string Str(JsonElement el, string name) =>
            el.TryGetProperty(name, out var v)
                ? v.ValueKind switch
                {
                    JsonValueKind.String => v.GetString() ?? "",
                    JsonValueKind.Number => v.ToString(),
                    _ => ""
                }
                : "";

        private static decimal? Dec(JsonElement el, string name) =>
            el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
                ? v.GetDecimal()
                : null;
    }
}
