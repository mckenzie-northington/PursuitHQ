using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.DTOs.Students;
using PursuitHQ.API.Services;

namespace PursuitHQ.API.Controllers
{
    /// <summary>
    /// Finding other students, and looking at what they have chosen to show.
    ///
    /// This is the first part of PursuitHQ that returns somebody else's data, so
    /// the rules it works under are worth stating plainly:
    ///
    /// 1. **Name search only lists students who opted in.** Discoverability is
    ///    off by default; nobody is enrolled in a directory by signing up for a
    ///    planner.
    /// 2. **Email lookup is exact, never partial, and rate limited.** Knowing
    ///    someone's address is the evidence that you know them. But it does
    ///    confirm whether an address has an account, so it cannot be run in bulk.
    /// 3. **Everything returns StudentCardDto**, never UserProfileDto, and the
    ///    email only appears on it once the two are connected.
    /// </summary>
    [Route("api/students")]
    public class StudentsController : ApiControllerBase
    {
        /// <summary>Enough that "a" cannot return half the school.</summary>
        private const int MinimumQueryLength = 2;

        private const int MaxResults = 20;

        /// <summary>
        /// Email lookups allowed per student per hour.
        ///
        /// Set where a person looking for their study group never notices and a
        /// script working through a list of addresses stops almost immediately.
        /// </summary>
        private const int EmailLookupsPerHour = 20;

        private readonly ApplicationDbContext _db;
        private readonly IConnectionService _connections;
        private readonly IMemoryCache _cache;

        public StudentsController(
            ApplicationDbContext db, IConnectionService connections, IMemoryCache cache)
        {
            _db = db;
            _connections = connections;
            _cache = cache;
        }

        [HttpGet("search")]
        public async Task<ActionResult<List<StudentCardDto>>> Search(
            [FromQuery] string? q, CancellationToken ct)
        {
            var query = (q ?? string.Empty).Trim();

            if (query.Length < MinimumQueryLength) return Ok(new List<StudentCardDto>());

            var pattern = $"%{Escape(query)}%";

            var users = await _db.Users
                .Where(u => u.IsDiscoverable && u.Id != CurrentUserId)
                .Where(u =>
                    EF.Functions.ILike(u.FirstName, pattern)
                    || EF.Functions.ILike(u.LastName, pattern)

                    // So a full name works, not only one half of it.
                    || EF.Functions.ILike(u.FirstName + " " + u.LastName, pattern))
                .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
                .Take(MaxResults)
                .ToListAsync(ct);

            return Ok(await CardsAsync(users, ct));
        }

        /// <summary>
        /// Finds one student by their exact email address.
        ///
        /// Works whether or not they are in the directory - that is the whole
        /// point, and it is how someone finds a classmate who would rather not
        /// be listed publicly. The trade is that it confirms an address has an
        /// account, so it is capped per student per hour.
        /// </summary>
        [HttpGet("lookup")]
        public async Task<ActionResult<StudentCardDto>> Lookup(
            [FromQuery] string? email, CancellationToken ct)
        {
            var address = (email ?? string.Empty).Trim();

            if (address.Length == 0)
            {
                return BadRequest(new ApiErrorDto("EmailRequired", "Enter an email address."));
            }

            if (!TakeLookupAllowance())
            {
                return StatusCode(429, new ApiErrorDto(
                    "TooManyLookups",
                    "That is a lot of lookups in one hour. Try again later."));
            }

            var normalised = address.ToUpperInvariant();

            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.NormalizedEmail == normalised && u.Id != CurrentUserId, ct);

            if (user is null)
            {
                return NotFound(new ApiErrorDto(
                    "StudentNotFound", "Nobody is using that email address."));
            }

            var connection = await _connections.BetweenAsync(CurrentUserId, user.Id, ct);

            // A blocked viewer gets the same answer as a wrong address. Saying
            // "found, but you are blocked" would tell them something they were
            // deliberately not told.
            if (connection?.Status == Models.ConnectionStatus.Blocked
                && connection.BlockedById != CurrentUserId)
            {
                return NotFound(new ApiErrorDto(
                    "StudentNotFound", "Nobody is using that email address."));
            }

            var visibility = await _connections.VisibilityAsync(CurrentUserId, user.Id, ct);

