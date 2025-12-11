using FlaQueueServer;
using System.Threading.Channels;

// =============================================
// FlaQueueServer_FLA - Console + TCP + 排队 + 真实设备(FLA)协议
// .NET 8 Console
// =============================================

var listenPort = 5600; // 客户端网关端口
var deviceHost = "192.168.1.1"; // FLA设备IP（可改）
var devicePort = 4300;          // FLA远控端口

// 命令行参数：listenPort deviceHost devicePort
if (args.Length > 0 && int.TryParse(args[0], out var p)) listenPort = p;
if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])) deviceHost = args[1];
if (args.Length > 2 && int.TryParse(args[2], out var dp)) devicePort = dp;

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true; cts.Cancel(); Console.WriteLine("\n[Server] Shutting down...");
};

var queue = Channel.CreateUnbounded<MeasureTask>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
var server = new TcpServer(listenPort, queue);
var adapter = new FlaInstrumentAdapter(deviceHost, devicePort);
var worker = new MeasurementWorker(queue, server, adapter);

// 启动服务与后台Worker
var serverTask = server.StartAsync(cts.Token);
var workerTask = worker.StartAsync(cts.Token);

Console.WriteLine($"[Server] Started on tcp://0.0.0.0:{listenPort} | FLA @ {deviceHost}:{devicePort}");
Console.WriteLine("[Server] Press Ctrl+C to exit.");

await Task.WhenAll(serverTask, workerTask);

// ======================= 类型定义 =======================

public record MeasureTask(
    string TaskId,
    ClientSession Session,
    int Channel,
    string Mode,
    Dictionary<string, string> Params,
    DateTime CreatedAt
);

public record SubmitRequest(
    string? Op,
    int Channel,
    string Mode,
    Dictionary<string, string>? Params
);

public record AckMessage(string op, string taskId);
public record StatusMessage(string op, string taskId, string status, string? error = null);
public record ResultMessage(string op, string taskId, bool success, object? data = null, string? error = null);

// =======================================================