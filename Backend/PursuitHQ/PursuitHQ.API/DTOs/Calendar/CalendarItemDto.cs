namespace PursuitHQ.API.DTOs.Calendar
{
    /// <summary>What kind of thing this calendar entry is.</summary>
    public enum CalendarItemType
    {
        Class = 0,
        Assignment = 1,
        StudySession = 2,
        Event = 3,
        Reminder = 4
    }

    /// <summary>
    /// One entry on the calendar. Four different tables feed this shape, so
    /// the frontend renders a single list instead of merging sources itself.
    /// </summary>
    public class CalendarItemDto
    {
        /// <summary>Unique across sources, e.g. "class-12" or "assignment-3".</summary>
        public string Id { get; set; } = string.Empty;

        public CalendarItemType Type { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }

        public DateOnly Date { get; set; }

        /// <summary>
        /// The time of day, when there is one. All-day items still carry a
        /// start time when it means something - an assignment due at 11:59pm
        /// is all-day on the calendar but the deadline is worth showing.
        /// </summary>
        public TimeOnly? StartTime { get; set; }
        public TimeOnly? EndTime { get; set; }

        /// <summary>
        /// True when this belongs in the strip above the hour grid rather than
        /// in a time slot: assignment due dates, and events marked all-day.
        /// </summary>
        public bool IsAllDay { get; set; }

        public string? Location { get; set; }
        public string? ColorHex { get; set; }

        /// <summary>The source record, so the UI can link to it.</summary>
        public int? CourseId { get; set; }
        public int? SourceId { get; set; }

        /// <summary>Assignment or study session status, where it applies.</summary>
        public string? Status { get; set; }
        public bool IsOverdue { get; set; }
    }
}
