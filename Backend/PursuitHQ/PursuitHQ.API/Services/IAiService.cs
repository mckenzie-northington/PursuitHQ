namespace PursuitHQ.API.Services
{
    /// <summary>
    /// The only place the app talks to an AI provider. Features depend on this,
    /// never on Gemini directly, so the provider can be swapped in one place.
    /// </summary>
    public interface IAiService
    {
        bool IsConfigured { get; }

        /// <summary>Plain completion, no external tools.</summary>
        Task<string> CompleteAsync(string prompt, string? model = null, CancellationToken ct = default);

        /// <summary>
        /// Completion grounded in Google Search. Returns the text plus the
        /// citations the model actually used - those carry real URLs from real
        /// search results, unlike any link the model writes into its prose.
        /// </summary>
        Task<GroundedResult> CompleteWithSearchAsync(
            string prompt, string? model = null, CancellationToken ct = default);
    }

    public record Citation(string Url, string Title);

    public record GroundedResult(string Text, IReadOnlyList<Citation> Citations);
}
