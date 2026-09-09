using PursuitHQ.API.Models;

namespace PursuitHQ.API.DTOs.Calendar
{
    public class CalendarEventDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public bool IsAllDay { get; set; }
        public string? Location { get; set; }
        public EventType EventType { get; set; }
        public bool IsRecurring { get; set; }
        public string? RecurrenceRule { get; set; }
        public string? ColorHex { get; set; }
    }
}
