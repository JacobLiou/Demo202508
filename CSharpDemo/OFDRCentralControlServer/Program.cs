using OFDRCentralControlServer.Core;
using OFDRCentralControlServer.Devices;
using OFDRCentralControlServer;
using OFDRCentralControlServer.Models;
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
        Log.Information("Started in MOCK mode");
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
        if(!await adapter.ConnectAsync(CancellationToken.None))
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