using Microsoft.EntityFrameworkCore;
using PursuitHQ.API.Data;
using PursuitHQ.API.Models;

namespace PursuitHQ.API.Services
{
    /// <summary>How much of someone's profile the viewer is allowed to see.</summary>
    public enum ProfileVisibility
    {
        /// <summary>Blocked, or no reason to know they exist. Treated as 404.</summary>
        None = 0,

        /// <summary>Name, photo, school, education level. Enough to decide whether to connect.</summary>
        Card = 1,

        /// <summary>Everything on the card plus email, major and graduation year.</summary>
        Full = 2
    }

    public interface IConnectionService
    {
        /// <summary>The row between two people in either direction, or null.</summary>
        Task<Connection?> BetweenAsync(string a, string b, CancellationToken ct = default);

        Task<bool> AreConnectedAsync(string a, string b, CancellationToken ct = default);

        /// <summary>True if either side has blocked the other.</summary>
        Task<bool> IsBlockedAsync(string a, string b, CancellationToken ct = default);

        Task<ProfileVisibility> VisibilityAsync(
            string viewerId, string subjectId, CancellationToken ct = default);
    }

    /// <summary>
    /// The one place that answers "what is the state between these two people".
    ///
    /// Centralised on purpose. A connection is stored on a single row with a
    /// requester and an addressee, so every question about a pair has to be
    /// asked in both directions. Scattering that across controllers is how a
    /// blocked person ends up still able to message, or a pending request looks
    /// accepted from one side and not the other.
    /// </summary>
    public class ConnectionService : IConnectionService
    {
        private readonly ApplicationDbContext _db;

        public ConnectionService(ApplicationDbContext db)
        {
            _db = db;
        }

        public Task<Connection?> BetweenAsync(string a, string b, CancellationToken ct = default) =>
            _db.Connections
                .FirstOrDefaultAsync(
                    c => (c.RequesterId == a && c.AddresseeId == b)
                         || (c.RequesterId == b && c.AddresseeId == a),
                    ct);

        public async Task<bool> AreConnectedAsync(string a, string b, CancellationToken ct = default) =>
            (await BetweenAsync(a, b, ct))?.Status == ConnectionStatus.Accepted;

        public async Task<bool> IsBlockedAsync(string a, string b, CancellationToken ct = default) =>
            (await BetweenAsync(a, b, ct))?.Status == ConnectionStatus.Blocked;

        public async Task<ProfileVisibility> VisibilityAsync(
            string viewerId, string subjectId, CancellationToken ct = default)
        {
            if (viewerId == subjectId) return ProfileVisibility.Full;

            var connection = await BetweenAsync(viewerId, subjectId, ct);

            // Checked before anything else, and asymmetric on purpose. The
            // person who was blocked gets exactly what they would get for a
            // stranger - nothing - rather than an error that announces it. The
            // person who did the blocking keeps the card, because otherwise
            // there is nowhere left to undo it from.
            if (connection?.Status == ConnectionStatus.Blocked)
            {
                return connection.BlockedById == viewerId
                    ? ProfileVisibility.Card
                    : ProfileVisibility.None;
            }

            if (connection?.Status == ConnectionStatus.Accepted) return ProfileVisibility.Full;

            // A pending or declined request is itself proof the viewer reached
            // this person legitimately - they were found in the directory, or by
            // an email address the viewer already had. Keeping the card visible
            // means a declined request does not make someone vanish mid-flow.
            if (connection is not null) return ProfileVisibility.Card;

            // No relationship at all, so the only way this is allowed is if they
            // have put themselves in the directory. Without this, guessing or
            // scraping an id would show a card for someone who opted out - the
            // email lookup returns its own card inline precisely so that this
            // endpoint does not have to be open.
            var discoverable = await _db.Users
                .Where(u => u.Id == subjectId)
                .Select(u => (bool?)u.IsDiscoverable)
                .FirstOrDefaultAsync(ct);

            return discoverable == true ? ProfileVisibility.Card : ProfileVisibility.None;
        }
    }
}
