using MIMS.Common;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;

namespace MIMS.Hub
{
    public class PipeHub
    {
        private readonly string _pipeName;
        private readonly List<ClientConnection> _clients = new List<ClientConnection>();
        private readonly Dictionary<string, ClientConnection> _clientMap = new Dictionary<string, ClientConnection>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public PipeHub(string pipeName)
        { _pipeName = pipeName; }

        public void Start()
        {
            var acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "PipeAccept" };
            acceptThread.Start();
            Console.WriteLine($"[Hub] Started on pipe: {_pipeName}");

            var monitorThread = new Thread(MonitorClients) { IsBackground = true, Name = "PipeMonitor" };
            monitorThread.Start();
        }

        private void AcceptLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 20,
                        PipeTransmissionMode.Message, PipeOptions.Asynchronous);
                    server.WaitForConnection();
                    var conn = new ClientConnection(server, this);
                    lock (_clients) _clients.Add(conn);
                    conn.Start();
                    Console.WriteLine($"[Hub] Client connected. total={_clients.Count}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Hub] Accept error: {ex.Message}");
                    Thread.Sleep(500);
                }
            }
        }

        private void MonitorClients()
        {
            while (!_cts.IsCancellationRequested)
            {
                lock (_clients)
                {
                    foreach (var c in _clients.ToArray())
                    {
                        if ((DateTime.Now - c.LastHeartbeat).TotalSeconds > 20)
                        {
                            Console.WriteLine($"[Hub] Heartbeat lost for {c.ClientId ?? "<unknown>"}, disconnecting.");
                            c.Dispose();
                            RemoveClient(c);
                        }
                    }
                }
                Thread.Sleep(5000);
            }
        }

        internal void RegisterClient(string clientId, ClientConnection conn)
        {
            lock (_clientMap)
            {
                _clientMap[clientId] = conn;
                conn.ClientId = clientId;
                Console.WriteLine($"[Hub] Registered {clientId}");
            }
        }

        internal void Unregister(ClientConnection conn)
        {
            lock (_clientMap)
            {
                var keys = _clientMap.Where(kv => kv.Value == conn).Select(kv => kv.Key).ToList();
                foreach (var k in keys) _clientMap.Remove(k);
                Console.WriteLine($"[Hub] Unregistered {string.Join(",", keys)}");
            }
        }

        public void Broadcast(BusMessage msg)
        {
            lock (_clients)
            {
                foreach (var c in _clients.ToArray())
                    c.Send(msg);
            }
        }

        public void Forward(string targetId, BusMessage msg)
        {
            ClientConnection target = null;
            lock (_clientMap) _clientMap.TryGetValue(targetId, out target);
            if (target != null)
                target.Send(msg);
            else
                Console.WriteLine($"[Hub] Target {targetId} not found.");
        }

        internal void RemoveClient(ClientConnection c)
        {
            lock (_clients) _clients.Remove(c);
            Unregister(c);
            Console.WriteLine($"[Hub] Client removed. total={_clients.Count}");
        }

        public class ClientConnection : IDisposable
        {
            private readonly NamedPipeServerStream _server;
            private readonly PipeHub _hub;
            private readonly Thread _readThread;
            private readonly object _writeLock = new object();
            internal DateTime LastHeartbeat { get; private set; } = DateTime.Now;
            internal string ClientId { get; set; }

            public ClientConnection(NamedPipeServerStream server, PipeHub hub)
            {
                _server = server;
                _hub = hub;
                _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "PipeClientRead" };
            }

            public void Start()
            { _readThread.Start(); }

            private void ReadLoop()
            {
                var buf = new byte[64 * 1024];
                try
                {
                    while (true)
                    {
                        int read = _server.Read(buf, 0, buf.Length);
                        if (read == 0) break; // client closed
                        string json = Encoding.UTF8.GetString(buf, 0, read);
                        var msg = JsonConvert.DeserializeObject<BusMessage>(json);
                        if (msg == null) continue;

                        if (msg.Type == "Ping")
                        {
                            LastHeartbeat = DateTime.Now;
                            var pong = new BusMessage { Type = "Pong", To = msg.From };
                            Send(pong);
                            continue;
                        }
                        if (msg.Type == "Register" && !string.IsNullOrEmpty(msg.From))
                        {
                            _hub.RegisterClient(msg.From, this);
                            continue;
                        }
                        if (msg.Type == "ACK" && !string.IsNullOrEmpty(msg.To))
                        {
                            _hub.Forward(msg.To, msg);
                            continue;
                        }
                        if (msg.Type == "Data" || msg.Type == "Reply")
                        {
                            if (!string.IsNullOrEmpty(msg.To)) _hub.Forward(msg.To, msg);
                            else _hub.Broadcast(msg);
                            continue;
                        }
                        // Unknown types ignored
                    }
                }
                catch (IOException ioEx)
                {
                    Console.WriteLine($"[Hub] IO error on {ClientId}: {ioEx.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Hub] Read error on {ClientId}: {ex}");
                }
                finally
                {
                    Dispose();
                    _hub.RemoveClient(this);
                }
            }

            public void Send(BusMessage msg)
            {
                try
                {
                    var json = JsonConvert.SerializeObject(msg);
                    var data = Encoding.UTF8.GetBytes(json);
                    lock (_writeLock)
                    {
                        _server.Write(data, 0, data.Length);
                        _server.Flush();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Hub] Send error to {ClientId}: {ex.Message}");
                }
            }

            public void Dispose()
            {
                try { _server?.Dispose(); } catch { }
            }
        }
    }
}