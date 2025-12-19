using OFDRCentralControlServer.Interfaces;
using OFDRCentralControlServer.Models;
using Serilog;

namespace OFDRCentralControlServer.Devices
{
    /// <summary>
    /// 光开关控制器（RS232 协议）
    /// </summary>
    public class OpticalSwitchControllerMock : IOpticalSwitchController, IDisposable
    {
        public bool IsConnected { get; private set; }

        public int InputChannel => throw new NotImplementedException();

        public IReadOnlyCollection<int> SupportedOutputChannels => throw new NotImplementedException();

        public int CurrentOutputChannel => throw new NotImplementedException();

        private readonly ILogger Log = Serilog.Log.ForContext<OpticalSwitchController>();

        public OpticalSwitchControllerMock()
        {
        }

        public Task ConnectAsync()
        {
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync()
        {
            Dispose();
            IsConnected = false;
            return Task.CompletedTask;
        }

        public async Task SwitchToOutputAsync(int outputChannel, CancellationToken ct)
        {
            await Task.Delay(100, ct); // 模拟延时
            Log.Debug("SWITCH prompt '>' received");
        }

        public async Task<int> GetActualOutputAsync(int switchIndex, CancellationToken ct = default)
        {
            await Task.Delay(100, ct); // 模拟延时
            return 0;
        }

        public async Task<long> GetCountAsync(int switchIndex, CancellationToken ct = default)
        {
            await Task.Delay(100, ct); // 模拟延时
            return 0;
        }

        public async Task MultiSwitchAsync(IEnumerable<SwitchRoute> routes, CancellationToken ct = default)
        {
            await Task.Delay(100, ct); // 模拟延时
        }

        public async Task SetEchoAsync(bool on, CancellationToken ct = default)
        {
            await Task.Delay(100, ct); // 模拟延时
        }

        public async Task<bool> GetEchoAsync(CancellationToken ct = default)
        {
            await Task.Delay(100, ct); // 模拟延时
            return true;
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
                Log.Debug($"Write {cmd}");
            }, ct);

        private Task<string?> ReadLineAsync(CancellationToken ct) =>
            Task.Run(() =>
            {
                try { return "Hello"; }
                catch { return null; }
            }, ct);

        public void Dispose()
        {
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

        public Task SetChannelAsync(int outputChannel, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<int> QueryChannelAsync(CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
}