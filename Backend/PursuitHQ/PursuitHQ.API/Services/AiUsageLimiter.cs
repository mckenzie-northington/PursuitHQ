using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace PursuitHQ.API.Services
{
    /// <summary>
    /// Counts requests per student per day, in memory.
    ///
    /// In memory is the honest choice for where this app is now: it is a soft
    /// guard against runaway loops, not a billing control, and a restart
    /// clearing the counters costs nothing. If PursuitHQ ever runs on more than
    /// one instance this has to move to the database or a cache both instances
    /// share, because per-process counters would each allow the full limit.
    /// </summary>
    public class AiUsageLimiter : IAiUsageLimiter
    {
        private readonly IMemoryCache _cache;
        private readonly AiOptions _options;
        private readonly object _gate = new();

        public AiUsageLimiter(IMemoryCache cache, IOptions<AiOptions> options)
        {
            _cache = cache;
            _options = options.Value;
        }

        public AiUsage Peek(string userId) =>
            new(_cache.TryGetValue(KeyFor(userId), out int used) ? used : 0,
                _options.RequestsPerUserPerDay);

        public AiUsage Consume(string userId)
        {
            // Read-modify-write is not atomic in IMemoryCache, and two requests
            // arriving together would otherwise both read the same count and
            // both write count + 1, losing one.
            lock (_gate)
            {
                var key = KeyFor(userId);
                var used = _cache.TryGetValue(key, out int current) ? current : 0;
                used++;

                // Expiring at midnight rather than 24 hours after the first
                // request means the allowance resets on a day boundary, which
                // is what "per day" means to the person using it.
                _cache.Set(key, used, DateTime.Now.Date.AddDays(1));

                return new AiUsage(used, _options.RequestsPerUserPerDay);
            }
        }

        private static string KeyFor(string userId) =>
            $"ai-usage:{userId}:{DateTime.Now:yyyy-MM-dd}";
    }
}
