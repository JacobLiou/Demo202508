using FlaQueueServer.Models;
using Serilog;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Channels;

namespace FlaQueueServer.Core
{
    public class TcpServer
    {
        private readonly int _port;
        private readonly Channel<MeasureTask> _queue;
        private TcpListener? _listener;
        private readonly List<ClientSession> _sessions = new();
        private readonly object _lock = new();

        public TcpServer(int port, Channel<MeasureTask> queue)
        {
            _port = port;
            _queue = queue;
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
                await session.SendAsync(new { op = "hello", message = "FlaQueueServer ready" }, ct);
                while (!ct.IsCancellationRequested && session.Connected)
                {
                    var line = await session.ReadLineAsync(ct);
                    if (line == null) break; // 客户端断开
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    SubmitRequest? req = null;
                    try
                    {
                        req = JsonSerializer.Deserialize<SubmitRequest>(line, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }
                    catch (Exception ex)
                    {
                        await session.SendAsync(new { Command = "error", message = "invalid json", detail = ex.Message }, ct);
                        Log.Error($"invalid json error: {ex.Message}");
                        continue;
                    }

                    if (req?.Command is null)
                    {
                        await session.SendAsync(new { Command = "error", message = "missing op" }, ct);
                        continue;
                    }

                    switch (req.Command.ToLowerInvariant())
                    {
                        case "submit":
                            var taskId = $"T{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid().ToString()[..8]}";
                            var task = new MeasureTask(taskId, session, req.Channel, req.Mode, req.Params ?? new(), DateTime.UtcNow);
                            await _queue.Writer.WriteAsync(task, ct);
                            await session.SendAsync(new AckMessage("ack", taskId), ct);
                            break;

                        case "status":
                            var tId = req.Params != null && req.Params.TryGetValue("taskId", out var tid) ? tid : "";
                            await session.SendAsync(new StatusMessage("status", tId, "queued"), ct);
                            break;

                        case "result":
                            var qTID = req.Params != null && req.Params.TryGetValue("taskId", out var qTId) ? qTId : "";
                            if (DailyResultStore.Instance.TryGet(qTID, out var result))
                                await session.SendAsync(result!, ct);
                            else
                                await session.SendAsync(new ResultMessage("result", qTID, false, null, "Still Runing Use status to query"), ct);
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