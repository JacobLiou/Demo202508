using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

// =====================================================
// FlaClientMock - 模拟最多16个客户端的 TCP 长连接 & 提交队列请求
// .NET 8 Console
// =====================================================
// 用法:
//   dotnet run -- <host> <port> <clients> <tasksPerClient> <mode> <staggerMs>
// 示例:
//   dotnet run -- 127.0.0.1 5600 16 3 scan 100
//   dotnet run -- 127.0.0.1 5600 8 5 auto_peak 200
// 参数说明:
//   host            服务器地址 (默认 127.0.0.1)
//   port            服务器端口 (默认 5600)
//   clients         客户端数量 (默认 16, 最大 16)
//   tasksPerClient  每个客户端提交的任务数 (默认 2)
//   mode            scan | auto_peak (默认 scan)
//   staggerMs       客户端启动错峰毫秒 (默认 50)
// =====================================================

var host = args.Length > 0 ? args[0] : "127.0.0.1";
var port = args.Length > 1 && int.TryParse(args[1], out var p) ? p : 5600;
var clients = args.Length > 2 && int.TryParse(args[2], out var c) ? Math.Min(Math.Max(c, 1), 16) : 16;
var tasksPerClient = args.Length > 3 && int.TryParse(args[3], out var tpc) ? Math.Max(tpc, 1) : 2;
var mode = args.Length > 4 ? args[4].ToLowerInvariant() : "zero";
var staggerMs = args.Length > 5 && int.TryParse(args[5], out var st) ? st : 50;

Console.WriteLine($"[Mock] Host={host} Port={port} Clients={clients} TasksPerClient={tasksPerClient} Mode={mode} StaggerMs={staggerMs}");
Console.WriteLine("[Mock] Press Ctrl+C to stop.");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

var tasks = new List<Task>();
for (int i = 1; i <= clients; i++)
{
    await Task.Delay(staggerMs, cts.Token);
    tasks.Add(RunClientAsync(i, host, port, tasksPerClient, mode, cts.Token));
}

await Task.WhenAll(tasks);

Thread.Sleep(2000); // 等待日志打印完
Console.WriteLine("[Mock] All clients completed.");


// =====================================================

static async Task RunClientAsync(int clientId, string host, int port, int tasksPerClient, string mode, CancellationToken ct)
{
    using var tcp = new TcpClient();
    await tcp.ConnectAsync(host, port, ct);
    using var stream = tcp.GetStream();
    using var reader = new StreamReader(stream, Encoding.UTF8);
    using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

    Console.WriteLine($"[C{clientId:00}] Connected.");

    // 用于跟踪本客户端提交的任务: ack 时收集 taskId，result 时标记完成
    var pendingTaskIds = new ConcurrentDictionary<string, byte>(); // value 不用，byte占位
    var allResultsArrived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

    // flag 表示主线程已完成所有 submit
    var allSubmitted = false;

    // 读取欢迎/ack/status/result 等消息
    _ = Task.Run(async () =>
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line is null) break; // 服务端断开
                HandleServerMessage(clientId, line, pendingTaskIds);

                // 如果主线程已提交完所有任务且 pending 为空，则可以结束读取并通知主线程
                if (allSubmitted && pendingTaskIds.IsEmpty)
                {
                    allResultsArrived.TrySetResult(true);
                    break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Console.WriteLine($"[C{clientId:00}] Read err: {ex.Message}"); }
        finally
        {
            // 如果读循环结束，但仍有未完成任务，提示异常退出
            if (!pendingTaskIds.IsEmpty)
                Console.WriteLine($"[C{clientId:00}] Connection closed while {pendingTaskIds.Count} task(s) still pending.");
            allResultsArrived.TrySetResult(true);
        }
    }, ct);

    var rand = new Random(unchecked(Environment.TickCount + clientId));

    // 提交任务
    for (int k = 1; k <= tasksPerClient; k++)
    {
        var (payload, taskIdHint) = BuildSubmitPayload(clientId, k, mode, rand);
        await writer.WriteLineAsync(payload);
        Console.WriteLine($"[C{clientId:00}] -> submit #{k}: {taskIdHint}");
        await Task.Delay(rand.Next(50, 200), ct); // 轻微节流
    }

    // 等待所有任务的 result（成功或异常）返回后再断开
    // 关键：我们需要在 ack 收到后将 taskId 放入 pendingTaskIds，
    // 一旦 result 到来则从 pending 中移除；当 pending 为空即可结束。
    // 为了避免“提交后尚未收到任何 ack 就判断空”的竞态，这里等待一个最短时间以收集 ack，
    // 然后轮询 pending 集合直到为空。
    var maxWaitAckMs = 200; // 适度等待 ack 进入集合
    await Task.Delay(maxWaitAckMs, ct);

    // 标记已提交完成，通知读线程可以在 pending 为空时结束
    allSubmitted = true;

    // 如果此时已经没有 pending，则直接设置完成，避免等待读线程再次收到消息
    if (pendingTaskIds.IsEmpty)
    {
        allResultsArrived.TrySetResult(true);
    }

    // 持续轮询已 ack 的 taskId 以请求状态，直到收到 result（result 到来时会在 HandleServerMessage 中移除 pending）
    _ = Task.Run(async () =>
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // 如果所有任务已提交且 pending 为空，退出查询
                if (allSubmitted && pendingTaskIds.IsEmpty) break;

                foreach (var taskId in pendingTaskIds.Keys)
                {
                    try
                    {
                        var q = BuildResultQuery(taskId);
                        await writer.WriteLineAsync(q);
                        // 轻微间隔，避免一次性刷爆服务器
                        await Task.Delay(50, ct);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex) { Console.WriteLine($"[C{clientId:00}] Query err: {ex.Message}"); }
                }

                // 每轮查询间隔
                await Task.Delay(500, ct);
            }
        }
        catch (OperationCanceledException) { }
    }, ct);

    // 若服务端没有ack（协议异常），也不能永远挂死：这个场景下继续监听，
    // 一旦服务端主动返回 result（没有ack也可以），pending不会为空，我们不结束。
    // 因此这里用一个循环等待 pending 为空，或读循环结束（服务端断开）。
    while (!ct.IsCancellationRequested)
    {
        if (pendingTaskIds.IsEmpty) break;
        await Task.Delay(50, ct);
    }

    // 等待读线程结束（保证打印完整日志）
    await allResultsArrived.Task;

    Console.WriteLine($"[C{clientId:00}] Completed. Closing.");
}

