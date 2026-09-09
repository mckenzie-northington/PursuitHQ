using System.ComponentModel.DataAnnotations;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.DTOs.Calendar
{
    public class SaveCalendarEventDto
    {
        [Required, MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        [Required]
        public DateTime StartDateTime { get; set; }

        [Required]
        public DateTime EndDateTime { get; set; }

        /// <summary>
        /// When true the times sent are ignored and normalised to cover whole
        /// days: midnight on the start date through 23:59:59 on the end date.
        /// </summary>
        public bool IsAllDay { get; set; }

        [MaxLength(200)]
        public string? Location { get; set; }

        public EventType EventType { get; set; } = EventType.Other;

        public bool IsRecurring { get; set; }

        /// <summary>iCal RRULE, e.g. "FREQ=WEEKLY;BYDAY=TU,TH".</summary>
        [MaxLength(200)]
        public string? RecurrenceRule { get; set; }

        [MaxLength(9)]
        public string? ColorHex { get; set; }
    }
}
