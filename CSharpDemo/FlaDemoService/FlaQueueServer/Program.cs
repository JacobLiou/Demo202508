using FlaQueueServer;
using FlaQueueServer.Core;
using FlaQueueServer.Devices;
using FlaQueueServer.Models;
using Serilog;
using Serilog.Events;
using System.Threading.Channels;

Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "logs"));

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] ({ThreadId}) {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(Path.Combine("logs", "server-.log"),
                  outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({ThreadId}) {Message:lj}{NewLine}{Exception}",
                  rollingInterval: RollingInterval.Day,
                  retainedFileCountLimit: 14,
                  shared: true)
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
        var worker = new MeasurementWorkerMock(queue, server);
        serverTask = server.StartAsync(cts.Token);
        workerTask = worker.StartAsync(cts.Token);
        Log.Information("Started in MOCK mode");
    }
    else
    {
        var adapter = new FlaInstrumentAdapter(cfg.FlaHost, cfg.FlaPort);
        var sw = new OpticalSwitchController(cfg.SwitchCom, cfg.SwitchBaud, cfg.SwitchIndex, cfg.SwitchInput);
        var worker = new MeasurementWorker(queue, server, adapter, sw);
        serverTask = server.StartAsync(cts.Token);
        workerTask = worker.StartAsync(cts.Token);
        Log.Information("Started in REAL mode");
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