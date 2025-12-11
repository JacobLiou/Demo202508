public class MeasureWorker : BackgroundService
{
    private readonly ILogger<MeasureWorker> _log;
    private readonly ITaskQueue _queue;
    private readonly IDeviceRegistry _registry;
    private readonly IInstrumentAdapterFactory _factory;
    private readonly IResultStore _store;
    private readonly Dictionary<string, SemaphoreSlim> _deviceLocks = new();

    public MeasureWorker(ILogger<MeasureWorker> log, ITaskQueue queue, IDeviceRegistry registry,
        IInstrumentAdapterFactory factory, IResultStore store)
    {
        _log = log;
        _queue = queue;
        _registry = registry;
        _factory = factory;
        _store = store;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _log.LogInformation("MeasureWorker started.");
        while (!ct.IsCancellationRequested)
        {
            var task = await _queue.TryDequeueAsync(ct);
            if (task is null) { await Task.Delay(100, ct); continue; }

            var dev = _registry.Get(task.DeviceId);
            if (dev is null)
            {
                await _store.SetStatusAsync(task.TaskId, TaskStatus.Failed, $"Device {task.DeviceId} not found");
                continue;
            }
            var adapter = _factory.Create(dev, Program.Configuration);

            var gate = GetLock(dev.DeviceId);
            await gate.WaitAsync(ct);
            try
            {
                await _store.SetStatusAsync(task.TaskId, TaskStatus.Running);
                await adapter.ConnectAsync(ct);
                var ok = await adapter.SelfTestAsync(ct);
                if (!ok) throw new Exception("SelfTest failed");

                var result = await adapter.ExecuteAsync(task, ct);
                await _store.SaveAsync(result, ct);
                await _store.SetStatusAsync(task.TaskId, TaskStatus.Success);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Task {TaskId} failed", task.TaskId);
                await _store.SetStatusAsync(task.TaskId, TaskStatus.Failed, ex.Message);
            }
            finally
            {
                await adapter.DisconnectAsync();
                gate.Release();
            }
        }
    }

    private SemaphoreSlim GetLock(string deviceId)
    {
        if (!_deviceLocks.TryGetValue(deviceId, out var s))
        {
            s = new SemaphoreSlim(1, 1);
            _deviceLocks[deviceId] = s;
        }
        return s;
    }
}