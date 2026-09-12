using System.ComponentModel.DataAnnotations;

namespace PursuitHQ.API.DTOs.Notifications
{
    /// <summary>
    /// What a student has chosen to be emailed about.
    ///
    /// Time zone is deliberately not here. It lives on the account and is edited
    /// under "Your information" in settings, and having a second copy on this
    /// row would mean two answers to the same question - which is exactly the
    /// kind of thing that ends with a reminder arriving at 3am.
    /// </summary>
    public class NotificationPreferenceDto
    {
        public bool EmailEnabled { get; set; }

        public bool AssignmentRemindersEnabled { get; set; }
        public int AssignmentReminderHoursBefore { get; set; }

        public bool EventRemindersEnabled { get; set; }
        public int EventReminderMinutesBefore { get; set; }

        public bool DailyDigestEnabled { get; set; }

        /// <summary>"07:00" - local to the student's own time zone.</summary>
        public string DailyDigestTime { get; set; } = "07:00";

        public bool WeeklyDigestEnabled { get; set; }

        /// <summary>0 is Sunday, matching both DayOfWeek and JavaScript's getDay().</summary>
        public int WeeklyDigestDay { get; set; }

        /// <summary>"18:00" - local to the student's own time zone.</summary>
        public string WeeklyDigestTime { get; set; } = "18:00";

        public bool CreationConfirmationsEnabled { get; set; }

        /// <summary>Read-only here, so the settings page can say where reminders will be timed from.</summary>
        public string TimeZone { get; set; } = string.Empty;

        /// <summary>
        /// False while the sending side does not exist yet. The page uses it to
        /// say so plainly rather than letting someone switch reminders on and
        /// then wonder for a week why nothing arrived.
        /// </summary>
        public bool DeliveryConfigured { get; set; }
    }

    public class SaveNotificationPreferenceDto
    {
        public bool EmailEnabled { get; set; }

        public bool AssignmentRemindersEnabled { get; set; }

        [Range(1, 336, ErrorMessage = "Choose between 1 hour and 2 weeks before.")]
        public int AssignmentReminderHoursBefore { get; set; } = 24;

        public bool EventRemindersEnabled { get; set; }

        [Range(5, 1440, ErrorMessage = "Choose between 5 minutes and 24 hours before.")]
        public int EventReminderMinutesBefore { get; set; } = 30;

        public bool DailyDigestEnabled { get; set; }

        /// <summary>"07:00" or "07:00:00".</summary>
        [Required]
        public string DailyDigestTime { get; set; } = "07:00";

        public bool WeeklyDigestEnabled { get; set; }

        [Range(0, 6, ErrorMessage = "Pick a day of the week.")]
        public int WeeklyDigestDay { get; set; } = 0;

        /// <summary>"18:00" or "18:00:00".</summary>
        [Required]
        public string WeeklyDigestTime { get; set; } = "18:00";

        public bool CreationConfirmationsEnabled { get; set; }
    }
}
