namespace PursuitHQ.API.Services
{
    /// <summary>
    /// Pulls plain text out of uploaded documents.
    ///
    /// Used for two things: showing a readable preview of file types the
    /// browser cannot render, and (later) feeding uploaded material to the AI
    /// study tools that generate flashcards, quizzes, and study guides.
    /// </summary>
    public interface ITextExtractionService
    {
        /// <summary>True if this file type can have text pulled out of it.</summary>
        bool CanExtract(string fileName, string contentType);

        /// <summary>
        /// Extracts text. Returns the text plus whether it was truncated, so
        /// callers can tell the user only part of the document was used.
        /// </summary>
        Task<TextExtractionResult> ExtractAsync(
            Stream file, string fileName, string contentType, int maxCharacters = 100_000,
            CancellationToken ct = default);
    }

    public record TextExtractionResult(string Text, bool Truncated, int SectionCount)
    {
        public bool IsEmpty => string.IsNullOrWhiteSpace(Text);
    }
}
