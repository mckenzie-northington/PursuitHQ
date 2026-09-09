namespace PursuitHQ.API.DTOs.StudyTools
{
    /// <summary>A deck in a list. Carries the count, not the cards.</summary>
    public class FlashcardDeckSummaryDto
    {
        public int Id { get; set; }
        public int? CourseId { get; set; }
        public string? CourseName { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsAiGenerated { get; set; }
        public int CardCount { get; set; }

        /// <summary>The file or note this came from, for showing where it started.</summary>
        public string? SourceName { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    /// <summary>One deck with everything in it.</summary>
    public class FlashcardDeckDto : FlashcardDeckSummaryDto
    {
        public List<FlashcardDto> Cards { get; set; } = new();
    }
}
