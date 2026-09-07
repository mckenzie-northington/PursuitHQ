namespace PursuitHQ.API.DTOs.Materials
{
    public class FolderDto
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? ParentFolderId { get; set; }
        public DateTime CreatedAt { get; set; }

        public int FileCount { get; set; }
        public int NoteCount { get; set; }
        public int SubfolderCount { get; set; }
    }
}
