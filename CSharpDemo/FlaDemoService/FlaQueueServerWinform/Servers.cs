using Serilog;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace OTMS
{
    public record SubmitRequest(string? Op, int Channel, string Mode, Dictionary<string, string>? @params);
    public record AckMessage(string op, string taskId);
    public record StatusMessage(string op, string taskId, string status, string? error = null);
    public record ResultMessage(string op, string taskId, bool success, object? data = null, string? error = null);

    public class ClientSession
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        public string Id { get; } = Guid.NewGuid().ToString("N").Substring(0, 8);
        public EndPoint? RemoteEndPoint => _client.Client?.RemoteEndPoint;
        public bool Connected => _client.Connected;

        public ClientSession(TcpClient client)
        { _client = client; _stream = client.GetStream(); _reader = new StreamReader(_stream, Encoding.UTF8); _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = true }; }

        public async Task<string?> ReadLineAsync(CancellationToken ct)
        { try { return await _reader.ReadLineAsync(ct); } catch { return null; } }

        public async Task SendAsync(object obj, CancellationToken ct)
        { var json = JsonSerializer.Serialize(obj); await _writeLock.WaitAsync(ct); try { await _writer.WriteLineAsync(json); } finally { _writeLock.Release(); } }

        public void Close()
        { try { _writer.Dispose(); } catch { } try { _reader.Dispose(); } catch { } try { _stream.Dispose(); } catch { } try { _client.Close(); } catch { } }
    }

    public record MeasureTask(string TaskId, ClientSession Session, int Channel, string Mode, Dictionary<string, string> Params, DateTime CreatedAt);

    public class TcpServer
    {
        private readonly int _port; private readonly Channel<MeasureTask> _queue; private TcpListener? _listener; private CancellationToken _ct;

        public event Action<string, string>? ClientConnected; // (id, remote)

        public event Action<string, string>? ClientDisconnected;

        public event Action<MeasureTask>? TaskQueued;

        public event Action<string, bool>? ResultPushed; // (taskId,success)

        public TcpServer(int port, Channel<MeasureTask> queue)
        { _port = port; _queue = queue; }

        public async Task StartAsync(CancellationToken ct)
        {
            _ct = ct; _listener = new TcpListener(IPAddress.Any, _port); _listener.Start(); Log.Information("WinForms TCP gateway listening on :{Port}", _port);
            while (!ct.IsCancellationRequested)
            {
                TcpClient? client = null; try { client = await _listener.AcceptTcpClientAsync(ct); } catch (OperationCanceledException) { break; } catch (Exception ex) { Log.Error(ex, "Accept error"); continue; }
                var session = new ClientSession(client); ClientConnected?.Invoke(session.Id, session.RemoteEndPoint?.ToString() ?? "");
                _ = HandleClientAsync(session, ct);
            }
            try { _listener?.Stop(); } catch { }
        }

        private async Task HandleClientAsync(ClientSession session, CancellationToken ct)
        {
            try
            {
                await session.SendAsync(new { op = "hello", message = "OTMS ready" }, ct);
                while (!ct.IsCancellationRequested && session.Connected)
                {
                    var line = await session.ReadLineAsync(ct); if (line == null) break; if (string.IsNullOrWhiteSpace(line)) continue;
                    Log.Debug("<= {Remote}: {Line}", session.RemoteEndPoint, line);
                    SubmitRequest? req = null; try { req = JsonSerializer.Deserialize<SubmitRequest>(line, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); } catch (Exception ex) { Log.Warning(ex, "Invalid JSON from {Remote}", session.RemoteEndPoint); await session.SendAsync(new { op = "error", message = "invalid json" }, ct); continue; }
                    if (req?.Op is null) { await session.SendAsync(new { op = "error", message = "missing op" }, ct); continue; }
                    switch (req.Op.ToLowerInvariant())
                    {
                        case "ping": await session.SendAsync(new { op = "pong" }, ct); break;
                        case "submit":
                            var taskId = $"T{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid().ToString()[..8]}";
                            var task = new MeasureTask(taskId, session, req.Channel, req.Mode, req.@params ?? new(), DateTime.UtcNow);
                            await _queue.Writer.WriteAsync(task, ct);
                            TaskQueued?.Invoke(task);
                            await session.SendAsync(new AckMessage("ack", taskId), ct);
                            break;

                        case "status":
                            var tid = req.@params != null && req.@params.TryGetValue("taskId", out var x) ? x : "";
                            await session.SendAsync(new StatusMessage("status", tid, "queued"), ct);
                            break;

                        default:
                            await session.SendAsync(new { op = "error", message = "unknown op" }, ct);
                            break;
                    }
                }
            }
            catch (Exception ex) { Log.Error(ex, "Client session error"); }
            finally { ClientDisconnected?.Invoke(session.Id, session.RemoteEndPoint?.ToString() ?? ""); session.Close(); }
        }

        public async Task SendResultAsync(MeasureTask task, object payload, CancellationToken ct)
        { await task.Session.SendAsync(payload, ct); ResultPushed?.Invoke(task.TaskId, (payload as ResultMessage)?.success ?? true); }
    }
}