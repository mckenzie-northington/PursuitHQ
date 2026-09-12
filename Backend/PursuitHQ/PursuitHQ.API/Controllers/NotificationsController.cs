using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.Notifications;
using PursuitHQ.API.Models;
using PursuitHQ.API.Services;

namespace PursuitHQ.API.Controllers
{
    /// <summary>
    /// What a student wants to be emailed about, and when.
    ///
    /// Only the choices for now - nothing here sends anything. The scheduler and
    /// the email service come next and will read exactly these rows, which is
    /// why this is worth having working and tested first: a reminder that goes
    /// to the wrong person or arrives after the deadline is worse than no
    /// reminder, and the settings are where that gets decided.
    /// </summary>
    [Route("api/notifications")]
    public class NotificationsController : ApiControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailService _email;
        private readonly EmailOptions _emailOptions;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(
            ApplicationDbContext db,
            IEmailService email,
            IOptions<EmailOptions> emailOptions,
            ILogger<NotificationsController> logger)
        {
            _db = db;
            _email = email;
            _emailOptions = emailOptions.Value;
            _logger = logger;
        }

        [HttpGet("preferences")]
        public async Task<ActionResult<NotificationPreferenceDto>> GetPreferences()
        {
            var preference = await GetOrCreateAsync();
            return Ok(await ToDtoAsync(preference));
        }

        [HttpPut("preferences")]
        public async Task<ActionResult<NotificationPreferenceDto>> SavePreferences(
            SaveNotificationPreferenceDto dto)
        {
            if (!TryParseTime(dto.DailyDigestTime, out var digestTime))
            {
                return BadRequest(new ApiErrorDto(
                    "InvalidTime", $"\"{dto.DailyDigestTime}\" is not a time like 07:00."));
            }

            if (!TryParseTime(dto.WeeklyDigestTime, out var weeklyTime))
            {
                return BadRequest(new ApiErrorDto(
                    "InvalidTime", $"\"{dto.WeeklyDigestTime}\" is not a time like 18:00."));
            }

            var preference = await GetOrCreateAsync();

            preference.EmailEnabled = dto.EmailEnabled;

            preference.AssignmentRemindersEnabled = dto.AssignmentRemindersEnabled;
            preference.AssignmentReminderHoursBefore = dto.AssignmentReminderHoursBefore;

            preference.EventRemindersEnabled = dto.EventRemindersEnabled;
            preference.EventReminderMinutesBefore = dto.EventReminderMinutesBefore;

            preference.DailyDigestEnabled = dto.DailyDigestEnabled;
            preference.DailyDigestTime = digestTime;

            preference.WeeklyDigestEnabled = dto.WeeklyDigestEnabled;
            preference.CreationConfirmationsEnabled = dto.CreationConfirmationsEnabled;
            preference.WeeklyDigestDay = (DayOfWeek)dto.WeeklyDigestDay;
            preference.WeeklyDigestTime = weeklyTime;

            await _db.SaveChangesAsync();

            return Ok(await ToDtoAsync(preference));
        }

        /// <summary>
        /// Sends one test email to the signed-in student's own address.
        ///
        /// Only ever to yourself - the address is read from the account, never
        /// taken from the request - so this cannot be turned into a way to mail
        /// strangers. It exists because DNS verification is the step most likely
        /// to go quietly wrong, and "press this and see if it arrives" is a much
        /// better answer than waiting until tomorrow's reminders do not.
        /// </summary>
        [HttpPost("test")]
        public async Task<IActionResult> SendTest(CancellationToken ct)
        {
            var account = await _db.Users
                .Where(u => u.Id == CurrentUserId)
                .Select(u => new { u.Email, u.FirstName })
                .FirstOrDefaultAsync(ct);

            if (account?.Email is null)
            {
                return NotFound(new ApiErrorDto("UserNotFound", "Could not find your account."));
            }

            var name = string.IsNullOrWhiteSpace(account.FirstName) ? "there" : account.FirstName;

            var text =
                $"Hi {name},\n\n"
                + "This is a test from PursuitHQ. If it reached you, reminders will too.\n\n"
                + "You can change what you get emailed about, or turn it off entirely, in Settings.";

            var html =
                $"<p>Hi {WebUtility.HtmlEncode(name)},</p>"
                + "<p>This is a test from PursuitHQ. If it reached you, reminders will too.</p>"
                + "<p>You can change what you get emailed about, or turn it off entirely, in Settings.</p>";

            var result = await _email.SendAsync(
                new EmailMessage(
                    account.Email,
                    name,
                    "PursuitHQ test email",
                    EmailLayout.Html("Your email is working", html, _emailOptions.AppUrl),
                    EmailLayout.Text("Your email is working", text, _emailOptions.AppUrl)),
                ct);

            if (!result.Sent)
            {
                _logger.LogWarning("Test email failed for {UserId}: {Error}", CurrentUserId, result.Error);

                return StatusCode(502, new ApiErrorDto("EmailFailed", result.Error ?? "The email could not be sent."));
            }

            return Ok(new
            {
                sent = _email.IsConfigured,
                to = account.Email,
                message = _email.IsConfigured
                    ? $"Sent to {account.Email}. Give it a minute, and check spam."
                    : "Sending is not set up yet, so the email was printed to the API terminal instead."
            });
        }

