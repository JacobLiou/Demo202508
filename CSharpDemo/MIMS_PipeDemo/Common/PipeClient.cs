using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace MIMS.Common
{
    public class PipeClient : IDisposable
    {
        private readonly string _pipeName;
        private readonly string _clientId;
        private NamedPipeClientStream _pipe;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly BlockingCollection<BusMessage> _sendQueue = new BlockingCollection<BusMessage>(new ConcurrentQueue<BusMessage>(), 2000);
        //private readonly ConcurrentDictionary<string, PendingAck> _pendingAcks = new ConcurrentDictionary<string, PendingAck>();
        private DateTime _lastPong = DateTime.MinValue;

        private Thread _ReadLoopThread;
        private Thread _HeartbeatLoopThread;

        public PipeClient(string pipeName, string clientId)
        {
            _pipeName = pipeName;
            _clientId = clientId;
        }

        public void Start()
        {
            new Thread(ConnectLoop) { IsBackground = true, Name = $"{_clientId}-Connect" }.Start();
            new Thread(WriteLoop) { IsBackground = true, Name = $"{_clientId}-Write" }.Start();
            //new Thread(MonitorAcks) { IsBackground = true, Name = $"{_clientId}-AckMonitor" }.Start();

            // start a single heartbeat thread that will use the currently connected pipe
            _HeartbeatLoopThread = new Thread(HeartbeatLoop) { IsBackground = true, Name = $"{_clientId}-Heartbeat" };
            _HeartbeatLoopThread.Start();
        }

        private volatile bool _isConnected = false;

        private void ConnectLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    if (_pipe != null && _pipe.IsConnected)
                    {
                        _isConnected = true;
                        Thread.Sleep(2000);
                        continue;
                    }

                    // Dispose old pipe if any
                    try { _pipe?.Dispose(); } catch { }

                    _pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                    _pipe.Connect(3000);
                    _pipe.ReadMode = PipeTransmissionMode.Message;
                    _isConnected = true;
                    Console.WriteLine($"[{_clientId}] Connected to Hub.");

                    // Register immediately
                    SendRaw(new BusMessage { Type = "Register", From = _clientId });

                    // start read loop for this connection
                    if (_ReadLoopThread == null || !_ReadLoopThread.IsAlive)
                    {
                        _ReadLoopThread = new Thread(ReadLoop) { IsBackground = true };
                        _ReadLoopThread.Start();
                    }
                }
                catch (Exception ex)
                {
                    _isConnected = false;
                    // ensure pipe disposed
                    try { _pipe?.Dispose(); } catch { }
                    _pipe = null;
                    // wait before retry
                    Console.WriteLine($"[{_clientId}] Connect error: {ex.Message}");
                    Thread.Sleep(2000);
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
                    if (_pipe == null) { Thread.Sleep(200); continue; }

                    using (var ms = new MemoryStream())
                    {
                        int read = 0;
                        do
                        {
                            read = _pipe.Read(buf, 0, buf.Length);
                            if (read == 0) throw new IOException("server closed");
                            ms.Write(buf, 0, read);
                        } while (!_pipe.IsMessageComplete);

                        string json = Encoding.UTF8.GetString(ms.ToArray());
                        var msg = JsonConvert.DeserializeObject<BusMessage>(json);
                        if (msg == null) continue;

                        if (msg.Type == "Pong") { _lastPong = DateTime.Now; continue; }

                        //if (msg.Type == "ACK" && !string.IsNullOrEmpty(msg.CorrelationId))
                        //{
                        //    _pendingAcks.TryRemove(msg.CorrelationId, out _);
                        //    Console.WriteLine($"[{_clientId}] ACK for {msg.CorrelationId}");
                        //    continue;
                        //}

                        if (msg.Type == "Data")
                        {
                            Console.WriteLine($"[{_clientId}] Data from {msg.From}: {msg.Payload}");
                            // business: auto reply
                            //SendTo(msg.From, $"Reply to {msg.From}: got '{msg.Payload}'");
                            //// ACK back to sender
                            //SendRaw(new BusMessage { Type = "ACK", From = _clientId, To = msg.From, CorrelationId = msg.CorrelationId });
                            continue;
                        }

                        if (msg.Type == "Reply")
                        {
                            Console.WriteLine($"[{_clientId}] Reply from {msg.From}: {msg.Payload}");
                            continue;
                        }
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
                if (_isConnected)
                {
                    SendRaw(new BusMessage { Type = "Ping", From = _clientId });
                }
                Thread.Sleep(5000);
                if (_lastPong != DateTime.MinValue && (DateTime.Now - _lastPong).TotalSeconds > 30)
                {
                    Console.WriteLine($"[{_clientId}] Heartbeat lost (>30s), reconnecting...");
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
                    var sent = SendRaw(msg);
                    if (!sent)
                    {
                        // re-enqueue and wait before retrying to avoid tight loop
                        Enqueue(msg);
                        Thread.Sleep(500);
                    }
                    else
                    {
                        if (msg.Type == "Data" || msg.Type == "Reply")
                        {
                            if (string.IsNullOrEmpty(msg.CorrelationId)) msg.CorrelationId = Guid.NewGuid().ToString();
                            //_pendingAcks[msg.CorrelationId] = new PendingAck(msg, DateTime.Now);
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{_clientId}] WriteLoop error: {ex.Message}");
                }
            }
        }

        //private void MonitorAcks()
        //{
        //    while (!_cts.IsCancellationRequested)
        //    {
        //        foreach (var kvp in _pendingAcks.ToArray())
        //        {
        //            if ((DateTime.Now - kvp.Value.Timestamp).TotalSeconds > 10)
        //            {
        //                Console.WriteLine($"[{_clientId}] Resend due to ACK timeout: {kvp.Key}");
        //                Enqueue(kvp.Value.Message); // re-enqueue for resend
        //                _pendingAcks[kvp.Key] = new PendingAck(kvp.Value.Message, DateTime.Now);
        //            }
        //        }
        //        Thread.Sleep(2000);
        //    }
        //}

        private bool SendRaw(BusMessage msg)
        {
            if (_pipe == null || !_pipe.IsConnected)
            {
                Console.WriteLine($"[{_clientId}] Pipe not connected, re-enqueue message.");
                return false;
            }

            try
            {
                var json = JsonConvert.SerializeObject(msg);
                var data = Encoding.UTF8.GetBytes(json);
                _pipe.Write(data, 0, data.Length);
                _pipe.Flush();
                Console.WriteLine($"[{_clientId}] → {(msg.To ?? "*")} Type={msg.Type} Corr={msg.CorrelationId ?? "-"}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_clientId}] SendRaw failed: {ex.Message}");
                TryReconnect();
                return false;
            }
        }

        private void TryReconnect()
        {
            try
            {
                _isConnected = false;
                try { _pipe?.Dispose(); } catch { }
                _pipe = null;
                Console.WriteLine($"[{_clientId}] Dispose");
            }
            catch
            {
                Console.WriteLine($"[{_clientId}] Dispose Error");
            }
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