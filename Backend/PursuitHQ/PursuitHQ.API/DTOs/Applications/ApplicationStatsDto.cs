namespace PursuitHQ.API.DTOs.Applications
{
    /// <summary>Pipeline counts for the board view and the dashboard.</summary>
    public class ApplicationStatsDto
    {
        public int Saved { get; set; }
        public int Applied { get; set; }
        public int Interview { get; set; }
        public int Offer { get; set; }
        public int Rejected { get; set; }

        public int Total { get; set; }

        /// <summary>
        /// Percentage of submitted applications that reached an interview.
        /// Saved-but-not-applied entries are excluded, since they were never sent.
        /// </summary>
        public double InterviewRate { get; set; }
    }
}
