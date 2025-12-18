using Serilog;
using System.Net.Sockets;
using System.Text;

namespace FlaQueueServer.Devices
{
    /// <summary>
    /// Fla设备驱动控制器 (TCP 协议)
    /// 归零：获取基准长度（通常是光纤的“原始长度”或参考点）。
    /// 目标：找到光纤链路的“原始长度”，作为后续扫描计算的参考。
    ////流程概述
    ////设置测量参数
    ////分辨率（SR_0 / SR_1 / SR_2）
    ////增益（G_1 / G_2）
    ////通道（如果有）

    ////执行一次完整测量（SCAN）
    ////发送 SCAN 指令
    ////接收数据（直到结束符 !）

    ////自动寻峰（或手动分析）
    ////使用 SCAN_AAA_BBB_...自动寻峰指令，或本地算法分析 SCAN 数据
    ////找到反射峰列表

    ////确定初始长度
    ////按产线规则取第 3 个峰（或最远峰）作为“归零长度”
    ////保存该长度用于后续扫描计算

    //目标：根据归零结果和当前测量，计算光纤长度。
    //流程概述
    //设置窗口和游标

    //根据归零长度，设置 X_ 和 WR_，确保窗口覆盖目标区域
    //例如：X_006.0，WR_01.00

    //执行测量（SCAN）

    //发送 SCAN 指令
    //获取数据
    //长度计算

    //如果使用自动寻峰：发送 SCAN_AAA_BBB_...指令，直接返回峰位置
    //如果使用原始数据：根据起点位置 + 数据索引 × 分辨率，计算目标点距离

    //输出最终长度

    //与归零长度结合，得到实际光纤长度或差值
    //扫描：在归零基础上，测量并计算目标长度（可能是跳线、段长、变化量）。
    /// </summary>
    public class FlaInstrumentAdapter
    {
        // ------------------ 固化的默认参数（最佳参数） ------------------
        private const string DEFAULT_START = "0.0";

        private const string DEFAULT_END = "30.0";
        private const string DEFAULT_ALGO = "2";
        private const string DEFAULT_WIDTH = "0.5";   // m
        private const string DEFAULT_THRESHOLD = "90";    // => -90dB
        private const string DEFAULT_ID = "12";
        private const string DEFAULT_SN = "SN1A234";

        // 扫描时对客户端 lengthHint 的安全裕度（单位：米）
        private const double SCAN_END_MARGIN_M = 5.0;

        private readonly string _host;

        private readonly int _port; // 4300

        private TcpClient? _client;

        private NetworkStream? _stream;

        private readonly ILogger Log = Serilog.Log.ForContext<FlaInstrumentAdapter>();

