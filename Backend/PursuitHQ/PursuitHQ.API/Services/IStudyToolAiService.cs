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
        /// <summary>
        /// Writes a practice test from source material.
        ///
        /// <paramref name="styleRequest"/> is what the student asked for in
        /// their own words - "free response", "all multiple choice", "mix of
        /// true/false and short answer". Passed through rather than parsed
        /// into an enum, because a model reads "mostly multiple choice but a
        /// couple of harder written ones" perfectly well and a dropdown never
        /// will.
        /// </summary>
        Task<GeneratedTest> GenerateTestAsync(
            string sourceText, string topic, int count, string? styleRequest,
            CancellationToken ct = default);

        /// <summary>
        /// Judges written answers against the answer key.
        ///
        /// Batched into one call: grading ten answers one at a time would be
        /// ten requests against the rate limit for no benefit.
        /// </summary>
        Task<IReadOnlyList<GradedAnswer>> GradeWrittenAnswersAsync(
            IReadOnlyList<AnswerToGrade> answers, CancellationToken ct = default);
    }

    public record GeneratedFlashcard(string Front, string Back);

    public record GeneratedTest(string Title, IReadOnlyList<GeneratedQuestion> Questions);

    /// <param name="Type">multiple_choice, true_false, or short_answer.</param>
    /// <param name="Options">Only for multiple choice.</param>
    public record GeneratedQuestion(
        string Type,
        string Question,
        IReadOnlyList<string>? Options,
        string Answer,
        string? Explanation);

    public record AnswerToGrade(int Index, string Question, string CorrectAnswer, string GivenAnswer);

    public record GradedAnswer(int Index, bool Correct, string? Feedback);

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