        /// <summary>
        /// Runs the reminder pass immediately instead of waiting for the timer.
        ///
        /// Development only, and a 404 anywhere else. This sends to every
        /// student who is due something, not just the caller, so it is not
        /// something a signed-in user should be able to trigger on a real
        /// server - the deployed version of this is a secured endpoint that
        /// only the scheduler can call.
        /// </summary>
        [HttpPost("run")]
        public async Task<IActionResult> RunNow(
            [FromServices] IWebHostEnvironment environment,
            [FromServices] INotificationService notifications,
            CancellationToken ct)
        {
            if (!environment.IsDevelopment()) return NotFound();

            return Ok(await notifications.RunAsync(ct));
        }

        // ---------- helpers ----------

        /// <summary>
        /// Finds this student's preferences, creating the row with defaults the
        /// first time.
        ///
        /// Create-on-read rather than create-at-registration: accounts made
        /// before this feature existed have no row, and a settings page that
        /// throws for exactly the accounts that have been around longest is not
        /// worth the tidiness of doing it at sign-up.
        /// </summary>
        private async Task<NotificationPreference> GetOrCreateAsync()
        {
            var preference = await _db.NotificationPreferences
                .FirstOrDefaultAsync(p => p.UserId == CurrentUserId);

            if (preference is not null) return preference;

            preference = new NotificationPreference { UserId = CurrentUserId };

            _db.NotificationPreferences.Add(preference);
            await _db.SaveChangesAsync();

            return preference;
        }

        private async Task<NotificationPreferenceDto> ToDtoAsync(NotificationPreference p)
        {
            // The account's time zone is the single source of truth; the copy on
            // the preference row is left alone.
            var timeZone = await _db.Users
                .Where(u => u.Id == CurrentUserId)
                .Select(u => u.TimeZone)
                .FirstOrDefaultAsync();

            return new NotificationPreferenceDto
            {
                EmailEnabled = p.EmailEnabled,
                AssignmentRemindersEnabled = p.AssignmentRemindersEnabled,
                AssignmentReminderHoursBefore = p.AssignmentReminderHoursBefore,
                EventRemindersEnabled = p.EventRemindersEnabled,
                EventReminderMinutesBefore = p.EventReminderMinutesBefore,
                DailyDigestEnabled = p.DailyDigestEnabled,
                DailyDigestTime = p.DailyDigestTime.ToString("HH\\:mm", CultureInfo.InvariantCulture),
                WeeklyDigestEnabled = p.WeeklyDigestEnabled,
                CreationConfirmationsEnabled = p.CreationConfirmationsEnabled,
                WeeklyDigestDay = (int)p.WeeklyDigestDay,
                WeeklyDigestTime = p.WeeklyDigestTime.ToString("HH\\:mm", CultureInfo.InvariantCulture),
                TimeZone = string.IsNullOrWhiteSpace(timeZone) ? "America/New_York" : timeZone,
                DeliveryConfigured = _email.IsConfigured
            };
        }

        /// <summary>Accepts "07:00" and "07:00:00"; rejects everything else.</summary>
        private static bool TryParseTime(string? value, out TimeOnly time)
        {
            time = default;

            return !string.IsNullOrWhiteSpace(value)
                   && TimeOnly.TryParseExact(
                       value.Trim(),
                       new[] { "HH:mm", "H:mm", "HH:mm:ss" },
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.None,
                       out time);
        }
    }
}
