using System.Threading.Channels;

public interface ITaskQueue
{
    Task EnqueueAsync(MeasureTask task, CancellationToken ct);

    Task<MeasureTask?> TryDequeueAsync(CancellationToken ct);
}

public class TaskQueue : ITaskQueue
{
    private readonly Channel<MeasureTask> _channel = Channel.CreateUnbounded<MeasureTask>();

    public Task EnqueueAsync(MeasureTask task, CancellationToken ct)
        => _channel.Writer.WriteAsync(task, ct).AsTask();

    public async Task<MeasureTask?> TryDequeueAsync(CancellationToken ct)
    {
        if (await _channel.Reader.WaitToReadAsync(ct) && _channel.Reader.TryRead(out var task))
            return task;
        return null;
    }
}