namespace PursuitHQ.API.DTOs.Courses
{
    public class CourseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Professor { get; set; } = string.Empty;
        public string Semester { get; set; } = string.Empty;

        /// <summary>First and last day the course meets. Optional.</summary>
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public int? CreditHours { get; set; }
        public string? ColorHex { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<ClassScheduleDto> Schedules { get; set; } = new();
        public int AssignmentCount { get; set; }
    }
}
