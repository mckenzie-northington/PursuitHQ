namespace PursuitHQ.API.Services
{
    /// <summary>
    /// Turns course material into study tools.
    ///
    /// Separate from <see cref="IAiService"/> on purpose: that one knows how to
    /// talk to Gemini, this one knows what to ask it and what a usable answer
    /// looks like. Prompt wording and answer validation change far more often
    /// than the transport does.
    /// </summary>
    public interface IStudyToolAiService
    {
        bool IsConfigured { get; }

        /// <summary>
        /// Writes flashcards from a piece of source material.
        ///
        /// Returns only cards that survived validation. Throws
        /// <see cref="StudyToolException"/> when nothing usable came back, so
        /// the caller never has to decide whether a half-empty list is worth
        /// saving.
        /// </summary>
        Task<List<GeneratedFlashcard>> GenerateFlashcardsAsync(
            string sourceText, string topic, int count, CancellationToken ct = default);
    }

    public record GeneratedFlashcard(string Front, string Back);

    /// <summary>
    /// The AI produced something we cannot use. Carries a message written for
    /// the student, not a stack trace.
    /// </summary>
    public class StudyToolException : Exception
    {
        public StudyToolException(string message) : base(message) { }
        public StudyToolException(string message, Exception inner) : base(message, inner) { }
    }
}