        public FlaInstrumentAdapter(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public async Task ConnectAsync(CancellationToken ct)
        {
            _client = new TcpClient();
            await _client.ConnectAsync(_host, _port, ct);
            Log.Information("Connected FLA");
            _stream = _client.GetStream();
            // 握手：设备会发 "OCI" 表示连接成功（协议文档）
            var ok = await ReadUntilAsync("OCI", TimeSpan.FromSeconds(5), ct);
            Log.Information($"OCI Recv OK {ok}");
            if (!ok) throw new Exception("Handshake failed: OCI not received");
        }

        public async Task DisconnectAsync()
        {
            try
            {
                _stream?.Dispose();
            }
            catch { }

            try
            {
                _client?.Close();
            }
            catch { }
        }

        /// <summary>
        /// 归零
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<double> ZeroLengthAsync(CancellationToken ct)
        {
            // 测量范围重置（归零）：X_00000 + WR_00000，均需返回 SET OK
            Log.Debug($" {nameof(ZeroLengthAsync)} X_00000 ");
            await SetCenterAsync("00000", ct);  // X_00000

            Log.Debug($" {nameof(ZeroLengthAsync)} WR_00000 ");
            await SetWindowAsync("00000", ct);  // WR_00000

            // 自动寻峰（多个峰）：使用默认参数（Start/End/Algo/Width/Threshold/Id/Sn）
            Log.Debug($" {nameof(AutoPeakMultiAsync)} ");
            var peaks = await AutoPeakMultiAsync(
                start: DEFAULT_START,
                end: DEFAULT_END,
                count: "2",
                algo: DEFAULT_ALGO,
                width: DEFAULT_WIDTH,
                thr: DEFAULT_THRESHOLD,
                id: DEFAULT_ID,
                sn: DEFAULT_SN,
                ct: ct
            );

            Log.Debug($" Complete {nameof(AutoPeakMultiAsync)} ");
            // 产线约定取第 3 峰作为归零线长
            if (peaks.Count >= 3)
            {
                var third = peaks[2];
                return third.Position_m;
            }

            // 兜底：不足 3 个峰，取距离最大
            if (peaks.Count > 0)
            {
                var maxDist = peaks.OrderByDescending(x => x.Position_m).First();
                return maxDist.Position_m;
            }

            return -1d;
        }

        /// <summary>
        /// 扫描
        /// </summary>
        /// <param name="lengthHint_m"></param>
        /// <param name="zeroLength_m"></param>
        /// <param name="ct"></param>
        /// <param name="minRl_dB"></param>
        /// <param name="lengthRange_m"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<double> ScanLengthAsync(
            double zeroLength_m,            // 上一步归零得到的基准线长
            CancellationToken ct
        )
        {
            Log.Debug($" {nameof(ScanLengthAsync)} AutoPeakMultiAsync ");
            var peaks = await AutoPeakMultiAsync(
                start: DEFAULT_START,
                end: DEFAULT_END,
                count: "2",
                algo: DEFAULT_ALGO,
                width: DEFAULT_WIDTH,
                thr: DEFAULT_THRESHOLD,
                id: DEFAULT_ID,
                sn: DEFAULT_SN,
                ct: ct
            );

            if (peaks.Count == 0)
                return -1d;

            Log.Debug($" {nameof(ScanLengthAsync)} AutoPeakMultiAsync End");
            // 代表产品端点的峰：采用距离最大（如需按 dB 选择，可改成幅值最大）
            var endpoint = peaks.OrderByDescending(x => x.Position_m).First();
            var productLen = endpoint.Position_m - zeroLength_m;
            return productLen;
        }

        // 峰值结果
        private record PeakPoint(double Position_m, double Db, double Id, string Sn);

        // ------------------ 自动寻峰（多峰） ------------------
        private async Task<List<PeakPoint>> AutoPeakMultiAsync(
            string start, string end, string count, string algo, string width, string thr, string id, string sn,
            CancellationToken ct)
        {
            var sum = CalcSum(new[] { start, end, count, algo, width, thr, id, sn }); // 绝对值求和（含 ID/SN 数字部分），容差 0.1
            var cmd = $"SCAN_{start}_{end}_{count}_{algo}_{width}_{thr}_{id}_{sn}_{sum:F3}_NACS";
            Log.Debug(cmd);
            await SendLineAsync(cmd, ct, TimeSpan.FromSeconds(10));

            var points = new List<PeakPoint>();
            for (int i = 0; i < 256; i++) // 读取所有 OP_ 行
            {
                var line = await ReadLineAsync(TimeSpan.FromSeconds(5), ct);
                Log.Debug($"Recv: {line}");

                if (line is null) break;

                if (line.Contains("INPUT_ERROR", StringComparison.OrdinalIgnoreCase))
                    throw new Exception("INPUT_ERROR from device");

                if (!line.StartsWith("OP_", StringComparison.OrdinalIgnoreCase))
                    break;

                var segs = line.Split('_', StringSplitOptions.RemoveEmptyEntries);
                if (segs.Length < 6) throw new Exception($"Unexpected response: {line}");

                double pos = double.Parse(segs[1]);     // m
                double db = double.Parse(segs[2]);     // dB（负值）
                double idResp = double.Parse(segs[3]);
                string snResp = segs[4];
                double sumDev = double.Parse(segs[5]);     // 校验和

                var calc = CalcSum(new[] { pos.ToString(), db.ToString(), idResp.ToString(), snResp });
                if (Math.Abs(calc - sumDev) > 0.1)
                    throw new Exception($"Checksum mismatch: calc={calc:F3}, dev={sumDev:F3}");

                points.Add(new PeakPoint(pos, db, idResp, snResp));
            }

            return points;
        }

