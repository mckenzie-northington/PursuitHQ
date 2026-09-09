using System.ComponentModel.DataAnnotations;

namespace PursuitHQ.API.DTOs.StudyTools
{
    public class SaveFlashcardDto
    {
        [Required, MaxLength(1000)]
        public string Front { get; set; } = string.Empty;

        [Required, MaxLength(4000)]
        public string Back { get; set; } = string.Empty;
    }

    public class RenameDeckDto
    {
        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;
    }

    /// <summary>One card's outcome during review.</summary>
    public class ReviewFlashcardDto
    {
        public bool Correct { get; set; }
    }
}
