namespace PursuitHQ.API.Models
{
    /// <summary>One turn in a study conversation.</summary>
    public class StudyMessage
    {
        public int Id { get; set; }

        public int ConversationId { get; set; }
        public StudyConversation? Conversation { get; set; }

        public StudyMessageRole Role { get; set; }

        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Set when this reply contained something worth keeping - a study
        /// guide, a practice test.
        /// </summary>
        public StudyArtifactKind ArtifactKind { get; set; } = StudyArtifactKind.None;

        public string? ArtifactTitle { get; set; }

        /// <summary>
        /// The artifact itself, held here until the student saves it.
        ///
        /// Kept on the message rather than written straight to StudyGuides so
        /// the library only ever contains things that were deliberately kept.
        /// Everything else stays in the conversation where it was produced.
        /// </summary>
        public string? ArtifactContent { get; set; }

        /// <summary>Set once saved, so the button can say "Saved" instead of offering again.</summary>
        public int? SavedStudyGuideId { get; set; }
        public StudyGuide? SavedStudyGuide { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
