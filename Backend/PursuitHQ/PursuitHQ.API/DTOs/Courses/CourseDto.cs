namespace PursuitHQ.API.DTOs.Courses
{
    public class CourseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Professor { get; set; } = string.Empty;
        public string Semester { get; set; } = string.Empty;
        public int? CreditHours { get; set; }
        public string? ColorHex { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<ClassScheduleDto> Schedules { get; set; } = new();
        public int AssignmentCount { get; set; }
    }
}
