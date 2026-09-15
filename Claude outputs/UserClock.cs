using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;

namespace PursuitHQ.API.Services
{
    /// <summary>
    /// What time it is where a student is.
    ///
    /// Everything with a due date in this app is stored as wall-clock time -
    /// "the 3rd at 11:59pm" means 11:59pm where the student lives, not an
    /// instant on a universal timeline. So every question of the form "has this
    /// passed yet" has to be asked against that student's wall clock.
    ///
    /// While the API runs on the student's own laptop, DateTime.Now happens to
    /// be the right answer, which is why this was easy to get away with. On a
    /// deployed server DateTime.Now is UTC, and an assignment due at 11:59pm
    /// Central would start showing as overdue at 6:59pm. That is the bug this
    /// exists to prevent.
    /// </summary>
    public interface IUserClock
    {
        /// <summary>
        /// Wall-clock now in the given zone. Used where the zone is already in
        /// hand - the reminder scheduler loads it with the rest of the row.
        /// </summary>
        DateTime LocalNow(string? timeZoneId);

        /// <summary>
        /// Wall-clock now for a student, looking their zone up once per request.
        /// </summary>
        Task<DateTime> LocalNowAsync(string userId, CancellationToken cancellationToken = default);
    }

    public class UserClock : IUserClock
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<UserClock> _logger;

        /// <summary>
        /// A single request often asks more than once - the dashboard wants
        /// overdue assignments and overdue reminders - and the answer cannot
        /// change mid-request. Scoped service, so this dies with the request.
        /// </summary>
        private readonly Dictionary<string, string?> _zones = new();

        public UserClock(ApplicationDbContext db, ILogger<UserClock> logger)
        {
            _db = db;
            _logger = logger;
        }

        public DateTime LocalNow(string? timeZoneId)
        {
            if (string.IsNullOrWhiteSpace(timeZoneId)) return DateTime.Now;

            try
            {
                return TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // Falling back rather than throwing. A clock an hour off is a
                // nuisance; a page that will not load because one stored zone
                // name is not recognised on this machine is a real problem.
                // Windows and Linux disagree about zone ids often enough that
                // this will fire for real the first time this is deployed.
                _logger.LogWarning("Unknown time zone {TimeZone}; using server time.", timeZoneId);
                return DateTime.Now;
            }
        }

        public async Task<DateTime> LocalNowAsync(
            string userId, CancellationToken cancellationToken = default)
        {
            if (!_zones.TryGetValue(userId, out var zone))
            {
                zone = await _db.Users
                    .Where(u => u.Id == userId)
                    .Select(u => u.TimeZone)
                    .FirstOrDefaultAsync(cancellationToken);

                _zones[userId] = zone;
            }

            return LocalNow(zone);
        }
    }
}
