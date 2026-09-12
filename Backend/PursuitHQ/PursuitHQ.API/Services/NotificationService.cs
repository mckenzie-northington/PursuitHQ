using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs.Calendar;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.Services
{
    /// <summary>
    /// Decides who is owed a reminder, writes it, sends it, and records that it
    /// went - so it is never sent twice.
    ///
    /// Four kinds: a nudge before an assignment is due, a nudge before something
    /// on the calendar starts, a morning summary, and a Sunday look at the week.
    /// Each is opt-in separately, and the master switch is checked once for all
    /// of them.
    ///
    /// Everything here reads wall-clock times in the student's own zone. Stored
    /// DateTime values are wall-clock, not instants (see
    /// ApplicationDbContext.ConfigureConventions): a 9am class is 9am to whoever
    /// typed it. Comparing those against UTC would shift every reminder by the
    /// offset, which is how people get emailed at 4am.
    /// </summary>
    public class NotificationService : INotificationService
    {
        /// <summary>
        /// How late an assignment reminder is still worth sending.
        ///
        /// Reminders only go out while the API is up, and a laptop closes.
        /// Without a grace period a missed run steps straight over the window
        /// and the reminder never goes at all. Past six hours it is not worth
        /// it: "due in 24 hours" about something due in one is worse than
        /// silence.
        /// </summary>
        private static readonly TimeSpan AssignmentGrace = TimeSpan.FromHours(6);

        /// <summary>
        /// Much shorter for events. A reminder that your 9am lecture is starting,
        /// delivered at 11, is just noise.
        /// </summary>
        private static readonly TimeSpan EventGrace = TimeSpan.FromMinutes(20);

        private readonly ApplicationDbContext _db;
        private readonly ICalendarFeedService _calendar;
        private readonly IEmailService _email;
        private readonly EmailOptions _options;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            ApplicationDbContext db,
            ICalendarFeedService calendar,
            IEmailService email,
            IOptions<EmailOptions> options,
            ILogger<NotificationService> logger)
        {
            _db = db;
            _calendar = calendar;
            _email = email;
            _options = options.Value;
            _logger = logger;
        }

        private sealed record Recipient(
            string UserId,
            string Email,
            string Name,
            string? TimeZone,
            NotificationPreference Preference);

        private sealed class Totals
        {
            public int Considered;
            public int Sent;
            public int Failed;
            public int Skipped;
        }

        public async Task<ReminderRunResult> RunAsync(CancellationToken ct = default)
        {
            var totals = new Totals();

            var recipients = await _db.NotificationPreferences
                .Where(p => p.EmailEnabled)
                .Join(_db.Users, p => p.UserId, u => u.Id, (p, u) => new
                {
                    Preference = p,
                    u.Id,
                    u.Email,
                    u.FirstName,
                    u.TimeZone
                })
                .ToListAsync(ct);

            foreach (var row in recipients)
            {
                if (ct.IsCancellationRequested) break;
                if (string.IsNullOrWhiteSpace(row.Email)) continue;

                var recipient = new Recipient(
                    row.Id,
                    row.Email!,
                    string.IsNullOrWhiteSpace(row.FirstName) ? "there" : row.FirstName,
                    row.TimeZone,
                    row.Preference);

                var localNow = LocalNow(row.TimeZone);

                // One student's bad data must not stop everyone else's
                // reminders, so each kind is isolated.
                await SafelyAsync("assignment reminders", recipient, () =>
                    recipient.Preference.AssignmentRemindersEnabled
                        ? AssignmentRemindersAsync(recipient, localNow, totals, ct)
                        : Task.CompletedTask);

                await SafelyAsync("event reminders", recipient, () =>
                    recipient.Preference.EventRemindersEnabled
                        ? EventRemindersAsync(recipient, localNow, totals, ct)
                        : Task.CompletedTask);

                await SafelyAsync("daily digest", recipient, () =>
                    recipient.Preference.DailyDigestEnabled
                        ? DailyDigestAsync(recipient, localNow, totals, ct)
                        : Task.CompletedTask);

                await SafelyAsync("weekly digest", recipient, () =>
                    recipient.Preference.WeeklyDigestEnabled
                        ? WeeklyDigestAsync(recipient, localNow, totals, ct)
                        : Task.CompletedTask);
            }

            if (totals.Considered > 0)
            {
                _logger.LogInformation(
                    "Reminder run: {Considered} considered, {Sent} sent, {Failed} failed, {Skipped} already sent.",
                    totals.Considered, totals.Sent, totals.Failed, totals.Skipped);
            }

            return new ReminderRunResult(totals.Considered, totals.Sent, totals.Failed, totals.Skipped);
        }

        private async Task SafelyAsync(string what, Recipient recipient, Func<Task> work)
        {
            try
            {
                await work();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{What} failed for {UserId}.", what, recipient.UserId);
            }
        }

        // ---------------------------------------------------------------- 1

        private async Task AssignmentRemindersAsync(
            Recipient recipient, DateTime localNow, Totals totals, CancellationToken ct)
        {
            var window = localNow.AddHours(recipient.Preference.AssignmentReminderHoursBefore);
            var earliest = localNow - AssignmentGrace;

            var due = await _db.Assignments
                .Include(a => a.Course)
                .Where(a => a.Course != null
                            && a.Course.UserId == recipient.UserId
                            && a.Status != AssignmentStatus.Completed
                            && a.DueDate <= window
                            && a.DueDate >= earliest)
                .OrderBy(a => a.DueDate)
                .ToListAsync(ct);

            foreach (var assignment in due)
            {
                var course = assignment.Course?.Name ?? "your course";
                var when = Describe(assignment.DueDate - localNow);

                var html =
                    $"<p><strong>{Encode(assignment.Title)}</strong> ({Encode(course)}) is due {Encode(when)}.</p>"
                    + $"<p style=\"color:#475569\">Due {assignment.DueDate:dddd, d MMMM} at {assignment.DueDate:h:mm tt}</p>";

                var text =
                    $"{assignment.Title} ({course}) is due {when}.\n"
                    + $"Due: {assignment.DueDate:dddd, d MMMM} at {assignment.DueDate:h:mm tt}";

                await SendOnceAsync(
                    recipient,
                    NotificationType.AssignmentDue,
                    "Assignment",
                    assignment.Id,
                    subject: $"{assignment.Title} is due {when}",
                    heading: "Coming up",
                    html, text, totals, ct);
            }
        }

        // ---------------------------------------------------------------- 2

        private async Task EventRemindersAsync(
            Recipient recipient, DateTime localNow, Totals totals, CancellationToken ct)
        {
            var minutes = recipient.Preference.EventReminderMinutesBefore;
            var today = DateOnly.FromDateTime(localNow);

            // Tomorrow too, so a reminder set two hours ahead still fires for
            // something at 00:30.
            var feed = await _calendar.GetAsync(recipient.UserId, today, today.AddDays(1), ct);

            foreach (var item in feed.Items)
            {
                if (item.StartTime is null) continue;
                if (item.Type is not (CalendarItemType.Class or CalendarItemType.Event)) continue;

                var startsAt = item.Date.ToDateTime(item.StartTime.Value);
                var until = startsAt - localNow;

                if (until > TimeSpan.FromMinutes(minutes)) continue;
                if (until < -EventGrace) continue;

                var when = until <= TimeSpan.Zero ? "now" : Describe(until);

                var where = string.IsNullOrWhiteSpace(item.Location)
                    ? ""
                    : $"<p style=\"color:#475569\">{Encode(item.Location!)}</p>";

                var html =
                    $"<p><strong>{Encode(item.Title)}</strong> starts {Encode(when)}.</p>"
                    + $"<p style=\"color:#475569\">{startsAt:h:mm tt}"
                    + (item.EndTime is not null ? $" - {item.Date.ToDateTime(item.EndTime.Value):h:mm tt}" : "")
                    + "</p>"
                    + where;

                var text =
                    $"{item.Title} starts {when}.\n"
                    + $"{startsAt:h:mm tt}"
                    + (item.EndTime is not null ? $" - {item.Date.ToDateTime(item.EndTime.Value):h:mm tt}" : "")
                    + (string.IsNullOrWhiteSpace(item.Location) ? "" : $"\n{item.Location}");

                // A class repeats weekly, so the course id alone would mark
                // every future Tuesday as already-sent after the first one. The
                // date goes in the key to make each meeting its own thing.
                await SendOnceAsync(
                    recipient,
                    NotificationType.EventReminder,
                    $"{item.Type}@{item.Date:yyyy-MM-dd}",
                    item.SourceId ?? 0,
                    subject: $"{item.Title} starts {when}",
                    heading: "Starting soon",
                    html, text, totals, ct);
            }
        }

        // ---------------------------------------------------------------- 3

        private async Task DailyDigestAsync(
            Recipient recipient, DateTime localNow, Totals totals, CancellationToken ct)
        {
            // Only once the chosen hour has passed, and only for today - so a
            // machine that was off all morning still sends it at noon, but never
            // sends yesterday's.
            if (TimeOnly.FromDateTime(localNow) < recipient.Preference.DailyDigestTime) return;

            var today = DateOnly.FromDateTime(localNow);
            var feed = await _calendar.GetAsync(recipient.UserId, today, today, ct);

            var schedule = feed.Items
                .Where(i => i.Type is CalendarItemType.Class or CalendarItemType.Event)
                .OrderBy(i => i.StartTime ?? TimeOnly.MinValue)
                .ToList();

            var due = feed.Items
                .Where(i => i.Type == CalendarItemType.Assignment && i.Status != nameof(AssignmentStatus.Completed))
                .ToList();

            // An empty digest every morning teaches you to ignore the digest.
            if (schedule.Count == 0 && due.Count == 0) return;

            var html = new StringBuilder();
            var text = new StringBuilder();

            if (schedule.Count > 0)
            {
                html.Append("<p style=\"font-weight:600;margin-bottom:4px\">Today</p><ul style=\"padding-left:18px\">");
                text.AppendLine("Today");

                foreach (var item in schedule)
                {
                    var time = item.StartTime is null ? "All day" : item.StartTime.Value.ToString("h:mm tt");

                    html.Append($"<li>{Encode(time)} - {Encode(item.Title)}")
                        .Append(string.IsNullOrWhiteSpace(item.Location) ? "" : $" <span style=\"color:#64748b\">({Encode(item.Location!)})</span>")
                        .Append("</li>");

                    text.AppendLine($"  {time} - {item.Title}");
                }

                html.Append("</ul>");
                text.AppendLine();
            }

            if (due.Count > 0)
            {
                html.Append("<p style=\"font-weight:600;margin:16px 0 4px\">Due today</p><ul style=\"padding-left:18px\">");
                text.AppendLine("Due today");

                foreach (var item in due)
                {
                    html.Append($"<li>{Encode(item.Title)}")
                        .Append(string.IsNullOrWhiteSpace(item.Subtitle) ? "" : $" <span style=\"color:#64748b\">({Encode(item.Subtitle!)})</span>")
                        .Append("</li>");

                    text.AppendLine($"  {item.Title}{(string.IsNullOrWhiteSpace(item.Subtitle) ? "" : $" ({item.Subtitle})")}");
                }

                html.Append("</ul>");
            }

            await SendOnceAsync(
                recipient,
                NotificationType.DailyDigest,
                $"Day@{today:yyyy-MM-dd}",
                relatedId: null,
                subject: $"Your day - {localNow:dddd, d MMMM}",
                heading: $"{localNow:dddd, d MMMM}",
                html.ToString(), text.ToString(), totals, ct);
        }

        // ---------------------------------------------------------------- 4

        private async Task WeeklyDigestAsync(
            Recipient recipient, DateTime localNow, Totals totals, CancellationToken ct)
        {
            if (localNow.DayOfWeek != recipient.Preference.WeeklyDigestDay) return;
            if (TimeOnly.FromDateTime(localNow) < recipient.Preference.WeeklyDigestTime) return;

            // The seven days starting tomorrow. Sent on a Sunday that is Monday
            // to Sunday; sent on a Monday it is Tuesday to Monday - either way
            // it is the week you have not lived yet, which is the point.
            var from = DateOnly.FromDateTime(localNow).AddDays(1);
            var to = from.AddDays(6);

            var feed = await _calendar.GetAsync(recipient.UserId, from, to, ct);

            // Classes are left out on purpose. They are the same every week, so
            // listing them turns a week-ahead summary into a wall of text you
            // stop reading - which would bury the assignments, the one part
            // that actually changes.
            var ahead = feed.Items
                .Where(i => i.Type is CalendarItemType.Assignment or CalendarItemType.Event)
                .Where(i => i.Status != nameof(AssignmentStatus.Completed))
                .OrderBy(i => i.Date)
                .ThenBy(i => i.StartTime ?? TimeOnly.MinValue)
                .ToList();

            if (ahead.Count == 0) return;

            var html = new StringBuilder();
            var text = new StringBuilder();

            foreach (var day in ahead.GroupBy(i => i.Date))
            {
                var heading = day.Key.ToString("dddd, d MMMM", CultureInfo.InvariantCulture);

                html.Append($"<p style=\"font-weight:600;margin:16px 0 4px\">{Encode(heading)}</p>")
                    .Append("<ul style=\"padding-left:18px\">");

                text.AppendLine(heading);

                foreach (var item in day)
                {
                    var time = item.StartTime is null ? null : item.StartTime.Value.ToString("h:mm tt");
                    var label = time is null ? item.Title : $"{time} - {item.Title}";

                    html.Append($"<li>{Encode(label)}")
                        .Append(item.Type == CalendarItemType.Assignment ? " <span style=\"color:#64748b\">(due)</span>" : "")
                        .Append("</li>");

                    text.AppendLine($"  {label}{(item.Type == CalendarItemType.Assignment ? " (due)" : "")}");
                }

                html.Append("</ul>");
                text.AppendLine();
            }

            await SendOnceAsync(
                recipient,
                NotificationType.WeeklyDigest,
                $"Week@{from:yyyy-MM-dd}",
                relatedId: null,
                subject: $"The week ahead - {from:d MMMM}",
                heading: "The week ahead",
                html.ToString(), text.ToString(), totals, ct);
        }

        // ---------------------------------------------------------- sending

        /// <summary>
        /// Sends one email, unless it has already gone out, and records it.
        ///
        /// Only successful sends count as "already sent". A failed attempt is
        /// recorded for the history but deliberately does not block anything, so
        /// a send that failed because the provider was briefly down is retried on
        /// the next run rather than lost for good.
        /// </summary>
        private async Task SendOnceAsync(
            Recipient recipient,
            NotificationType type,
            string relatedType,
            int? relatedId,
            string subject,
            string heading,
            string html,
            string text,
            Totals totals,
            CancellationToken ct)
        {
            totals.Considered++;

            var previous = _db.Notifications.Where(
                n => n.UserId == recipient.UserId
                     && n.Type == type
                     && n.RelatedEntityType == relatedType
                     && n.Status == DeliveryStatus.Sent);

            // Split rather than writing `n.RelatedEntityId == relatedId`. With a
            // null parameter that compiles to `= NULL` in SQL, which is never
            // true - so every digest would look unsent and go out again on every
            // run. The digests are exactly the rows with no id.
            previous = relatedId is null
                ? previous.Where(n => n.RelatedEntityId == null)
                : previous.Where(n => n.RelatedEntityId == relatedId);

            if (await previous.AnyAsync(ct))
            {
                totals.Skipped++;
                return;
            }

            var body = $"<p>Hi {Encode(recipient.Name)},</p>{html}";
            var plain = $"Hi {recipient.Name},\n\n{text}";

            var result = await _email.SendAsync(
                new EmailMessage(
                    recipient.Email,
                    recipient.Name,
                    subject,
                    EmailLayout.Html(heading, body, _options.AppUrl),
                    EmailLayout.Text(heading, plain, _options.AppUrl)),
                ct);

            // The body is not stored on purpose - it carries assignment titles
            // and course names, and keeping it adds risk without adding value.
            _db.Notifications.Add(new Notification
            {
                UserId = recipient.UserId,
                Type = type,
                RelatedEntityType = relatedType,
                RelatedEntityId = relatedId,
                Subject = subject,
                SentAt = DateTime.UtcNow,
                Status = result.Sent ? DeliveryStatus.Sent : DeliveryStatus.Failed,
                ErrorMessage = Clip(result.Error)
            });

            await _db.SaveChangesAsync(ct);

            if (result.Sent)
            {
                totals.Sent++;
            }
            else
            {
                totals.Failed++;
                _logger.LogWarning("{Type} for {UserId} failed: {Error}", type, recipient.UserId, result.Error);
            }
        }

        // ---------------------------------------------------------- helpers

        /// <summary>"in 3 hours", "tomorrow" - how a person would say it.</summary>
        private static string Describe(TimeSpan remaining)
        {
            if (remaining <= TimeSpan.Zero) return "now";
            if (remaining < TimeSpan.FromMinutes(2)) return "in a minute";

            if (remaining < TimeSpan.FromHours(1))
            {
                return $"in {(int)Math.Round(remaining.TotalMinutes)} minutes";
            }

            if (remaining < TimeSpan.FromHours(24))
            {
                var hours = (int)Math.Round(remaining.TotalHours);
                return hours == 1 ? "in an hour" : $"in {hours} hours";
            }

            var days = (int)Math.Round(remaining.TotalDays);
            return days == 1 ? "tomorrow" : $"in {days} days";
        }

        private static string Encode(string value) => WebUtility.HtmlEncode(value);

        private static string? Clip(string? error) =>
            string.IsNullOrWhiteSpace(error) ? null
            : error.Length <= 500 ? error
            : error[..500];

        /// <summary>
        /// The current wall-clock time where this student is.
        ///
        /// An unknown or misspelled zone falls back to the server's own time
        /// rather than throwing: a reminder an hour off is a nuisance, but one
        /// bad row stopping the run for everybody is a real problem.
        /// </summary>
        private DateTime LocalNow(string? timeZoneId)
        {
            if (string.IsNullOrWhiteSpace(timeZoneId)) return DateTime.Now;

            try
            {
                return TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                _logger.LogWarning("Unknown time zone {TimeZone}; using server time.", timeZoneId);
                return DateTime.Now;
            }
        }
    }
}
