using Serilog;
using System.IO.Ports;

namespace FlaQueueServer.Device
{
    /// <summary>
    /// 光开关控制器（RS232 协议）
    /// </summary>
    public class OpticalSwitchController : IDisposable
    {
        private readonly string _portName;
        private readonly int _baud;
        private readonly int _switchIndex;
        private readonly int _inputChannel;
        private readonly SerialPort _port;

        private readonly ILogger Log = Serilog.Log.ForContext<OpticalSwitchController>();

        public OpticalSwitchController(string portName, int baud, int switchIndex, int inputChannel)
        {
            _portName = portName;
            _baud = baud;
            _switchIndex = switchIndex;
            _inputChannel = inputChannel;
            _port = new SerialPort(_portName, _baud, Parity.None, 8, StopBits.One)
            {
                Handshake = Handshake.None,
                ReadTimeout = 1000,
                WriteTimeout = 1000,
                NewLine = "\r\n"
            };

            try
            {
                _port.Open();
                Log.Information("Switch opened {Port}@{Baud}", _portName, _baud);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Switch open failed {Port}@{Baud}", _portName, _baud);
            }
        }

        public async Task SwitchToOutputAsync(int outputChannel, CancellationToken ct)
        {
            var cmd = $"SW {_switchIndex} SPOS {_inputChannel} {outputChannel}\r\n";
            Log.Debug("SWITCH send: {Cmd}", cmd.Replace("\r\n", "\\r\\n"));
            await WriteAsync(cmd, ct);
            var line = await ReadLineAsync(ct); // 期待 OK
            Log.Debug("SWITCH recv: {Line}", line);
            if (line is null || !line.Contains("OK", StringComparison.OrdinalIgnoreCase))
            {
                var errDetail = line ?? "(no response)";
                throw new Exception($"Switch command failed: {errDetail}");
            }
            await ReadUntilPromptAsync(ct);
            Log.Debug("SWITCH prompt '>' received");
        }

        private Task WriteAsync(string cmd, CancellationToken ct) =>
            Task.Run(() =>
            {
                _port.Write(cmd);
                Log.Debug($"Write {cmd}");
            }, ct);

        private Task<string?> ReadLineAsync(CancellationToken ct) =>
            Task.Run(() =>
            {
                try { return _port.ReadLine(); }
                catch { return null; }
            }, ct);

        private Task ReadUntilPromptAsync(CancellationToken ct) =>
            Task.Run(() =>
            {
                try
                {
                    while (true)
                    {
                        var ch = (char)_port.ReadChar();
                        if (ch == '>') break;
                    }
                }
                catch { }
            }, ct);

        public void Dispose()
        {
            try
            {
                _port?.Close();
            }
            catch { }

            try
            {
                _port?.Dispose();
            }
            catch { }
        }
    }
}