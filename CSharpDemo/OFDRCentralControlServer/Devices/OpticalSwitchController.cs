using OFDRCentralControlServer.Models;
using Serilog;
using System.IO.Ports;
using System.Text;

namespace OFDRCentralControlServer.Devices
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

        private SerialPort? _port;

        public bool IsConnected { get; private set; }

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
                // === 关键改造 1：ASCII 编码 + 终止符 CRLF ===
                Encoding = Encoding.ASCII,   // 设备要求 8-bit ASCII
                NewLine = "\r\n",            // 设备终止符 <CR><LF>

            };
        }

        public Task<bool> ConnectAsync()
        {
            try
            {
                if (!IsConnected)
                    _port!.Open();
                Log.Information("Switch opened {Port}@{Baud}", _portName, _baud);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Switch open failed {Port}@{Baud}", _portName, _baud);
                return Task.FromResult(false);
            }

            IsConnected = true;
            return Task.FromResult(true);
        }

        public Task DisconnectAsync()
        {
            Dispose();
            IsConnected = false;
            return Task.CompletedTask;
        }

        public async Task SwitchToOutputAsync(int outputChannel, CancellationToken ct)
        {
            var cmd = $"SW {_switchIndex} SPOS {_inputChannel} {outputChannel}";
            Log.Debug($"SWITCH send: {cmd}");
            await WriteAsync(cmd, ct);
            var line = await ReadLineAsync(ct); // 期待 OK
            Log.Debug("SWITCH recv: {Line}", line);
            EnsureOkOrThrow(line);
            await ReadUntilPromptAsync(ct);
            Log.Debug("SWITCH prompt '>' received");
        }

        public async Task<int> GetActualOutputAsync(int switchIndex, CancellationToken ct = default)
        {
            var cmd = $"SW {switchIndex} POS";
            Log.Debug("SWITCH send: {Cmd}", cmd);
            await WriteAsync(cmd, ct);

            var line = await ReadLineAsync(ct);
            Log.Debug("SWITCH recv: {Line}", line);
            EnsureOkOrThrow(line);

            // 设备返回格式：例如 "SW 3 POS: 1->8"（先读到 OK，再读详细？视设备回显模式）
            // 若需要解析具体 1->8，可继续 ReadLine，或按你设备回包解析规则调整。
            await ReadUntilPromptAsync(ct);
            Log.Debug("SWITCH prompt '>' received");

            // 如果设备实际返回的是数据行而非 OK，这里可以根据你设备回包进一步解析
            return int.TryParse(line, out var result) ? result : -1;

        }

        public async Task<long> GetCountAsync(int switchIndex, CancellationToken ct = default)
        {
            var cmd = $"SW {switchIndex} CNT";
            Log.Debug("SWITCH send: {Cmd}");
            await WriteAsync(cmd, ct);
            var line = await ReadLineAsync(ct); // 期待 OK
            Log.Debug("SWITCH recv: {Line}", line);
            EnsureOkOrThrow(line);
            await ReadUntilPromptAsync(ct);
            Log.Debug("SWITCH prompt '>' received");
            return long.TryParse(line, out var result) ? result : -1;
        }

        public async Task MultiSwitchAsync(IEnumerable<SwitchRoute> routes, CancellationToken ct = default)
        {
            // 按文档：格式 "MSW AA,BB,CC;AA,BB,CC;"；AA=0 表示全部；忽略未安装或失败的开关
            var parts = string.Join("; ", routes.Select(r => $"{r.SwitchIndex}, {r.Input}, {r.Output}")) + ";";
            var cmd = $"MSW {parts}";
            await WriteAsync(cmd, ct);
            var line = await ReadLineAsync(ct);
            EnsureOkOrThrow(line);
        }

        public async Task SetEchoAsync(bool on, CancellationToken ct = default)
        {
            // 开/关回显
            var cmd = $"ECHO {(on ? "ON" : "OFF")}";
            await WriteAsync(cmd, ct);
            var line = await ReadLineAsync(ct);
            EnsureOkOrThrow(line);
        }

        public async Task<bool> GetEchoAsync(CancellationToken ct = default)
        {
            var cmd = $"ECHO";
            await WriteAsync(cmd, ct);
            var line = await ReadLineAsync(ct);
            if (line is null) ThrowUnexpected(line);
            return line!.Contains("ON", StringComparison.OrdinalIgnoreCase);
        }

        public async Task SetLocalAsync(bool on, CancellationToken ct = default)
        {
            // 切本地/远程。[1]
            var cmd = $"Local {(on ? "ON" : "OFF")}";
            await WriteAsync(cmd, ct);
            var line = await ReadLineAsync(ct);
            EnsureOkOrThrow(line);
        }

        public async Task ResetAsync(CancellationToken ct = default)
        {
            // 软复位。
            var cmd = $"RST";
            await WriteAsync(cmd, ct);
            var line = await ReadLineAsync(ct);
            EnsureOkOrThrow(line);
        }

        private Task WriteAsync(string cmd, CancellationToken ct) =>
            Task.Run(() =>
            {
                // 依据 NewLine = "\r\n" 自动附加终止符
                _port!.WriteLine(cmd);
                Log.Debug("WriteLine {Cmd}", cmd);
            }, ct);

        private Task<string?> ReadLineAsync(CancellationToken ct) =>
            Task.Run(() =>
            {
                try { return _port!.ReadLine(); }
                catch { return null; }
            }, ct);

        private Task ReadUntilPromptAsync(CancellationToken ct) =>
            Task.Run(() =>
            {
                try
                {
                    while (true)
                    {
                        var ch = (char)_port!.ReadChar();
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

        private static void EnsureOkOrThrow(string? line)
        {
            if (line is null) ThrowUnexpected(line);
            if (line!.Trim().StartsWith("OK", StringComparison.OrdinalIgnoreCase)) return;
            if (IsError(line)) ThrowError(line);
        }

        private static bool IsError(string? line) =>
            (line ?? "").StartsWith("Err:", StringComparison.OrdinalIgnoreCase);

        private static void ThrowError(string? line) =>
            throw new InvalidOperationException($"设备错误响应：{line}");

        private static void ThrowUnexpected(string? line) =>
            throw new InvalidOperationException($"响应格式异常：{line ?? "(null)"}");
    }
}