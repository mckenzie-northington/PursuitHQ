using System.ComponentModel.DataAnnotations;

namespace PursuitHQ.API.DTOs.Reminders
{
    public class ReminderDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public DateOnly Date { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SaveReminderDto
    {
        [Required(ErrorMessage = "Give the reminder a title.")]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Notes { get; set; }

        [Required]
        public DateOnly Date { get; set; }

        public bool IsCompleted { get; set; }
    }

    /// <summary>
    /// Just the tick.
    ///
    /// The checkbox on the calendar has a calendar item rather than a whole
    /// reminder, so a full PUT would force it to send back a title and notes it
    /// does not have - which is how fields get overwritten with stale values.
    /// The same reason assignments have their own status endpoint.
    /// </summary>
    public class UpdateReminderStatusDto
    {
        public bool IsCompleted { get; set; }
    }
}
