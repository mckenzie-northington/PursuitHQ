namespace PursuitHQ.API.Models
{
    /// <summary>
    /// A one-off thing to do on a given day: "email advisor", "buy lab goggles".
    ///
    /// Deliberately not a CalendarEvent. An event occupies a slot and has a
    /// start and an end; a reminder is a line you tick off, with a day attached
    /// and no time. Bolting a "this one is really a to-do" flag onto events
    /// would leave every query about events checking whether it meant this one.
    /// </summary>
    public class Reminder
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Notes { get; set; }

        /// <summary>
        /// DateOnly, not DateTime.
        ///
        /// A reminder belongs to a day rather than a moment, and DateOnly says
        /// so in the type. It also sidesteps the wall-clock converter in
        /// ApplicationDbContext entirely - there is no time of day here to be
        /// shifted by a time zone, which is one fewer way for this to go wrong.
        /// </summary>
        public DateOnly Date { get; set; }

        public bool IsCompleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
