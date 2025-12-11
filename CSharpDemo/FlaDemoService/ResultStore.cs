public interface IResultStore
{
    Task SetStatusAsync(string taskId, TaskStatus status, string? error = null);

    Task<(TaskStatus status, string? error)> GetStatusAsync(string taskId);

    Task SaveAsync(MeasureResult result, CancellationToken ct);

    Task<MeasureResult?> GetAsync(string taskId, CancellationToken ct);
}

public class InMemoryResultStore : IResultStore
{
    private readonly Dictionary<string, (TaskStatus, string?)> _status = new();

    private readonly Dictionary<string, MeasureResult> _results = new();

    public Task SetStatusAsync(string taskId, TaskStatus status, string? error = null)
    {
        _status[taskId] = (status, error);
        return Task.CompletedTask;
    }

    public Task<(TaskStatus status, string? error)> GetStatusAsync(string taskId)
    {
        return Task.FromResult(_status.TryGetValue(taskId, out var s) ?
            s : (TaskStatus.Failed, "Task not found"));
    }

    public Task SaveAsync(MeasureResult result, CancellationToken ct)
    {
        _results[result.TaskId] = result;
        return Task.CompletedTask;
    }

    public Task<MeasureResult?> GetAsync(string taskId, CancellationToken ct)
    {
        _results.TryGetValue(taskId, out var r);
        return Task.FromResult(r);
    }
}