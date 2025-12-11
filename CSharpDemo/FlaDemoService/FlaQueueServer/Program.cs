using FlaQueueServer;
using Serilog;
using Serilog.Events;
using System.Threading.Channels;

// =============================================
// FlaQueueServer_FLA - Console + TCP + 排队 + 真实设备(FLA)协议
// .NET 8 Console
// =============================================

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
    var listenPort = 5600;
    var flaHost = "192.168.1.1";
    var flaPort = 4300;
    var switchCom = "COM3";
    var switchBaud = 115200;
    var switchIndex = 1;       // SW X ...
    var switchInputChannel = 1;// M

    if (args.Length > 0 && int.TryParse(args[0], out var p)) listenPort = p;
    if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])) flaHost = args[1];
    if (args.Length > 2 && int.TryParse(args[2], out var dp)) flaPort = dp;
    if (args.Length > 3 && !string.IsNullOrWhiteSpace(args[3])) switchCom = args[3];
    if (args.Length > 4 && int.TryParse(args[4], out var sb)) switchBaud = sb;
    if (args.Length > 5 && int.TryParse(args[5], out var si)) switchIndex = si;
    if (args.Length > 6 && int.TryParse(args[6], out var ic)) switchInputChannel = ic;

    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) =>
    {
        e.Cancel = true; cts.Cancel(); Console.WriteLine("\n[Server] Shutting down...");
    };

    var queue = Channel.CreateUnbounded<MeasureTask>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
    var server = new TcpServer(listenPort, queue);
    var adapter = new FlaInstrumentAdapter(flaHost, flaPort);
    var sw = new OpticalSwitchController(switchCom, switchBaud, switchIndex, switchInputChannel);
    var worker = new MeasurementWorker(queue, server, adapter, sw);

    // 启动服务与后台Worker
    var serverTask = server.StartAsync(cts.Token);
    var workerTask = worker.StartAsync(cts.Token);

    Console.WriteLine($"[Server] Started on tcp://0.0.0.0:{listenPort} | FLA @ {flaHost}:{flaPort}");
    Console.WriteLine("[Server] Press Ctrl+C to exit.");

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