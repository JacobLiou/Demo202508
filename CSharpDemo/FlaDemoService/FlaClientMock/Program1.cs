//using Serilog;
//using Serilog.Events;
//using System.Collections.Concurrent;
//using System.Net.Sockets;
//using System.Text;
//using System.Text.Json;

//// -----------------------------------------------------
//// 压测客户端：持续不断请求，自动重连，永不退出
//// .NET 8 Console
//// -----------------------------------------------------

//Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "logs"));
//Log.Logger = new LoggerConfiguration()
//    .MinimumLevel.Debug()
//    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
//    .Enrich.FromLogContext()
//    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] ({ThreadId}) {Message:lj}{NewLine}{Exception}")
//    .WriteTo.File(Path.Combine("logs", "client-stress-.log"),
//        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({ThreadId}) {Message:lj}{NewLine}{Exception}",
//        rollingInterval: RollingInterval.Day,
//        retainedFileCountLimit: 14,
//        shared: true)
//    .CreateLogger();

//// 参数：host port clients mode staggerMs qps
//var host = args.Length > 0 ? args[0] : "127.0.0.1";
//var port = args.Length > 1 && int.TryParse(args[1], out var p) ? p : 5600;
//var clients = args.Length > 2 && int.TryParse(args[2], out var c) ? Math.Min(Math.Max(c, 1), 256) : 16;
//var mode = args.Length > 3 ? args[3].ToLowerInvariant() : "mix"; // scan | zero | auto_peak | mix
//var staggerMs = args.Length > 4 && int.TryParse(args[4], out var st) ? st : 500;
//var qps = args.Length > 5 && int.TryParse(args[5], out var q) ? Math.Max(q, 1) : 2;

//Console.WriteLine($"[Stress] Host={host} Port={port} Clients={clients} Mode={mode} StaggerMs={staggerMs} QPS={qps}");
//Console.WriteLine("[Stress] 程序为压力测试模式：持续提交、自动重连、除非进程被强制终止否则不退出。");

//// 不再监听 Ctrl+C，以避免退出；如需允许退出可恢复以下几行：
//// var cts = new CancellationTokenSource();
//// Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

//var tasks = new List<Task>();
//for (int i = 1; i <= clients; i++)
//{
//    await Task.Delay(staggerMs);
//    tasks.Add(RunClientForeverAsync(i, host, port, mode, qps));
//}

//await Task.WhenAll(tasks);
//// 永远不会到达此处（除非所有客户端任务因未处理异常同时崩溃）

//// -----------------------------------------------------

//static async Task RunClientForeverAsync(int clientId, string host, int port, string mode, int qps)
//{
//    var rand = new Random(unchecked(Environment.TickCount + clientId));
//    var submitIntervalMs = Math.Max(8000 / qps, 4000); // 每个客户端的速率控制
//    var reconnectDelayMs = 1000;                    // 重连间隔

//    // 持续运行：自动重连
//    while (true)
//    {
//        using var tcp = new TcpClient();
//        try
//        {
//            await tcp.ConnectAsync(host, port);
//            using var stream = tcp.GetStream();
//            using var reader = new StreamReader(stream, Encoding.ASCII);
//            using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };

//            Console.WriteLine($"[C{clientId:00}] Connected.");

//            // 跟踪本客户端的 pending 任务（仅用于日志）
//            var pendingTaskIds = new ConcurrentDictionary<string, byte>();

//            // 启动读循环：持续处理服务器返回
//            var readLoop = Task.Run(async () =>
//            {
//                try
//                {
//                    while (true)
//                    {
//                        var line = await reader.ReadLineAsync();
//                        if (line is null)
//                        {
//                            Console.WriteLine($"[C{clientId:00}] Server closed.");
//                            break;
//                        }
//                        HandleServerMessage(clientId, line, pendingTaskIds);
//                    }
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"[C{clientId:00}] Read err: {ex.Message}");
//                }
//            });

//            // 提交循环：持续不断提交
//            var seq = 0;
//            while (true)
//            {
//                seq++;

//                // 选择模式
//                var m = PickMode(mode, seq);
//                var (payload, taskIdHint) = BuildSubmitPayload(clientId, seq, m, rand);

