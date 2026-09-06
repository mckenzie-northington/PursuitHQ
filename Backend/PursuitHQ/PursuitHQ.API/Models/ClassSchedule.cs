namespace PursuitHQ.API.Models
{
    /// <summary>
    /// One recurring weekly meeting time for a course. A course that meets
    /// Monday and Wednesday has two ClassSchedule rows.
    /// </summary>
    public class ClassSchedule
    {
        public int Id { get; set; }

        public int CourseId { get; set; }
        public Course? Course { get; set; }

        public DayOfWeek DayOfWeek { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }

        public string? Location { get; set; }
    }
}
