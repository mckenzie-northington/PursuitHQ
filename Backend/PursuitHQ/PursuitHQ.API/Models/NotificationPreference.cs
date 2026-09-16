using System.ComponentModel.DataAnnotations;

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

        /// <summary>
        /// How many hours before a due date each reminder goes out, comma
        /// separated and largest first: "168,24" is a week ahead, and again the
        /// day before.
        ///
        /// Read it with ReminderOffsets.Parse rather than splitting it here -
        /// the bounds, the cap and the ordering all live in one place because
        /// the scheduler depends on them.
        /// </summary>
        [MaxLength(60)]
        public string AssignmentReminderHours { get; set; } = ReminderOffsets.Default;

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

        /// <summary>
        /// Email when somebody messages you in PursuitHQ.
        ///
        /// On by default: a message is addressed to you by a person and is the
        /// one thing here that is worth knowing about while the tab is closed.
        /// The email names the sender and the group, never the message itself -
        /// a mail provider is a third party, and what two students said to each
        /// other is not its business.
        /// </summary>
        public bool MessageEmailsEnabled { get; set; } = true;

        /// <summary>
        /// Email when another student asks to connect, or invites you to a
        /// group.
        ///
        /// On by default, and for the same reason as message email: somebody is
        /// waiting on an answer from you, and an invitation nobody sees is an
        /// invitation nobody accepts.
        /// </summary>
        public bool RequestEmailsEnabled { get; set; } = true;

        /// <summary>IANA time zone id, e.g. "America/New_York". Required so reminders arrive at the right local time.</summary>
        public string TimeZone { get; set; } = "America/New_York";
    }
}
