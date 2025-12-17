using Serilog;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Channels;

namespace FlaQueueServer.Core
{




    // ======= 业务 DTO（可替换为你现有的 Request/SubmitRequest/ResultRequest 等）=======
    public sealed record SubmitRequest(string? Command, string ClientId, string Mode, Dictionary<string, object>? Params);
    public sealed record ResultRequest(string? Command, string TaskId);
    public sealed record AckMessage(string Command, string TaskId, string ClientId, string Mode);
    public sealed record ResultMessage(string Command, string TaskId, string Status, object? Data = null, string? Error = null);

    // 简单的结果缓存（替换为你已有的 HourlyResultStore）
    public sealed class ResultStore
    {
        private readonly ConcurrentDictionary<string, ResultMessage> _map = new();
        public void Set(ResultMessage msg) => _map[msg.TaskId] = msg;
        public bool TryGet(string id, out ResultMessage? msg) => _map.TryGetValue(id, out msg);
        public static ResultStore Instance { get; } = new ResultStore();
    }

    public sealed class FastTcpServer : IAsyncDisposable
    {
        private readonly int _port;
        private readonly Channel<MeasureTask> _queue;  // 沿用你的测量任务队列
        private readonly Socket _listenSocket;
        private readonly CancellationTokenSource _cts = new();
        private readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public FastTcpServer(int port, Channel<MeasureTask> queue)
        {
            _port = port;
            _queue = queue;

            _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
                ReceiveBufferSize = 64 * 1024, // 可按压测调整
                SendBufferSize = 64 * 1024
            };
            _listenSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            // 可选：启用 KeepAlive（跨平台写法不同，Windows 需要 IOControl；这里简单设置 SO_KEEPALIVE）
            _listenSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        }

        public async Task StartAsync(CancellationToken externalCt = default)
        {
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, externalCt);
            var ct = linkedCts.Token;

            _listenSocket.Bind(new IPEndPoint(IPAddress.Any, _port));
            _listenSocket.Listen(backlog: 1024);
            Log.Information($"[Server] Listening on 0.0.0.0:{_port}");

