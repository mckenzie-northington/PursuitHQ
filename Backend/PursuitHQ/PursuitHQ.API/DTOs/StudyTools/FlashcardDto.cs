namespace PursuitHQ.API.DTOs.StudyTools
{
    public class FlashcardDto
    {
        public int Id { get; set; }
        public string Front { get; set; } = string.Empty;
        public string Back { get; set; } = string.Empty;
        public int Order { get; set; }

        public int TimesReviewed { get; set; }
        public int TimesCorrect { get; set; }
    }
}