//                await writer.WriteLineAsync(payload);
//                Console.WriteLine($"[C{clientId:00}] -> submit #{seq}: {taskIdHint}");

//                await Task.Delay(submitIntervalMs);
//            }

//            // 永不到达
//            // await readLoop;
//        }
//        catch (Exception ex)
//        {
//            Console.WriteLine($"[C{clientId:00}] Connect/Send err: {ex.Message}. Reconnecting after {reconnectDelayMs}ms...");
//            await Task.Delay(reconnectDelayMs);
//        }
//    }
//}

//static string PickMode(string mode, int seq)
//{
//    // 简单轮换：zero/scan/auto_peak
//    var r = seq % 2;
//    return r == 1 ? "zero" : "scan";
//}

//static string BuildResultQuery(string taskId)
//{
//    var payloadObj = new { command = "result", taskId };
//    return JsonSerializer.Serialize(payloadObj);
//}

//// 支持 ack 收集 taskId 和 result 删除 taskId（仅日志用途，不影响持续提交）
//static void HandleServerMessage(int clientId, string line, ConcurrentDictionary<string, byte> pendingTaskIds)
//{
//    try
//    {
//        using var doc = JsonDocument.Parse(line);
//        var root = doc.RootElement;
//        var command = root.TryGetProperty("command", out var vop) ? vop.GetString() : "";
//        switch (command)
//        {
//            case "ack":
//                var taskIdAck = root.GetProperty("taskId").GetString();
//                if (!string.IsNullOrEmpty(taskIdAck))
//                {
//                    pendingTaskIds.TryAdd(taskIdAck!, 0);
//                }
//                Console.WriteLine($"[C{clientId:00}] <- ack taskId={taskIdAck}");
//                break;

//            case "result":
//                var success = root.GetProperty("success").GetBoolean();
//                string? taskIdRes = root.TryGetProperty("taskId", out var tidEl) ? tidEl.GetString() : null;
//                if (!string.IsNullOrEmpty(taskIdRes))
//                    pendingTaskIds.TryRemove(taskIdRes!, out _);

//                if (success)
//                {
//                    var data = root.GetProperty("data");
//                    var mode = data.TryGetProperty("mode", out var m) ? m.GetString() : "";
//                    if (mode == "scan")
//                    {
//                        Console.WriteLine($"[C{clientId:00}] <- result scan: res={data.GetProperty("resolution_m").GetDouble():F6} count={data.GetProperty("point_count").GetInt32()} len={data.GetProperty("segment_length_m").GetDouble():F3}m");
//                    }
//                    else if (mode == "auto_peak")
//                    {
//                        Console.WriteLine($"[C{clientId:00}] <- result peak: pos={data.GetProperty("peak_pos_m").GetDouble():F3}m db={data.GetProperty("peak_db").GetDouble():F3}dB sn={data.GetProperty("sn").GetString()}");
//                    }
//                    else
//                    {
//                        Console.WriteLine($"[C{clientId:00}] <- result: {line}");
//                    }
//                }
//                else
//                {
//                    var err = root.TryGetProperty("error", out var e) ? e.GetString() : "";
//                    Console.WriteLine($"[C{clientId:00}] <- result error: {err}");
//                }
//                break;

//            default:
//                Console.WriteLine($"[C{clientId:00}] <- {line}");
//                break;
//        }
//    }
//    catch
//    {
//        Console.WriteLine($"[C{clientId:00}] <- (raw) {line}");
//    }
//}

//static (string payload, string taskIdHint) BuildSubmitPayload(int clientId, int seq, string mode, Random rand)
//{
//    if (mode == "scan")
//    {
//        var zero_length = (rand.NextDouble() * 30).ToString("F1"); // 0.0 ~ 30.0
//        var payloadObj = new
//        {
//            command = "submit",
//            clientId,
//            mode = "scan",
//            @params = new { zero_length }
//        };
//        var json = JsonSerializer.Serialize(payloadObj);
//        return (json, $"scan-{seq}");
//    }
//    else
//    {
//        var payloadObj = new
//        {
//            command = "submit",
//            clientId,
//            mode = "zero",
//        };
//        var json = JsonSerializer.Serialize(payloadObj);
//        return (json, $"zero-{seq}");
//    }
//}