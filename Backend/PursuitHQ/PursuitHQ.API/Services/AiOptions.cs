namespace PursuitHQ.API.Services
{
    public class AiOptions
    {
        public const string SectionName = "Ai";

        /// <summary>Gemini API key from Google AI Studio. Server-side only.</summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>Model used for search-grounded work like job discovery.</summary>
        public string SearchModel { get; set; } = "gemini-3.8-flash";

        /// <summary>Model used for generating study tools.</summary>
        public string StudyToolModel { get; set; } = "gemini-3.8-flash";

        /// <summary>Model used for resume review, where quality matters most.</summary>
        public string ResumeModel { get; set; } = "gemini-3.8-flash";

        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>Per-user daily cap, so a runaway loop cannot run up a bill.</summary>
        public int RequestsPerUserPerDay { get; set; } = 30;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
    }
}