            return Ok(StudentCardMapper.ToCard(
                user,
                visibility == ProfileVisibility.Full ? visibility : ProfileVisibility.Card,
                connection,
                CurrentUserId));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<StudentCardDto>> Get(string id, CancellationToken ct)
        {
            var visibility = await _connections.VisibilityAsync(CurrentUserId, id, ct);

            if (visibility == ProfileVisibility.None) return NotFoundStudent();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user is null) return NotFoundStudent();

            var connection = await _connections.BetweenAsync(CurrentUserId, id, ct);

            return Ok(StudentCardMapper.ToCard(user, visibility, connection, CurrentUserId));
        }

        /// <summary>
        /// A student's photo.
        ///
        /// Behind the same visibility check as the profile, and served from the
        /// API rather than a public folder, so a photo cannot be fetched by
        /// anyone who happens to learn the file name.
        /// </summary>
        [HttpGet("{id}/photo")]
        public async Task<IActionResult> Photo(
            string id, [FromServices] IFileStorageService storage, CancellationToken ct)
        {
            if (await _connections.VisibilityAsync(CurrentUserId, id, ct) == ProfileVisibility.None)
            {
                return NotFound();
            }

            var user = await _db.Users
                .Where(u => u.Id == id)
                .Select(u => new { u.PhotoPath, u.PhotoContentType })
                .FirstOrDefaultAsync(ct);

            if (user?.PhotoPath is null) return NotFound();

            try
            {
                var stream = await storage.OpenAsync(user.PhotoPath, ct);

                // Private: this is behind authorization, and a shared cache
                // holding it would serve it to whoever asked next.
                Response.Headers.CacheControl = "private, max-age=300";

                return File(stream, user.PhotoContentType ?? "image/jpeg");
            }
            catch (FileNotFoundException)
            {
                // The row outlived the file. A missing avatar is not worth a 500.
                return NotFound();
            }
        }

        // ---------- helpers ----------

        private ActionResult<StudentCardDto> NotFoundStudent() =>
            NotFound(new ApiErrorDto("StudentNotFound", "No student found."));

        /// <summary>
        /// Builds cards for a list of people without a query per person.
        /// </summary>
        private async Task<List<StudentCardDto>> CardsAsync(
            List<Models.ApplicationUser> users, CancellationToken ct)
        {
            var ids = users.Select(u => u.Id).ToList();

            var connections = await _db.Connections
                .Where(c =>
                    (c.RequesterId == CurrentUserId && ids.Contains(c.AddresseeId))
                    || (c.AddresseeId == CurrentUserId && ids.Contains(c.RequesterId)))
                .ToListAsync(ct);

            var cards = new List<StudentCardDto>();

            foreach (var user in users)
            {
                var connection = connections.FirstOrDefault(
                    c => c.RequesterId == user.Id || c.AddresseeId == user.Id);

                // Someone who blocked this viewer drops out of their results
                // entirely. Being listed but unopenable would say more than
                // being absent does.
                var blockedByThem =
                    connection?.Status == Models.ConnectionStatus.Blocked
                    && connection.BlockedById != CurrentUserId;

                if (blockedByThem) continue;

                // Search results are always cards. A connected student's fuller
                // profile comes from opening it, not from appearing in a list.
                cards.Add(StudentCardMapper.ToCard(
                    user, ProfileVisibility.Card, connection, CurrentUserId));
            }

            return cards;
        }

        /// <summary>
        /// A fixed number of email lookups per hour, counted in memory.
        ///
        /// In memory is the right size for this: it resets if the API restarts,
        /// which is fine, because the thing being prevented is one person
        /// working through a list in a sitting - not a determined attacker with
        /// a botnet, who this could never stop anyway.
        /// </summary>
        private bool TakeLookupAllowance()
        {
            var key = $"email-lookup:{CurrentUserId}:{DateTime.UtcNow:yyyy-MM-dd-HH}";
            var used = _cache.GetOrCreate(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return 0;
            });

            if (used >= EmailLookupsPerHour) return false;

            _cache.Set(key, used + 1, TimeSpan.FromHours(1));
            return true;
        }

        /// <summary>
        /// Escapes ILIKE's wildcards.
        ///
        /// Without this, searching for "%" matches every discoverable student in
        /// a single request - exactly the enumeration the minimum length exists
        /// to prevent.
        /// </summary>
        private static string Escape(string value) =>
            value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    }
}
