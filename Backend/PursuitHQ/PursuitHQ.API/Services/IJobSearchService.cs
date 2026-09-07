using PursuitHQ.API.DTOs.JobSearch;

namespace PursuitHQ.API.Services
{
    /// <summary>
    /// Searches an external job board. Wrapped behind an interface like file
    /// storage and AI, so the provider can change without touching controllers.
    /// </summary>
    public interface IJobSearchService
    {
        bool IsConfigured { get; }

        Task<JobSearchResponseDto> SearchAsync(
            string query, string? location, string? contractTime, int page = 1,
            CancellationToken ct = default);
    }
}
