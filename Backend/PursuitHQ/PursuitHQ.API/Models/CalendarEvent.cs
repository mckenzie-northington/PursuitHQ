namespace PursuitHQ.API.Models
{
    /// <summary>
    /// Anything on the calendar that is not a class meeting, an assignment due
    /// date, or a study session: work shifts, club meetings, appointments.
    /// </summary>
    public class CalendarEvent
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }

        /// <summary>
        /// True for events with no meaningful time of day, such as "Spring
        /// break" or "Tuition due". The times are still stored - start is
        /// midnight and end is 23:59:59 on the last day - so range queries
        /// keep working without a special case.
        /// </summary>
        public bool IsAllDay { get; set; }

        public string? Location { get; set; }

        public EventType EventType { get; set; } = EventType.Other;

        public bool IsRecurring { get; set; }

        /// <summary>iCal RRULE string when recurring, e.g. "FREQ=WEEKLY;BYDAY=TU,TH".</summary>
        public string? RecurrenceRule { get; set; }

        public string? ColorHex { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
