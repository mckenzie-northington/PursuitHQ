namespace PursuitHQ.API.Services
{
    public class JobSearchOptions
    {
        public const string SectionName = "JobSearch";

        /// <summary>Adzuna app id, from developer.adzuna.com. Free and instant.</summary>
        public string AppId { get; set; } = string.Empty;

        public string AppKey { get; set; } = string.Empty;

        /// <summary>Country code: us, gb, ca, au, and others.</summary>
        public string Country { get; set; } = "us";

        public int ResultsPerPage { get; set; } = 20;

        /// <summary>
        /// How long a search response is cached. The free tier allows a few
        /// hundred calls a day and students re-run the same searches.
        /// </summary>
        public int CacheMinutes { get; set; } = 10;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(AppKey);
    }
}
