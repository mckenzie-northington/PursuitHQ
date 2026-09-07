namespace PursuitHQ.API.DTOs.JobSearch
{
    public class JobSearchResponseDto
    {
        public List<JobSearchResultDto> Results { get; set; } = new();
        public int Page { get; set; }
        public long TotalResults { get; set; }
    }
}
