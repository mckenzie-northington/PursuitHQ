namespace PursuitHQ.API.Models
{
    /// <summary>
    /// A job posting the student kept, with the score their resume got against it.
    ///
    /// The posting text is stored alongside the link on purpose. Postings come
    /// down - the link is dead within weeks of the role closing - and without
    /// the text there is no way to see what a saved score was even measuring, or
    /// to run it again after editing the resume.
    /// </summary>
    public class SavedJob
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Company { get; set; }

        /// <summary>Where it came from, when a link was used rather than pasted text.</summary>
        public string? Url { get; set; }

        /// <summary>The posting itself, as it was read.</summary>
        public string PostingText { get; set; } = string.Empty;

        /// <summary>The percentage at the moment it was saved.</summary>
        public int Score { get; set; }

        /// <summary>
        /// The whole match - requirements, evidence, suggestions - as JSON.
        ///
        /// Kept whole rather than split into its own table: it is written once,
        /// read whole, and never queried across. A RequirementMatch table would
        /// be four files and a foreign key to store what is only ever shown as
        /// one block.
        /// </summary>
        public string MatchJson { get; set; } = string.Empty;

        /// <summary>The resume it was scored against, if it still exists.</summary>
        public int? ResumeId { get; set; }
        public Resume? Resume { get; set; }

        public string? Notes { get; set; }

        public DateTime SavedAt { get; set; } = DateTime.UtcNow;
    }
}
