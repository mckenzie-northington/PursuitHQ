using System.Threading.Channels;

namespace PursuitHQ.API.Services
{
    public interface IEmailQueue
    {
        /// <summary>
        /// Hands an already-rendered email to the background sender.
        ///
        /// Returns immediately and never throws. Confirmation emails are sent
        /// from inside a request - "add course" should not sit waiting on an
        /// HTTP call to Resend, and an email provider having a bad minute must
        /// not turn into a failed save of the thing the student actually asked
        /// for.
        /// </summary>
        void Enqueue(EmailMessage message);
    }

    public class EmailQueue : IEmailQueue
    {
        /// <summary>
        /// Bounded, and drops rather than blocks when full.
        ///
        /// Unbounded would let a runaway loop eat memory until the process dies.
        /// Dropping loses a confirmation email, which is a nuisance; blocking
        /// would stall the request that queued it, which is a bug.
        /// </summary>
        private readonly Channel<EmailMessage> _channel =
            Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(500)
            {
                FullMode = BoundedChannelFullMode.DropWrite
            });

        private readonly ILogger<EmailQueue> _logger;

        public EmailQueue(ILogger<EmailQueue> logger) => _logger = logger;

        public ChannelReader<EmailMessage> Reader => _channel.Reader;

        public void Enqueue(EmailMessage message)
        {
            if (!_channel.Writer.TryWrite(message))
            {
                _logger.LogWarning(
                    "Email queue is full; dropped \"{Subject}\".", message.Subject);
            }
        }
    }

    /// <summary>
    /// Drains the queue, one email at a time.
    ///
    /// In-memory, so anything still queued when the API stops is lost. That is
    /// an acceptable trade for a confirmation: it is a courtesy, not a record,
    /// and the thing it confirms is already safely in the database. Reminders,
    /// which do matter, go through NotificationService and are recorded.
    /// </summary>
    public class EmailQueueWorker : BackgroundService
    {
        private readonly EmailQueue _queue;
        private readonly IServiceProvider _services;
        private readonly ILogger<EmailQueueWorker> _logger;

        public EmailQueueWorker(
            EmailQueue queue, IServiceProvider services, ILogger<EmailQueueWorker> logger)
        {
            _queue = queue;
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
                {
                    try
                    {
                        // A scope per message: IEmailService may be a typed
                        // HttpClient, which must not be held for the life of the
                        // application.
                        using var scope = _services.CreateScope();

                        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();

                        await email.SendAsync(message, stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // One bad email must not end the loop - that would stop
                        // every later one silently.
                        _logger.LogError(ex, "Could not send \"{Subject}\".", message.Subject);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
        }
    }
}
