namespace PursuitHQ.API.Services
{
    /// <summary>
    /// Runs the reminder pass on a timer, inside the API.
    ///
    /// In-process rather than an external cron hitting an endpoint, because
    /// there is no server yet - this has to work on a laptop. At deployment the
    /// same <see cref="INotificationService"/> gets a secured /api/jobs/run
    /// endpoint in front of it and a hosted cron calls that instead; nothing in
    /// the reminder logic changes, because none of it lives here.
    ///
    /// The cost of running in-process: reminders only go out while the API is
    /// up. That is what the grace period in NotificationService is for.
    /// </summary>
    public class ReminderBackgroundService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Long enough for the app to finish starting. A reminder run competing
        /// with startup is a slow first request for no reason.
        /// </summary>
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

        private readonly IServiceProvider _services;
        private readonly ILogger<ReminderBackgroundService> _logger;

        public ReminderBackgroundService(
            IServiceProvider services, ILogger<ReminderBackgroundService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Reminder service started; checking every {Minutes} minutes.", Interval.TotalMinutes);

            try
            {
                await Task.Delay(StartupDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            using var timer = new PeriodicTimer(Interval);

            do
            {
                try
                {
                    // A scope per run: NotificationService holds a DbContext,
                    // which is scoped and must not be kept alive for the life of
                    // the application.
                    using var scope = _services.CreateScope();

                    var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

                    await notifications.RunAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Swallowed on purpose. An unhandled exception here would
                    // end the loop silently and reminders would simply stop, with
                    // nothing to show why until someone noticed the absence of
                    // emails. Logging and carrying on means one bad run costs one
                    // run.
                    _logger.LogError(ex, "A reminder run failed. Carrying on.");
                }
            }
            while (await SafeWaitAsync(timer, stoppingToken));

            _logger.LogInformation("Reminder service stopped.");
        }

        private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
        {
            try
            {
                return await timer.WaitForNextTickAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }
}
