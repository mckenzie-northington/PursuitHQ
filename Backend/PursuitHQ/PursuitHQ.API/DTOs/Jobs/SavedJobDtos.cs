using System.ComponentModel.DataAnnotations;
using PursuitHQ.API.DTOs.Resumes;

namespace PursuitHQ.API.DTOs.Jobs
{
    /// <summary>One row in the saved jobs list.</summary>
    public class SavedJobSummaryDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Company { get; set; }
        public string? Url { get; set; }
        public int Score { get; set; }
        public string? ResumeTitle { get; set; }
        public DateTime SavedAt { get; set; }
    }

    /// <summary>A saved job opened up: the full match, and the posting it was scored against.</summary>
    public class SavedJobDto : SavedJobSummaryDto
    {
        public string PostingText { get; set; } = string.Empty;
        public string? Notes { get; set; }

        /// <summary>
        /// The match as it stood when saved, overrides included.
        ///
        /// Null when the stored JSON cannot be read - an old row, or one written
        /// by an earlier shape of this feature. A job whose breakdown will not
        /// open should still show its title, its link and its score rather than
        /// failing the whole page.
        /// </summary>
        public JobMatchDto? Match { get; set; }
    }

    public class SaveJobDto
    {
        [Required(ErrorMessage = "Give this job a title.")]
        [MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Company { get; set; }

        [MaxLength(2000)]
        public string? Url { get; set; }

        [Required]
        public string PostingText { get; set; } = string.Empty;

        [Range(0, 100)]
        public int Score { get; set; }

        /// <summary>The match to keep, sent back as the page has it - overrides and all.</summary>
        public JobMatchDto? Match { get; set; }

        public int? ResumeId { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }
    }
}
