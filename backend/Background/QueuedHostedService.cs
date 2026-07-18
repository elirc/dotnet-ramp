namespace ExpenseManagement.Api.Background;

/// <summary>
/// Long-running hosted service that drains <see cref="IBackgroundTaskQueue"/>.
/// One failing job never takes down the worker — errors are logged and the loop
/// continues. Each job runs inside its own DI scope.
/// </summary>
public class QueuedHostedService : BackgroundService
{
    private readonly IBackgroundTaskQueue _queue;
    private readonly IServiceProvider _services;
    private readonly ILogger<QueuedHostedService> _logger;

    public QueuedHostedService(
        IBackgroundTaskQueue queue,
        IServiceProvider services,
        ILogger<QueuedHostedService> logger)
    {
        _queue = queue;
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background processing worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            Func<IServiceProvider, CancellationToken, Task> work;
            try
            {
                work = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break; // shutting down
            }

            try
            {
                using var scope = _services.CreateScope();
                await work(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background job failed.");
            }
        }

        _logger.LogInformation("Background processing worker stopping.");
    }
}
