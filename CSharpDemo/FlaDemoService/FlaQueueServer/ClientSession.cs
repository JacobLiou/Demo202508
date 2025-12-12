using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace FlaQueueServer
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

        public ClientSession(TcpClient client)
        {
            _client = client;
            _stream = client.GetStream();
            _reader = new StreamReader(_stream, Encoding.UTF8);
            _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = true };
        }

        public async Task<string?> ReadLineAsync(CancellationToken ct)
        {
            try { return await _reader.ReadLineAsync(ct); }
            catch { return null; }
        }

        public async Task SendAsync(object obj, CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(obj);
            await _writeLock.WaitAsync(ct);

            try
            {
                await _writer.WriteLineAsync(json);
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