namespace PursuitHQ.API.Models
{
    /// <summary>
    /// The "how long before" list for assignment reminders.
    ///
    /// Stored as a comma-separated string on NotificationPreference rather than
    /// in a table of its own. It is written whole, read whole, and never queried
    /// across - a table would be a migration, an entity and a join to hold what
    /// is only ever "168,24".
    /// </summary>
    public static class ReminderOffsets
    {
        /// <summary>
        /// Beyond this a single assignment generates more mail than it is worth.
        /// Four already covers a week out, two days, a day, and that evening.
        /// </summary>
        public const int MaxCount = 4;

        /// <summary>An hour is the tightest useful warning, two weeks the loosest.</summary>
        public const int MinHours = 1;

        public const int MaxHours = 336;

        public const string Default = "24";

        /// <summary>
        /// Reads the stored string, dropping anything unusable.
        ///
        /// Forgiving on purpose. This parses a text column, so a row written by
        /// an older version, a half-applied migration or a hand edit should cost
        /// one student one odd setting - not throw and take down the reminder
        /// run for everybody else.
        /// </summary>
        public static List<int> Parse(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return new List<int>();

            return Clean(csv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => int.TryParse(part, out var hours) ? hours : 0));
        }

        /// <summary>
        /// Bounds, de-duplicates, caps and orders - largest first.
        ///
        /// Largest first is not cosmetic: the scheduler walks the list in that
        /// order to work out which reminder an assignment is currently due for.
        /// </summary>
        public static List<int> Clean(IEnumerable<int>? hours) =>
            (hours ?? Enumerable.Empty<int>())
                .Where(h => h >= MinHours && h <= MaxHours)
                .Distinct()
                .OrderByDescending(h => h)
                .Take(MaxCount)
                .ToList();

        /// <summary>Turns a list from the client into what goes in the column.</summary>
        public static string ToStorage(IEnumerable<int>? hours) => string.Join(",", Clean(hours));
    }
}
