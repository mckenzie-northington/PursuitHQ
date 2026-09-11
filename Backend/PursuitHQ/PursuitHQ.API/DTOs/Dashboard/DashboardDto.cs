using PursuitHQ.API.DTOs.Calendar;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.DTOs.Dashboard
{
    /// <summary>
    /// Everything the home page shows, in one response.
    ///
    /// One request rather than six: the dashboard reads from five different
    /// tables, and six round trips on the page you open most often is the
    /// difference between the app feeling instant and feeling slow.
    /// </summary>
    public class DashboardDto
    {
        public string FirstName { get; set; } = string.Empty;

        /// <summary>The student's own date, not the server's idea of today.</summary>
        public DateOnly Today { get; set; }

        /// <summary>Classes, events and study sessions happening today.</summary>
        public List<CalendarItemDto> TodaysSchedule { get; set; } = new();

        /// <summary>Due in the next seven days, plus anything already late.</summary>
        public List<DashboardAssignmentDto> DueSoon { get; set; } = new();

        public List<DashboardCourseDto> Courses { get; set; } = new();

        public List<DashboardDeckDto> RecentDecks { get; set; } = new();
        public List<DashboardTestDto> RecentTests { get; set; } = new();

        public DashboardCountsDto Counts { get; set; } = new();
    }

    public class DashboardAssignmentDto
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string? ColorHex { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public AssignmentStatus Status { get; set; }
        public bool IsOverdue { get; set; }
    }

    public class DashboardCourseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ColorHex { get; set; }
        public string Professor { get; set; } = string.Empty;
        public int OpenAssignments { get; set; }
    }

    public class DashboardDeckDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? CourseName { get; set; }
        public int CardCount { get; set; }

        /// <summary>
        /// Percent correct across every review of every card, or null if the
        /// deck has never been studied. Null and 0% mean very different things.
        /// </summary>
        public int? Accuracy { get; set; }
    }

    public class DashboardTestDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? CourseName { get; set; }
        public int QuestionCount { get; set; }
        public int AttemptCount { get; set; }
        public int? BestScore { get; set; }
    }

    public class DashboardCountsDto
    {
        public int Courses { get; set; }
        public int OpenAssignments { get; set; }
        public int Overdue { get; set; }
        public int StudyTools { get; set; }
    }
}
