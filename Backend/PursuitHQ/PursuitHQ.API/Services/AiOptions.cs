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

        /// <summary>
        /// Per-user daily cap.
        ///
        /// Low on purpose. The AI features are the only part of PursuitHQ that
        /// costs money per use, and it is paid for out of one person's pocket.
        ///
        /// Read what this is not: counting happens in memory, so a restart
        /// clears it, and there is no ceiling across users at all - twenty
        /// people at five each is a hundred requests nobody capped. The control
        /// that actually bounds the bill is the requests-per-day quota set on
        /// the Generative Language API in Google Cloud. Set that too, and treat
        /// this number as courtesy rather than protection.
        /// </summary>
        public int RequestsPerUserPerDay { get; set; } = 5;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
    }
}
