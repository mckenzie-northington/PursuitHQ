using System.ComponentModel.DataAnnotations;

namespace PursuitHQ.API.DTOs.Materials
{
    public class UpdateMaterialDto
    {
        [Required, MaxLength(300)]
        public string FileName { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        /// <summary>Move to a different folder. Null moves it to the course root.</summary>
        public int? FolderId { get; set; }
    }
}
