using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace OFDRCentralControlServer.Core
{
    public class ClientSession
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public EndPoint? RemoteEndPoint => _client.Client?.RemoteEndPoint;
        public bool Connected => _client.Connected;
        public DateTime LastActive { get; set; } = DateTime.UtcNow;

        public ClientSession(TcpClient client)
        {
            _client = client;
            _client.NoDelay = true;
            _client.ReceiveTimeout = 10000;
            _client.SendTimeout = 10000;
            _stream = client.GetStream();
            _reader = new StreamReader(_stream, Encoding.ASCII);
            _writer = new StreamWriter(_stream, Encoding.ASCII) { AutoFlush = true };
        }

        public async Task<string?> ReadLineAsync(CancellationToken ct)
        {
            LastActive = DateTime.UtcNow;
            return await _reader.ReadLineAsync(ct);
        }

        public async Task SendAsync(object obj, CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(obj);
            await _writeLock.WaitAsync(ct);

            try
            {
                await _writer.WriteLineAsync(json);
                LastActive = DateTime.UtcNow;
            }
            catch
            {
                /* ignore */
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public void Close()
        {
            try
            {
                _writer.Dispose();
            }
            catch { }

            try
            {
                _reader.Dispose();
            }
            catch { }

            try
            {
                _stream.Dispose();
            }
            catch { }

            try
            {
                _client.Close();
            }
            catch { }
        }
    }
}