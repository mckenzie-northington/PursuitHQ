using System.ComponentModel.DataAnnotations;

namespace PursuitHQ.API.DTOs.StudyTools
{
    public class GenerateFlashcardsDto
    {
        [Required]
        public int CourseId { get; set; }

        /// <summary>An uploaded file to generate from. Mutually exclusive with SourceNoteId.</summary>
        public int? SourceMaterialId { get; set; }

        /// <summary>A typed note to generate from. Mutually exclusive with SourceMaterialId.</summary>
        public int? SourceNoteId { get; set; }

        /// <summary>How many cards to ask for. Clamped server-side to 5-40.</summary>
        public int Count { get; set; } = 15;

        /// <summary>Optional. Defaults to the name of the file or note.</summary>
        [MaxLength(200)]
        public string? Title { get; set; }
    }
}
