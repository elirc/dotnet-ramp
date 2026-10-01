using System.Threading.Channels;

namespace ExpenseManagement.Api.Background;

/// <summary>
/// A bounded producer/consumer queue for background work. Each job receives a
/// fresh DI scope so it can resolve services safely off the request thread.
/// </summary>
public interface IBackgroundTaskQueue
{
    /// <summary>
    /// Attempts admission without waiting. Commands that change domain state use
    /// this boundary so a full queue cannot leave a half-completed command.
    /// </summary>
    bool TryEnqueue(Func<IServiceProvider, CancellationToken, Task> work);
    ValueTask EnqueueAsync(Func<IServiceProvider, CancellationToken, Task> work);
    ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken ct);
}

public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _channel;

    public BackgroundTaskQueue(int capacity = 100)
    {
        // Backpressure: if the queue fills up, producers wait rather than blowing memory.
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _channel = Channel.CreateBounded<Func<IServiceProvider, CancellationToken, Task>>(options);
    }

    public async ValueTask EnqueueAsync(Func<IServiceProvider, CancellationToken, Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        await _channel.Writer.WriteAsync(work);
    }

    public bool TryEnqueue(Func<IServiceProvider, CancellationToken, Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        return _channel.Writer.TryWrite(work);
    }

    public async ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken ct) =>
        await _channel.Reader.ReadAsync(ct);
}
