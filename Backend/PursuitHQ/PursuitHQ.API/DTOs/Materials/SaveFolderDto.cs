using System.ComponentModel.DataAnnotations;

namespace PursuitHQ.API.DTOs.Materials
{
    public class SaveFolderDto
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Null means the folder sits at the course root.</summary>
        public int? ParentFolderId { get; set; }
    }
}
