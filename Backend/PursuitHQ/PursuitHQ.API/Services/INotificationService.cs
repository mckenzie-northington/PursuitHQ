namespace PursuitHQ.API.Services
{
    /// <summary>What one pass of the reminder run did. Returned so it can be logged and tested.</summary>
    public record ReminderRunResult(int Considered, int Sent, int Failed, int Skipped);

    public interface INotificationService
    {
        /// <summary>
        /// Sends whatever is due right now, for every student.
        ///
        /// Safe to call as often as you like: every send is recorded, and a
        /// reminder that has already gone out is never sent again.
        /// </summary>
        Task<ReminderRunResult> RunAsync(CancellationToken ct = default);
    }
}
