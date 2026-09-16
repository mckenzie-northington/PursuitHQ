using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PursuitHQ.API.Data;

namespace PursuitHQ.API.Services
{
    /// <summary>
    /// Can the API reach its database?
    ///
    /// Written by hand rather than pulling in the EF Core health-check package,
    /// because it is one query and the package is another dependency to keep
    /// current. Deliberately not part of "/health": see Program.cs.
    /// </summary>
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly ApplicationDbContext _db;

        public DatabaseHealthCheck(ApplicationDbContext db) => _db = db;

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken ct = default)
        {
            try
            {
                return await _db.Database.CanConnectAsync(ct)
                    ? HealthCheckResult.Healthy()
                    : HealthCheckResult.Unhealthy("The database did not answer.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("The database did not answer.", ex);
            }
        }
    }
}
