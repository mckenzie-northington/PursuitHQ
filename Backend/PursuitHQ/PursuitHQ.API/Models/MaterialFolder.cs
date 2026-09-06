namespace PursuitHQ.API.Models
{
    /// <summary>
    /// A folder inside a course, used to organize study materials like a file
    /// system. Folders nest via ParentFolderId; null means the course root.
    /// </summary>
    public class MaterialFolder
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public int CourseId { get; set; }
        public Course? Course { get; set; }

        public string Name { get; set; } = string.Empty;

        public int? ParentFolderId { get; set; }
        public MaterialFolder? ParentFolder { get; set; }
        public ICollection<MaterialFolder> ChildFolders { get; set; } = new List<MaterialFolder>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<StudyMaterial> StudyMaterials { get; set; } = new List<StudyMaterial>();
        public ICollection<Note> Notes { get; set; } = new List<Note>();
    }
}
