public class Program
{
    public static IConfiguration Configuration = default!;

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        Configuration = builder.Configuration;

        // 注册服务
        builder.Services.AddSingleton<IDeviceRegistry, DeviceRegistry>();
        builder.Services.AddSingleton<ITaskQueue, TaskQueue>();
        builder.Services.AddSingleton<IInstrumentAdapterFactory, InstrumentAdapterFactory>();
        builder.Services.AddSingleton<IResultStore, InMemoryResultStore>();
        builder.Services.AddHostedService<MeasureWorker>();

        var app = builder.Build();

        app.MapGet("/healthz", () => Results.Ok(new { ok = true }));
        app.MapGet("/api/devices", (IDeviceRegistry reg) => reg.List());

        app.MapPost("/api/measure", async (SubmitRequest req, ITaskQueue queue, IResultStore store) =>
        {
            var taskId = $"T{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid().ToString()[..8]}";
            var normalized = ParamDefaults.Normalize(req.Params);
            var task = new MeasureTask(taskId, req.DeviceId, req.Type, normalized, DateTime.UtcNow);
            await store.SetStatusAsync(taskId, TaskStatus.Queued);
            await queue.EnqueueAsync(task, default);
            return Results.Ok(new SubmitResponse(taskId));
        });

        app.MapGet("/api/measure/{taskId}/status", async (string taskId, IResultStore store) =>
        {
            var (s, e) = await store.GetStatusAsync(taskId);
            return Results.Ok(new StatusResponse(taskId, s, e));
        });

        app.MapGet("/api/measure/{taskId}/result", async (string taskId, IResultStore store) =>
        {
            var res = await store.GetAsync(taskId, default);
            return Results.Ok(new ResultResponse(taskId, res, res is null ? "Not ready or failed" : null));
        });

        app.Run();
    }
}