namespace PursuitHQ.API.DTOs.Courses
{
    public class ClassScheduleDto
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public string? Location { get; set; }
    }
}
