using System.ComponentModel.DataAnnotations;

namespace PursuitHQ.API.DTOs.Materials
{
    public class SaveNoteDto
    {
        [Required, MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public int? FolderId { get; set; }
    }
}