            // 接收循环
            while (!ct.IsCancellationRequested)
            {
                Socket? client = null;
                try
                {
#if NET8_0_OR_GREATER
                    client = await _listenSocket.AcceptAsync(ct);
#else
                    client = await _listenSocket.AcceptAsync();
#endif
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Log.Error($"[Server] Accept error: {ex.Message}");
                    continue;
                }

                client.NoDelay = true;
                client.ReceiveBufferSize = 64 * 1024;
                client.SendBufferSize = 64 * 1024;

                _ = HandleConnectionAsync(client, ct); // fire-and-forget
            }
        }

        public async ValueTask DisposeAsync()
        {
            try { _cts.Cancel(); } catch { }
            try { _listenSocket.Close(); _listenSocket.Dispose(); } catch { }
            await Task.CompletedTask;
        }

        // ======= 每连接的处理协程（Pipelines 读写 + 行帧解析 + 轻量 JSON 解析）=======
        private async Task HandleConnectionAsync(Socket socket, CancellationToken ct)
        {
            var remote = socket.RemoteEndPoint?.ToString() ?? "unknown";
            Log.Information($"[Server] Client connected: {remote}");

            using var ns = new NetworkStream(socket, ownsSocket: true);
            var reader = PipeReader.Create(ns, new StreamPipeReaderOptions(bufferSize: 64 * 1024, minimumReadSize: 4096));
            var writer = PipeWriter.Create(ns, new StreamPipeWriterOptions(minimumBufferSize: 4096));

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var readResult = await reader.ReadAsync(ct);
                    var buf = readResult.Buffer;

                    while (TryReadLine(ref buf, out var line))
                    {
                        if (line.Length == 0) continue;

                        // 解析命令（只取 command 字段，降低反序列化开销）
                        var cmd = ReadCommand(line);
                        if (cmd is null)
                        {
                            await WriteJsonAsync(writer, new { command = "error", message = "invalid json" }, ct);
                            continue;
                        }

                        switch (cmd)
                        {
                            case "submit":
                                SubmitRequest? submit;

                                if (line.IsSingleSegment)
                                {
                                    // .NET 6/7/8 均可：Span 重载
                                    submit = JsonSerializer.Deserialize<SubmitRequest>(line.FirstSpan, _jsonOpts);
                                }
                                else
                                {
                                    // 多段缓冲：拼成连续数组（会有一次分配）
                                    var bytes = line.ToArray();
                                    submit = JsonSerializer.Deserialize<SubmitRequest>(bytes, _jsonOpts);
                                }

                                if (submit is null || string.IsNullOrWhiteSpace(submit.ClientId) || string.IsNullOrWhiteSpace(submit.Mode))
                                {
                                    await WriteJsonAsync(writer, new { command = "error", message = "invalid submit request" }, ct);
                                    break;
                                }
                                var taskId = $"T{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}".Substring(0, 32); // 简短ID
                                var task = new MeasureTask(
                                    taskId,
                                    /* TODO: 如需会话对象，可封装 session 对象，或在 MeasureTask 中仅保存回写管道 */
                                    Session: new PipeSession(writer),       // —— 示例替代你原来的 ClientSession
                                    ClientId: submit.ClientId,
                                    Mode: submit.Mode,
                                    @params: submit.Params ?? new Dictionary<string, object>(),
                                    createdUtc: DateTime.UtcNow
                                );

                                // 背压（有界 Channel 建议：Channel.CreateBounded）
                                if (!_queue.Writer.TryWrite(task))
                                {
                                    await WriteJsonAsync(writer, new { command = "result", taskId, status = "busy" }, ct);
                                    break;
                                }

                                // ACK
                                await WriteJsonAsync(writer, new AckMessage("ack", taskId, submit.ClientId, submit.Mode), ct);
                                break;

                            case "result":
                                ResultRequest? resultReq;

                                if (line.IsSingleSegment)
                                {
                                    // .NET 6/7/8 均可：Span 重载
                                    resultReq = JsonSerializer.Deserialize<ResultRequest>(line.FirstSpan, _jsonOpts);
                                }
                                else
                                {
                                    // 多段缓冲：拼成连续数组（会有一次分配）
                                    var bytes = line.ToArray();
                                    resultReq = JsonSerializer.Deserialize<ResultRequest>(bytes, _jsonOpts);
                                }

                                var tId = resultReq?.TaskId ?? string.Empty;

                                // TODO: RunningTaskTracker（替换为你的实现）
                                var running = false; // RunningTaskTracker.Instance.IsRunning(tId);
                                if (running)
                                {
                                    await WriteJsonAsync(writer, new ResultMessage("result", tId, "running"), ct);
                                }
                                else if (ResultStore.Instance.TryGet(tId, out var final))
                                {
                                    await WriteJsonAsync(writer, final!, ct);
                                }
                                else
                                {
                                    await WriteJsonAsync(writer, new ResultMessage("result", tId, "queued"), ct);
                                }
                                break;

                            default:
                                await WriteJsonAsync(writer, new { command = "error", message = "unknown command" }, ct);
                                break;
                        }
                    }

                    reader.AdvanceTo(buf.Start, buf.End);
                    if (readResult.IsCompleted) break;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log.Error($"[Server] Client error: {ex.Message}");
            }
            finally
            {
                try { await writer.FlushAsync(ct); } catch { }
                Log.Information($"[Server] Client disconnected: {remote}");
            }
        }

        // ======= 行帧解析（支持 CRLF / LF）=======

        private static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
        {
            var reader = new SequenceReader<byte>(buffer);

            // 找到 LF（\n）作为行结束
            if (reader.TryReadTo(out ReadOnlySequence<byte> tmp, (byte)'\n', advancePastDelimiter: true))
            {
                // 如果存在 CR（\r），把它去掉
                if (tmp.Length > 0)
                {
                    // 取最后一个字节（跨段安全）
                    var lastByte = tmp.Slice(tmp.Length - 1, 1).FirstSpan[0];
                    if (lastByte == (byte)'\r')
                    {
                        tmp = tmp.Slice(0, tmp.Length - 1);
                    }
                }

                line = tmp;
                // 将外部 buffer 前移到当前 reader 位置
                buffer = buffer.Slice(reader.Position);
                return true;
            }

            line = default;
            return false;
        }

        // ======= 仅解析 command，减少负载 =======
        private static string? ReadCommand(ReadOnlySequence<byte> json)
        {
            var reader = new Utf8JsonReader(json.ToArray()); // 小型帧场景可接受；极致优化可自定义 reader 迭代
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName &&
                    reader.ValueTextEquals("command"))
                {
                    if (reader.Read() && reader.TokenType == JsonTokenType.String)
                        return reader.GetString()?.ToLowerInvariant();
                }
            }
            return null;
        }

        // ======= 写 JSON（PipeWriter + Utf8JsonWriter）=======
        private static async Task WriteJsonAsync(PipeWriter writer, object payload, CancellationToken ct)
        {
            //var mem = writer.GetMemory(1024);
            //using var jsonWriter = new Utf8JsonWriter(mem, new JsonWriterOptions { SkipValidation = false });
            //JsonSerializer.Serialize(jsonWriter, payload);
            //jsonWriter.Flush();

            //var written = jsonWriter.BytesCommitted;
            //writer.Advance(written);

            //// 行分隔（与客户端协议一致）
            //var nl = writer.GetSpan(1);
            //nl[0] = (byte)'\n';
            //writer.Advance(1);

            await writer.FlushAsync(ct);
        }

        // ======= 示例：把 PipeWriter 封装为会话，用于你的 SendResultAsync 等回写 =======
        private sealed class PipeSession : IResultSession
        {
            private readonly PipeWriter _writer;
            public PipeSession(PipeWriter writer) => _writer = writer;

            public Task SendAsync(object payload, CancellationToken ct) => WriteJsonAsync(_writer, payload, ct);
        }

        // ======= 你现有代码中使用的 MeasureTask / IResultSession 接口（示意）=======
        public interface IResultSession
        {
            Task SendAsync(object payload, CancellationToken ct);
        }

        public sealed record MeasureTask(
            string TaskId,
            IResultSession Session,
            string ClientId,
            string Mode,
            Dictionary<string, object> @params,
            DateTime createdUtc
        );
    }
}


