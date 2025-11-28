using MIMS.Common;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace MIMS.Client
{
    public class PipeClient : IDisposable
    {
        private readonly string _pipeName;
        private readonly string _clientId;
        private NamedPipeClientStream _pipe;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly BlockingCollection<BusMessage> _sendQueue = new BlockingCollection<BusMessage>(new ConcurrentQueue<BusMessage>(), 2000);
        private readonly ConcurrentDictionary<string, PendingAck> _pendingAcks = new ConcurrentDictionary<string, PendingAck>();
        private DateTime _lastPong = DateTime.MinValue;

        public PipeClient(string pipeName, string clientId)
        {
            _pipeName = pipeName;
            _clientId = clientId;
        }

        public void Start()
        {
            new Thread(ConnectLoop) { IsBackground = true, Name = $"{_clientId}-Connect" }.Start();
            new Thread(WriteLoop) { IsBackground = true, Name = $"{_clientId}-Write" }.Start();
            new Thread(MonitorAcks) { IsBackground = true, Name = $"{_clientId}-AckMonitor" }.Start();
        }

        private volatile bool _isConnected = false;

        private void ConnectLoop()
        {
            int attempt = 0;
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    _pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut);
                    _pipe.Connect(3000);
                    _pipe.ReadMode = PipeTransmissionMode.Message;
                    _isConnected = true;
                    Console.WriteLine($"[{_clientId}] Connected to Hub.");

                    SendRaw(new BusMessage { Type = "Register", From = _clientId });
                    new Thread(ReadLoop) { IsBackground = true }.Start();
                    new Thread(HeartbeatLoop) { IsBackground = true }.Start();
                    return; // 成功后退出重连循环
                }
                catch
                {
                    _isConnected = false;
                    int backoff = Math.Min(30000, (int)Math.Pow(2, Math.Min(++attempt, 10)) * 500);
                    Console.WriteLine($"[{_clientId}] Connect failed, retry in {backoff}ms...");
                    Thread.Sleep(backoff);
                }
            }
        }



        private void ReadLoop()
        {
            var buf = new byte[64 * 1024];
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    int read = _pipe.Read(buf, 0, buf.Length);
                    if (read == 0) throw new IOException("server closed");
                    string json = Encoding.UTF8.GetString(buf, 0, read);
                    var msg = JsonConvert.DeserializeObject<BusMessage>(json);
                    if (msg == null) continue;

                    if (msg.Type == "Pong") { _lastPong = DateTime.Now; continue; }

                    if (msg.Type == "ACK" && !string.IsNullOrEmpty(msg.CorrelationId))
                    {
                        _pendingAcks.TryRemove(msg.CorrelationId, out _);
                        Console.WriteLine($"[{_clientId}] ACK for {msg.CorrelationId}");
                        continue;
                    }

                    if (msg.Type == "Data")
                    {
                        Console.WriteLine($"[{_clientId}] Data from {msg.From}: {msg.Payload}");
                        // business: auto reply
                        SendTo(msg.From, $"Reply to {msg.From}: got '{msg.Payload}'");
                        // ACK back to sender
                        SendRaw(new BusMessage { Type = "ACK", From = _clientId, To = msg.From, CorrelationId = msg.CorrelationId });
                        continue;
                    }

                    if (msg.Type == "Reply")
                    {
                        Console.WriteLine($"[{_clientId}] Reply from {msg.From}: {msg.Payload}");
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_clientId}] Read error: {ex.Message}, reconnecting...");
                TryReconnect();
            }
        }

        private void HeartbeatLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                SendRaw(new BusMessage { Type = "Ping", From = _clientId });
                Thread.Sleep(5000);
                if (_lastPong != DateTime.MinValue && (DateTime.Now - _lastPong).TotalSeconds > 20)
                {
                    Console.WriteLine($"[{_clientId}] Heartbeat lost (>20s), reconnecting...");
                    TryReconnect();
                }
            }
        }

        private void WriteLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var msg = _sendQueue.Take(_cts.Token);
                    SendRaw(msg);
                    if (msg.Type == "Data" || msg.Type == "Reply")
                    {
                        if (string.IsNullOrEmpty(msg.CorrelationId)) msg.CorrelationId = Guid.NewGuid().ToString();
                        _pendingAcks[msg.CorrelationId] = new PendingAck(msg, DateTime.Now);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{_clientId}] WriteLoop error: {ex.Message}");
                }
            }
        }

        private void MonitorAcks()
        {
            while (!_cts.IsCancellationRequested)
            {
                foreach (var kvp in _pendingAcks.ToArray())
                {
                    if ((DateTime.Now - kvp.Value.Timestamp).TotalSeconds > 10)
                    {
                        Console.WriteLine($"[{_clientId}] Resend due to ACK timeout: {kvp.Key}");
                        Enqueue(kvp.Value.Message); // re-enqueue for resend
                        _pendingAcks[kvp.Key] = new PendingAck(kvp.Value.Message, DateTime.Now);
                    }
                }
                Thread.Sleep(2000);
            }
        }

        private void SendRaw(BusMessage msg)
        {
            if (_pipe == null || !_pipe.IsConnected)
            {
                Console.WriteLine($"[{_clientId}] Pipe not connected, re-enqueue message.");
                Enqueue(msg);
                return;
            }

            try
            {
                var json = JsonConvert.SerializeObject(msg);
                var data = Encoding.UTF8.GetBytes(json);
                _pipe.Write(data, 0, data.Length);
                _pipe.Flush();
                Console.WriteLine($"[{_clientId}] → {(msg.To ?? "*")} Type={msg.Type} Corr={msg.CorrelationId ?? "-"}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_clientId}] SendRaw failed: {ex.Message}");
                TryReconnect();
                // after reconnect, retry
                Enqueue(msg);
            }
        }

        private void TryReconnect()
        {
            try
            {
                _pipe?.Dispose();
            }
            catch { }
            // ConnectLoop will run again and re-register
        }

        public void Enqueue(BusMessage msg)
        {
            if (!_sendQueue.TryAdd(msg, 500))
                Console.WriteLine($"[{_clientId}] send queue full, drop Type={msg.Type}");
        }

        public void SendTo(string targetId, string payload)
        {
            var msg = new BusMessage { Type = "Data", From = _clientId, To = targetId, Payload = payload, CorrelationId = Guid.NewGuid().ToString() };
            Enqueue(msg);
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _pipe?.Dispose(); } catch { }
        }
    }
}