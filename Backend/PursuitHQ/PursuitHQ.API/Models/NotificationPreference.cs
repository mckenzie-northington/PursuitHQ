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

        /// <summary>IANA time zone id, e.g. "America/New_York". Required so reminders arrive at the right local time.</summary>
        public string TimeZone { get; set; } = "America/New_York";
    }
}