static string BuildResultQuery(string taskId)
{
    var payloadObj = new { command = "result", taskId };
    return JsonSerializer.Serialize(payloadObj);
}

// 修改：HandleServerMessage 支持 ack 收集 taskId 和 result 删除 taskId
static void HandleServerMessage(int clientId, string line, ConcurrentDictionary<string, byte> pendingTaskIds)
{
    try
    {
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        var command = root.TryGetProperty("command", out var vop) ? vop.GetString() : "";

        switch (command)
        {
            case "ack":
                var taskIdAck = root.GetProperty("taskId").GetString();
                if (!string.IsNullOrEmpty(taskIdAck))
                {
                    pendingTaskIds.TryAdd(taskIdAck!, 0);
                }
                Console.WriteLine($"[C{clientId:00}] <- ack taskId={taskIdAck}");
                break;

            //case "status":
            //    Console.WriteLine($"[C{clientId:00}] <- status {root.GetProperty("status").GetString()} taskId={root.GetProperty("taskId").GetString()}");
            //    break;

            case "result":
                var success = root.GetProperty("success").GetBoolean();
                string? taskIdRes = root.TryGetProperty("taskId", out var tidEl) ? tidEl.GetString() : null;

                // 移除 pending
                if (!string.IsNullOrEmpty(taskIdRes))
                    pendingTaskIds.TryRemove(taskIdRes!, out _);

                if (success)
                {
                    var data = root.GetProperty("data");
                    var mode = data.TryGetProperty("mode", out var m) ? m.GetString() : "";
                    if (mode == "scan")
                    {
                        Console.WriteLine($"[C{clientId:00}] <- result scan: res={data.GetProperty("resolution_m").GetDouble():F6} count={data.GetProperty("point_count").GetInt32()} len={data.GetProperty("segment_length_m").GetDouble():F3}m");
                    }
                    else if (mode == "auto_peak")
                    {
                        Console.WriteLine($"[C{clientId:00}] <- result peak: pos={data.GetProperty("peak_pos_m").GetDouble():F3}m db={data.GetProperty("peak_db").GetDouble():F3}dB sn={data.GetProperty("sn").GetString()}");
                    }
                    else
                    {
                        Console.WriteLine($"[C{clientId:00}] <- result: {line}");
                    }
                }
                else
                {
                    var err = root.TryGetProperty("error", out var e) ? e.GetString() : "";
                    Console.WriteLine($"[C{clientId:00}] <- result error: {err}");
                }
                break;

            default:
                Console.WriteLine($"[C{clientId:00}] <- {line}");
                break;
        }
    }
    catch
    {
        Console.WriteLine($"[C{clientId:00}] <- (raw) {line}");
    }
}

static (string payload, string taskIdHint) BuildSubmitPayload(int clientId, int seq, string mode, Random rand)
{
    if (mode == "scan")
    {
        var sr = (rand.Next(0, 3)).ToString();          // 0/1/2
        var gain = new[] { "1", "2", "5", "10" }[rand.Next(0, 4)];
        var wr = (rand.NextDouble() * 20 + 1).ToString("F2"); // 1.00 ~ 21.00
        var xc = (rand.NextDouble() * 30).ToString("F1");     // 0.0 ~ 30.0
        var payloadObj = new
        {
            command = "submit",
            clientId,
            mode = "scan",
            @params = new { sr_mode = sr, gain, wr_len = wr, x_center = xc }
        };
        var json = JsonSerializer.Serialize(payloadObj);
        return (json, $"scan-{seq}");
    }
    else if (mode == "zero")
    {
        var payloadObj = new
        {
            command = "submit",
            clientId,
            mode = "zero",
        };
        var json = JsonSerializer.Serialize(payloadObj);
        return (json, $"zero-{seq}");
    }
    else // auto_peak
    {
        var start = (rand.NextDouble() * 1.0 + 0.2).ToString("F3"); // 0.2~1.2
        var end = (rand.NextDouble() * 24.0 + 1.0).ToString("F3");  // 1.0~25.0
        var width = (rand.NextDouble() * 0.7 + 0.3).ToString("F3"); // 0.3~1.0
        var thr = new[] { "-80", "-85", "-90" }[rand.Next(0, 3)];
        var id = clientId.ToString("00");
        var sn = $"SN{clientId}A{seq}";
        var payloadObj = new
        {
            command = "submit",
            clientId,
            mode = "auto_peak",
            @params = new { start_m = start, end_m = end, count_mode = "2", algo = "2", width_m = width, threshold_db = thr, id, sn }
        };
        var json = JsonSerializer.Serialize(payloadObj);
        return (json, $"peak-{seq}");
    }
}
