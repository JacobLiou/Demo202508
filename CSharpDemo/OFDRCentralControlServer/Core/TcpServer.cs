using OFDRCentralControlServer.Models;
using Serilog;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Channels;

namespace OFDRCentralControlServer.Core
{
    public class TcpServer
    {
        private readonly int _port;

        private readonly Channel<MeasureTask> _channel;

        private TcpListener? _listener;

        private readonly List<ClientSession> _sessions = new();

        private readonly object _lock = new();

        private readonly CancellationToken _serverLifetime;

        public TcpServer(int port, Channel<MeasureTask> channel)
        {
            _port = port;
            _channel = channel;
        }

        public async Task StartAsync(CancellationToken ct)
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.Start(100);

            while (!ct.IsCancellationRequested)
            {
                TcpClient? client = null;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    Log.Error($"OperationCanceledException");
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error($"[Server] Accept error: {ex.Message}");
                    continue;
                }

                var session = new ClientSession(client);
                lock (_lock)
                    _sessions.Add(session);

                Log.Information($"[Server] Client connected: {session.RemoteEndPoint}");
                _ = HandleClientAsync(session, ct);
            }

            try
            {
                _listener?.Stop();
            }
            catch
            {
            }
        }

        private async Task HandleClientAsync(ClientSession session, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    string? line;
                    try
                    {
                        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        readCts.CancelAfter(TimeSpan.FromSeconds(10));

                        line = await session.ReadLineAsync(readCts.Token);
                    }
                    catch (OperationCanceledException) // 读超时或取消 -> 不等于断开
                    {
                        // 空闲超时：继续循环（可以选择发心跳）
                        Log.Debug("[Server] ReadLine timeout/canceled: {remote}", session.RemoteEndPoint);
                        line = string.Empty;
                        await session.SendAsync(new { command = "ping" }, ct);
                        await Task.Delay(100, ct); // 避免紧循环
                        continue;
                    }
                    catch (TimeoutException ex) // 如果你的 ReadLineAsync 会抛这个
                    {
                        Log.Debug("[Server] ReadLine timeout for {remote}: {msg}", session.RemoteEndPoint, ex.Message);
                        await Task.Delay(100, ct); // 避免紧循环
                        continue;
                    }
                    catch (IOException ex)
                    {
                        Log.Error("[Server] ReadLine I/O error for {remote}: {msg}", session.RemoteEndPoint, ex.Message);
                        break; // 通常视为连接异常结束
                    }
                    catch (Exception ex)
                    {
                        Log.Error("[Server] ReadLine error for {remote}: {msg}", session.RemoteEndPoint, ex.Message);
                        break; // 其它未知异常，结束会话更稳妥
                    }

                    // 仅当 line == null 才认为断开（EOF）
                    if (line is null)
                    {
                        Log.Debug("[Server] EOF -> client disconnected: {remote}", session.RemoteEndPoint);
                        break;
                    }

                    // 空行或纯空白：不是断开，跳过
                    if (string.IsNullOrEmpty(line))
                    {
                        Log.Debug("[Server] Blank line -> continue: {remote}", session.RemoteEndPoint);
                        continue;
                    }

                    Log.Debug("Resolve Request");
                    Request? req = null;
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    try
                    {
                        req = JsonSerializer.Deserialize<Request>(line, options);
                    }
                    catch (Exception ex)
                    {
                        await session.SendAsync(new { Command = "error", message = "invalid json", detail = ex.Message }, ct);
                        Log.Error($"invalid json error: {ex.Message}");
                        continue;
                    }

                    if (req?.Command is null)
                    {
                        await session.SendAsync(new { Command = "error", message = "missing command" }, ct);
                        continue;
                    }

                    switch (req.Command.ToLowerInvariant())
                    {
                        case "submit":
                            var reqSubmit = JsonSerializer.Deserialize<SubmitRequest>(line, options);
                            if (reqSubmit == null)
                            {
                                await session.SendAsync(new { command = "error", message = "invalid submit request" }, ct);
                                break;
                            }

                            var taskId = $"T{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid().ToString()[..8]}";
                            var task = new MeasureTask(taskId, session, reqSubmit.ClientId, reqSubmit.Mode!, reqSubmit.Params ?? new(), DateTime.UtcNow);
                            await _channel.Writer.WriteAsync(task, ct);
                            RunningTaskTracker.Instance.MarkQueued(task.TaskId);
                            // send ack with unified field names
                            await session.SendAsync(new AckMessage("ack", taskId, reqSubmit.ClientId, reqSubmit.Mode), ct);
                            break;

                        case "result":
                            var reqStatus = JsonSerializer.Deserialize<ResultRequest>(line, options);
                            var tId = reqStatus?.TaskId != null ? reqStatus.TaskId : "";
                            if (string.IsNullOrEmpty(tId))
                            {
                                await session.SendAsync(new ResultMessage("result", tId, status: "unknown"), ct);
                                break;
                            }
                            // 优先判断是否已经有结果
                            if (HourlyResultStore.Instance.TryGet(tId, out var sResult))
                            {
                                // stored final result (status=complete + success/data/error)
                                await session.SendAsync(sResult!, ct);
                            }
                            else if (RunningTaskTracker.Instance.IsQueued(tId))
                            {
                                await session.SendAsync(new ResultMessage("result", tId, status: "queued"), ct);
                            }
                            else if (RunningTaskTracker.Instance.IsRunning(tId))
                            {
                                await session.SendAsync(new ResultMessage("result", tId, status: "running"), ct);
                            }
                            else
                            {
                                await session.SendAsync(new ResultMessage("result", tId, status: "expired"), ct);
                            }
                            break;

                        default:
                            await session.SendAsync(new { command = "error", message = "unknown command" }, ct);
                            break;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log.Error($"[Server] Client session error: {ex.Message}");
            }
            finally
            {
                Log.Information($"[Server] Client disconnected: {session.RemoteEndPoint}");
                session.Close();
                lock (_lock)
                    _sessions.Remove(session);
            }
        }

        public async Task SendResultAsync(MeasureTask task, object payload, CancellationToken ct)
            => await task.Session.SendAsync(payload, ct);
    }
}