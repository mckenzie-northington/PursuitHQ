namespace PursuitHQ.API.DTOs.JobSearch
{
    /// <summary>One posting returned from the external job board.</summary>
    public class JobSearchResultDto
    {
        /// <summary>The provider's id. Stored on save so duplicates can be detected.</summary>
        public string ExternalId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public decimal? SalaryMin { get; set; }
        public decimal? SalaryMax { get; set; }

        /// <summary>e.g. "full_time" or "part_time", when the provider says.</summary>
        public string? ContractTime { get; set; }

        public DateTime? PostedAt { get; set; }

        /// <summary>The real posting. The Apply button opens this; nothing is submitted from here.</summary>
        public string ApplyUrl { get; set; } = string.Empty;

        /// <summary>True when this posting is already in the student's tracker.</summary>
        public bool AlreadySaved { get; set; }
    }
}
