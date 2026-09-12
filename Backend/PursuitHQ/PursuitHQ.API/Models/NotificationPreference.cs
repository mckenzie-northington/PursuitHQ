namespace PursuitHQ.API.Models
{
    /// <summary>
    /// One row per student controlling whether and when reminder emails are
    /// sent. Created with defaults at registration.
    /// </summary>
    public class NotificationPreference
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        /// <summary>Master switch. When false, nothing is ever emailed.</summary>
        public bool EmailEnabled { get; set; } = true;

        public bool AssignmentRemindersEnabled { get; set; } = true;
        public int AssignmentReminderHoursBefore { get; set; } = 24;

        public bool EventRemindersEnabled { get; set; } = true;
        public int EventReminderMinutesBefore { get; set; } = 30;

        public bool DailyDigestEnabled { get; set; }
        public TimeOnly DailyDigestTime { get; set; } = new TimeOnly(7, 0);

        public bool WeeklyDigestEnabled { get; set; }

        /// <summary>
        /// Which day the week-ahead summary goes out. Sunday evening by default,
        /// but Monday morning suits people who would rather not think about the
        /// week until it starts.
        /// </summary>
        public DayOfWeek WeeklyDigestDay { get; set; } = DayOfWeek.Sunday;

        /// <summary>Local time to send it, e.g. 18:00.</summary>
        public TimeOnly WeeklyDigestTime { get; set; } = new TimeOnly(18, 0);

        /// <summary>
        /// Email a confirmation whenever the student adds a course, an
        /// assignment or an event.
        ///
        /// Off by default. This is the only kind of email here that is not tied
        /// to a deadline, and one per record adds up fast - opting in should be
        /// a choice, not something to discover and switch off.
        /// </summary>
        public bool CreationConfirmationsEnabled { get; set; }

        /// <summary>IANA time zone id, e.g. "America/New_York". Required so reminders arrive at the right local time.</summary>
        public string TimeZone { get; set; } = "America/New_York";
    }
}
