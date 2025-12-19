using Microsoft.Extensions.Configuration;
using OFDRCentralControlServer;
using OFDRCentralControlServer.Core;
using OFDRCentralControlServer.Devices;
using OFDRCentralControlServer.Models;
using Serilog;
using System.Threading.Channels;

// 确保日志目录存在
Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "logs"));

// 读取环境变量：Development / Production 等
var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
          ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
          ?? "Production";

// 构建配置（支持多环境）
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

// 从配置读取 Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .Enrich.WithThreadId()
    .CreateLogger();

try
{
    var cfg = ConfigLoader.Load();

    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) =>
    {
        e.Cancel = true; cts.Cancel();
        Log.Information("\n[Server] Shutting down...");
    };

    var queue = Channel.CreateUnbounded<MeasureTask>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
    var server = new TcpServer(cfg.ListenPort, queue);

    Task serverTask, workerTask;

    // 启动服务与后台Worker
    if (cfg.RunMode.Equals("mock", StringComparison.OrdinalIgnoreCase))
    {
        Log.Information("Started in MOCK mode");
        Log.Debug("Try Log Level");
        var adapter = new FlaInstrumentAdapterMock();
        var sw = new OpticalSwitchControllerMock();
        var worker = new MeasurementWorkerMock(queue, server, adapter, sw);
        serverTask = server.StartAsync(cts.Token);
        workerTask = worker.StartAsync(cts.Token);
    }
    else
    {
        Log.Information("Started in REAL mode");
        var adapter = new FlaInstrumentAdapter(cfg.FlaHost, cfg.FlaPort);
        var sw = new OpticalSwitchController(cfg.SwitchCom, cfg.SwitchBaud, cfg.SwitchIndex, cfg.SwitchInput);

        //针对两个设备建立连接做一次诊断 如果没有连接 则报错退出
        if (!await adapter.ConnectAsync(CancellationToken.None))
        {
            Log.Error("Failed to connect to FLA instrument. Exiting.");
            return;
        }
        if (!await sw.ConnectAsync())
        {
            Log.Error("Failed to connect to OSW instrument. Exiting.");
            return;
        }

        var worker = new MeasurementWorker(queue, server, adapter, sw);
        serverTask = server.StartAsync(cts.Token);
        workerTask = worker.StartAsync(cts.Token);
    }

    Log.Information("Server started. Waiting for tasks...");
    Log.Information($"[Server] Started on tcp://0.0.0.0:{cfg.ListenPort} | FLA @ {cfg.FlaHost}:{cfg.FlaPort}");
    Log.Information("[Server] Press Ctrl+C to exit.");

    await Task.WhenAll(serverTask, workerTask);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Server crashed");
}
finally
{
    Log.Information("Server exiting.");
}