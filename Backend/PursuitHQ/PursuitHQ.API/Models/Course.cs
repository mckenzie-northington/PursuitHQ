namespace PursuitHQ.API.Models
{
    /// <summary>A course the student is taking in a given semester.</summary>
    public class Course
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Professor { get; set; } = string.Empty;
        public string Semester { get; set; } = string.Empty;

        public int? CreditHours { get; set; }

        /// <summary>Used to color-code this course on the calendar, e.g. "#3B82F6".</summary>
        public string? ColorHex { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ClassSchedule> ClassSchedules { get; set; } = new List<ClassSchedule>();
        public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
        public ICollection<MaterialFolder> MaterialFolders { get; set; } = new List<MaterialFolder>();
        public ICollection<StudyMaterial> StudyMaterials { get; set; } = new List<StudyMaterial>();
        public ICollection<Note> Notes { get; set; } = new List<Note>();
    }
}
