namespace PursuitHQ.API.Models
{
    /// <summary>
    /// An ongoing study conversation, scoped to one course.
    ///
    /// One per course rather than one per question: the point is that it
    /// remembers what you already covered, so "quiz me on the rest" means
    /// something.
    /// </summary>
    public class StudyConversation
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public int CourseId { get; set; }
        public Course? Course { get; set; }

        public string Title { get; set; } = "Study session";

        /// <summary>
        /// Which uploaded files are in context, as comma-separated ids.
        ///
        /// A joined string rather than a join table: this list is always read
        /// and written whole, is never queried against, and belongs to exactly
        /// one conversation.
        /// </summary>
        public string? SourceMaterialIds { get; set; }

        /// <summary>Which notes are in context, same shape.</summary>
        public string? SourceNoteIds { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<StudyMessage> Messages { get; set; } = new List<StudyMessage>();
    }
}