        public async Task SetResolutionAsync(string srMode, CancellationToken ct)
            => await SendAndExpectOkAsync($"SR_{srMode}", ct);

        public async Task SetGainAsync(string gain, CancellationToken ct)
        {
            // 协议中：G_1=×1, G_2=×2, G_3=×5, G_4=×10
            var gmap = gain switch { "1" => "1", "2" => "2", "5" => "3", "10" => "4", _ => "1" };
            await SendAndExpectOkAsync($"G_{gmap}", ct);
        }

        private async Task SetWindowAsync(string wr, CancellationToken ct)
            => await SendAndExpectOkAsync($"WR_{Fmt5(wr)}", ct);

        private async Task SetCenterAsync(string x, CancellationToken ct)
            => await SendAndExpectOkAsync($"X_{Fmt5(x)}", ct);

        private async Task SendAndExpectOkAsync(string cmd, CancellationToken ct)
        {
            await SendLineAsync(cmd, ct, TimeSpan.FromSeconds(10));

            try
            {
                var ok = await ReadUntilAsync("OK", TimeSpan.FromSeconds(5), ct);
                if (!ok)
                {
                    throw new Exception($"Did not receive OK for '{cmd}'");
                }
            }
            catch (TimeoutException tex)
            {
                throw new TimeoutException($"Timeout waiting for OK for '{cmd}' (5s).", tex);
            }
        }

        private async Task SendLineAsync(string line, CancellationToken ct, TimeSpan timeout)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            var buf = Encoding.ASCII.GetBytes(line + "\n");
            try
            {
                await _stream!.WriteAsync(buf, 0, buf.Length, ct);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                throw new TimeoutException("SendLineAsync timed out while waiting for device response.");
            }
        }

        private async Task<string> ReadLineAsync(TimeSpan timeout, CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            var sb = new StringBuilder();
            var buffer = new byte[1];

            try
            {
                while (true)
                {
                    var n = await _stream!.ReadAsync(buffer.AsMemory(0, 1), cts.Token);
                    if (n == 0) break; // 连接关闭
                    var ch = (char)buffer[0];
                    Log.Debug(ch.ToString());
                    if (ch == '\n') break;
                    sb.Append(ch);
                }
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                throw new TimeoutException("ReadLineAsync timed out while waiting for device response.");
            }

            return sb.ToString().Trim();
        }

        private async Task<bool> ReadUntilAsync(string token, TimeSpan timeout, CancellationToken ct)
        {
            var end = DateTime.UtcNow + timeout;
            var buf = new byte[1024];
            var mem = new MemoryStream();
            while (DateTime.UtcNow < end)
            {
                if (_stream!.DataAvailable)
                {
                    var n = await _stream.ReadAsync(buf, ct);
                    if (n > 0) mem.Write(buf, 0, n);
                    var s = Encoding.ASCII.GetString(mem.ToArray());
                    if (s.Contains(token)) return true;
                }

                await Task.Delay(50, ct);
            }

            return false;
        }

        private static string Fmt5(string raw)
        {
            var s = raw.Trim();
            if (s.Length > 5)
                s = s[..5];

            return s.PadLeft(5, '0');
        }

        private static double CalcSum(IEnumerable<string> parts)
        {
            double total = 0.0;
            foreach (var s in parts)
            {
                foreach (var x in ExtractNumbers(s))
                    total += Math.Abs(x);
            }

            return total;
        }

        private static IEnumerable<double> ExtractNumbers(string s)
        {
            var list = new List<double>();
            var token = new StringBuilder();
            foreach (var ch in s)
            {
                if (char.IsDigit(ch) || ch == '.' || ch == '-') token.Append(ch);
                else
                {
                    if (token.Length > 0 && double.TryParse(token.ToString(), out var vd))
                        list.Add(vd);
                    token.Clear();
                }
            }

            if (token.Length > 0 && double.TryParse(token.ToString(), out var v))
                list.Add(v);

            return list;
        }
    }
}