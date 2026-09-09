namespace PursuitHQ.API.Services
{
    /// <summary>
    /// A per-student daily cap on AI requests.
    ///
    /// Gemini's free tier has its own quota, and hitting it takes the feature
    /// down for everyone until the next day. A local cap means one runaway loop
    /// or one impatient student cannot spend the whole allowance.
    /// </summary>
    public interface IAiUsageLimiter
    {
        /// <summary>How much of today's allowance is used, without using any.</summary>
        AiUsage Peek(string userId);

        /// <summary>
        /// Records one request. Returns the usage *after* it. Check
        /// <see cref="AiUsage.Exceeded"/> on the value returned by
        /// <see cref="Peek"/> before doing the work - this is for counting it.
        /// </summary>
        AiUsage Consume(string userId);
    }

    public record AiUsage(int Used, int Limit)
    {
        public int Remaining => Math.Max(0, Limit - Used);
        public bool Exceeded => Used >= Limit;
    }
}
