using OFDRCentralControlServer.Models;
using Serilog;
using System.Diagnostics;
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

        private readonly int _sw1Addr = 1;   // SW1
        private readonly int _sw9Addr = 9;   // SW9
        private readonly int _inputPort = 1; // BB 固定为 1
        private readonly int _sw1ToSw9State = 1; // 1 或 2：现场确认 SW1 的哪个状态通向 SW9

        // SW9 端口 → 标签映射，来自光路图
        private readonly Dictionary<int, string> _sw9Map = new Dictionary<int, string>
        {
            {1, "L3-4"},
            {2, "L3-3"},
            {3, "L3-2"},
            {4, "L3-1"},
            {5, "L2-4"},
            {6, "L2-3"},
            {7, "L2-2"},
            {8, "L2-1"},
        };

        private readonly Dictionary<int, string> _commandMap = new Dictionary<int, string>
        {
            {1, "MSW 1,1,2;9,1,1;"},
            {2, "MSW 1,1,2;9,1,2;"},
            {3, "MSW 1,1,2;9,1,3;"},
            {4, "MSW 1,1,2;9,1,4;"},
            {5, "MSW 1,1,2;9,1,5;"},
            {6, "MSW 1,1,2;9,1,6;"},
            {7, "MSW 1,1,2;9,1,7;"},
            {8, "MSW 1,1,2;9,1,8;"},
            {9, "MSW 1,1,2;10,1,1;"},
            {10, "MSW 1,1,2;10,1,2;"},
            {11, "MSW 1,1,2;10,1,3;"},
            {12, "MSW 1,1,2;10,1,4;"},
            {13, "MSW 1,1,2;10,1,5;"},
            {14, "MSW 1,1,2;10,1,6;"},
            {15, "MSW 1,1,2;10,1,7;"},
            {16, "MSW 1,1,2;10,1,8;"},
        };

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

        public async Task<bool> ConnectAsync()
        {
            try
            {
                if (!IsConnected)
                {
                    _port!.Open();
                    Log.Information("Switch opened {Port}@{Baud}", _portName, _baud);
                }

                IsConnected = true;//await SetLocalAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Switch open failed {Port}@{Baud}", _portName, _baud);
                IsConnected = false;
            }

            return IsConnected;
        }

        public Task DisconnectAsync()
        {
            Dispose();
            IsConnected = false;
            return Task.CompletedTask;
        }

        /// <summary>
        /// 进行路由。
        /// </summary>
        public async Task RouteL1_1To(int clientId, CancellationToken ct)
        {
            //MSW 1,1,1关闭 MSW 1,1,2打开
            //MSW 2,1,1关闭 MSW 2,1,2打开
            //9,1,1 --- 9,1,8
            //MSW 10,1,1 --- 10,1,8
            // 1) 先把 SW1 切到 SW9 的支路
            string cmd = $"MSW {_sw1Addr},{_inputPort},{_sw1ToSw9State}";
            Log.Debug($"SWITCH send: {cmd}");
            await WriteAsync(cmd, ct);

            var lines = await ReadUntilPromptAsync(ct, TimeSpan.FromSeconds(1));
            Log.Debug("SWITCH recv: {Line}", lines);

            EnsureOkOrThrow(lines);

            cmd = $"MSW {_sw9Addr},{_inputPort},{clientId}";
            Log.Debug($"SWITCH send: {cmd}");
            await WriteAsync(cmd, ct);

            lines = await ReadUntilPromptAsync(ct, TimeSpan.FromSeconds(1));
            Log.Debug("SWITCH recv: {Line}", lines);

            EnsureOkOrThrow(lines);
        }

        public async Task SwitchToOutputMSWAsync(int outputChannel, CancellationToken ct)
        {
            var cmd = _commandMap[outputChannel];
            Log.Debug($"SWITCH send: {cmd}");
            await WriteAsync(cmd, ct);

            var lines = await ReadUntilPromptAsync(ct, TimeSpan.FromSeconds(1));
            Log.Debug("SWITCH recv: {Line}", lines);

            EnsureOkOrThrow(lines);
        }

        /// <summary>
        /// 这里输出通道要加一 因为设备通道从 1 开始编号
        /// </summary>
        /// <param name="outputChannel"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task SwitchToOutputAsync(int outputChannel, CancellationToken ct)
        {
            var cmd = $"SW {_switchIndex} SPOS {_inputChannel} {outputChannel + 1}";
            Log.Debug($"SWITCH send: {cmd}");
            await WriteAsync(cmd, ct);

            var lines = await ReadUntilPromptAsync(ct, TimeSpan.FromSeconds(1));
            Log.Debug("SWITCH recv: {Line}", lines);

            EnsureOkOrThrow(lines);
        }

        public async Task<int> GetActualOutputAsync(int switchIndex, CancellationToken ct = default)
        {
            var cmd = $"SW {switchIndex} POS";
            Log.Debug("SWITCH send: {Cmd}", cmd);
            await WriteAsync(cmd, ct);

            // 设备返回格式：例如 "SW 3 POS: 1->8"（先读到 OK，再读详细？视设备回显模式）
            // 若需要解析具体 1->8，可继续 ReadLine，或按你设备回包解析规则调整。
            var lines = await ReadUntilPromptAsync(ct, TimeSpan.FromSeconds(1));
            Log.Debug("SWITCH recv: {Line}", lines);

            EnsureOkOrThrow(lines);

            // 如果设备实际返回的是数据行而非 OK，这里可以根据你设备回包进一步解析
            return int.TryParse(lines, out var result) ? result : -1;
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

        public async Task<bool> SetLocalAsync(CancellationToken ct = default)
        {
            // 切本地/远程。[1]
            var cmd = $"Local OFF";
            await WriteAsync(cmd, ct);
            var line = await ReadLineAsync(ct);
            if (!string.IsNullOrEmpty(line))
                return true;
            return false;
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

        /// <summary>
        /// 从串口读取直到遇到提示符 '>'；在 timeout 或取消时返回 false。
        /// </summary>

        public async Task<string?> ReadUntilPromptAsync(
            CancellationToken ct,
            TimeSpan timeout
        )
        {
            return await Task.Run(() =>
                {
                    var buffer = new byte[256];
                    var sb = new StringBuilder(1024);
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    try
                    {
                        while (stopwatch.ElapsedMilliseconds <= timeout.TotalMilliseconds)
                        {
                            var ch = (char)_port!.ReadChar();
                            sb.Append(ch);
                            if (ch == '>') break;
                        }

                        return sb.ToString();
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"ReadUntilPromptAsync error: {ex}");
                        return null;
                    }
                }, ct);
        }

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