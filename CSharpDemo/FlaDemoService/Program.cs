using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;

public class Program
{
    public static IConfiguration Configuration = default!;

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        Configuration = builder.Configuration;

        // 服务注册
        builder.Services.AddSingleton<IDeviceRegistry, DeviceRegistry>();
        builder.Services.AddSingleton<ITaskQueue, TaskQueue>();
        builder.Services.AddSingleton<IInstrumentAdapterFactory, InstrumentAdapterFactory>();
        builder.Services.AddSingleton<IResultStore, InMemoryResultStore>();
        builder.Services.AddHostedService<MeasureWorker>();

        // Swagger/OpenAPI
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(opt =>
        {
            opt.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "FLA Demo Service API",
                Version = "v1",
                Description = "东隆科技 FLA 远控集成 Demo 的 REST API",
            });
            // 支持在 Minimal API 上显示参数与注释（需要 XML 注释可扩展）
            opt.SupportNonNullableReferenceTypes();
        });

        var app = builder.Build();

        // Swagger 中间件
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "FLA Demo Service API v1");
            c.RoutePrefix = string.Empty; // 让 http://localhost:5000 直接显示 UI
        });

        // API 映射
        app.MapGet("/healthz", () => Results.Ok(new { ok = true }))
           .WithName("健康检查").WithDescription("服务健康检查").WithTags("system");

        app.MapGet("/api/devices", (IDeviceRegistry reg) => reg.List())
           .WithName("设备列表").WithDescription("查询已配置的设备清单").WithTags("device");

        app.MapPost("/api/measure", async (SubmitRequest req, ITaskQueue queue, IResultStore store) =>
        {
            var taskId = $"T{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid().ToString()[..8]}";
            var normalized = ParamDefaults.Normalize(req.Params);
            var task = new MeasureTask(taskId, req.DeviceId, req.Type, normalized, DateTime.UtcNow);
            await store.SetStatusAsync(taskId, TaskStatus.Queued);
            await queue.EnqueueAsync(task, default);
            return Results.Ok(new SubmitResponse(taskId));
        })
        .WithName("提交测量任务").WithDescription("提交 OTDR/寻峰 测量任务到队列").WithTags("measure");

        app.MapGet("/api/measure/{taskId}/status", async (string taskId, IResultStore store) =>
        {
            var (s, e) = await store.GetStatusAsync(taskId);
            return Results.Ok(new StatusResponse(taskId, s, e));
        })
        .WithName("查询任务状态").WithDescription("获取任务的排队/运行/成功/失败状态").WithTags("measure");

        app.MapGet("/api/measure/{taskId}/result", async (string taskId, IResultStore store) =>
        {
            var res = await store.GetAsync(taskId, default);
            return Results.Ok(new ResultResponse(taskId, res, res is null ? "Not ready or failed" : null));
        })
        .WithName("获取任务结果").WithDescription("获取任务测量结果或失败信息").WithTags("measure");

        app.Run();
    }
}
