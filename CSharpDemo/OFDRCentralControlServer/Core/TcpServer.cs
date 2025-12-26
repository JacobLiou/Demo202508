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

        public TcpServer(int port, Channel<MeasureTask> channel)
        {
            _port = port;
            _channel = channel;
        }

        public async Task StartAsync(CancellationToken ct)
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();

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
                while (!ct.IsCancellationRequested && session.Connected)
                {
                    var line = await session.ReadLineAsync(ct);
                    if (line == null) break; // 客户端断开
                    if (string.IsNullOrWhiteSpace(line)) continue;

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