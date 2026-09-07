namespace PursuitHQ.API.DTOs.Materials
{
    public class NoteDto
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public int? FolderId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
