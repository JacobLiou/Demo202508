using Serilog;
using System.Buffers;
using System.Globalization;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace OFDRCentralControlServer.Devices
{
    /// <summary>
    /// Fla设备驱动控制器 (TCP 协议)
    /// 归零：获取基准长度（通常是光纤的“原始长度”或参考点）。  一次自动寻峰，取第3峰位置作为归零长度。
    /// 目标：找到光纤链路的“原始长度”，作为后续扫描计算的参考。  再一次自动寻峰，取最大峰位置作为端点位置。
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

        public async Task<bool> ConnectAsync(CancellationToken ct)
        {
            _client = new TcpClient();
            await _client.ConnectAsync(_host, _port, ct);
            Log.Information("Connected FLA");
            _stream = _client.GetStream();
            // 握手：设备会发 "OCI" 表示连接成功（协议文档）
            var ok = await ReadUntilAsync("OCI", TimeSpan.FromSeconds(5), ct);
            Log.Information($"OCI Recv OK {ok}");
            if (!ok)
                throw new Exception("Handshake failed: OCI not received");

            return ok;
        }

        public async Task DisconnectAsync()
        {
            try
            {
                await SendLineAsync("QUIT");
                await Task.Delay(50);
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
            //// 测量范围重置（归零）：X_00000 + WR_00000，均需返回 SET OK
            //Log.Debug($" {nameof(ZeroLengthAsync)} SR_0 ");
            //await SetResolutionAsync("00000", ct);  // SR_0

            //Log.Debug($" {nameof(ZeroLengthAsync)} G_1 ");
            //await SetGainAsync("1", ct);  // G_1

            //Log.Debug($" {nameof(ZeroLengthAsync)} X_00000 ");
            //await SetCenterAsync("00000", ct);  // X_00000

            //Log.Debug($" {nameof(ZeroLengthAsync)} WR_00000 ");
            //await SetWindowAsync("00000", ct);  // WR_00000

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

            Log.Debug($" Complete {nameof(AutoPeakMultiAsync)} ");
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
            Log.Debug("ScanLengthAsync Begin");
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

        // 发送 SCAN 并读取数据
        public async Task<ScanResult> ScanAsync(
            double windowLength_m,   // m（来自 WR 设置）
            double nExpected,        // 期望分辨率 n（来自 SR 模式）
            CancellationToken ct = default)
        {
            if (_stream is null)
                throw new InvalidOperationException("Not connected.");

            // 1) 发送 SCAN 指令（ASCII + CRLF）
            await SendLineAsync("SCAN", ct, TimeSpan.FromSeconds(10));
            Log.Debug("SCAN sent");

            // 2) 读取首行分辨率（文本行，CRLF 结束）
            string firstLine = await ReadFrameAsync(TimeSpan.FromSeconds(20), ct);
            Log.Debug("SCAN resolution line: `{Line}`", firstLine);

            double n = TryParseResolution(firstLine, fallback: nExpected);
            if (Math.Abs(n - nExpected) > 1e-9)
                Log.Warning("Resolution from device ({DeviceN} m) != expected ({ExpectedN} m). Using device value.",
                    n, nExpected);

            // 3) 计算理论数据字节
            int NExpected = Math.Max(1, (int)Math.Ceiling(windowLength_m / n)); // 文档：向上取整
            int expectedBytes = NExpected * 12;                                  // 每点 12 字节
            Log.Debug("Expect points={N}, dataBytes={B}", NExpected, expectedBytes);

            // 4) 读取数据区（尽量按理论字节）
            var dataBuffer = new List<byte>(expectedBytes + 64);
            await ReadAtLeastAsync(_stream, dataBuffer, expectedBytes, ct);

            // 4.2) 继续读到 '!'（0x21）
            bool terminatedByBang = false;
            var buffer = new byte[1];

            while (true)
            {
                var b = await _stream!.ReadAsync(buffer.AsMemory(0, 1), ct);
                Log.Debug("Read extra byte: {B}", buffer[0]);
                if (b < 0) break;          // 连接关闭（非预期）
                if (b == (byte)'!')
                {
                    terminatedByBang = true;
                    break;
                }
                // 某些实现可能有 CRLF、提示字符等尾随，直接跳过
            }
            int rawBytes = dataBuffer.Count;

            // 自适应修正：若理论值与实际值不符，用实际字节数/12 作为点数
            int actualPoints = rawBytes / 12;
            if (actualPoints != NExpected)
            {
                Log.Warning("Data bytes mismatch: theory={ExpectedBytes}({NExpected}pts) vs actual={RawBytes}({ActualPts}pts). Will parse actual.",
                    expectedBytes, NExpected, rawBytes, actualPoints);
            }

            // 5) 解析 12B -> double（ASCII 固定宽度）
            var y = new double[actualPoints];
            for (int i = 0; i < actualPoints; i++)
            {
                int offset = i * 12;
                y[i] = ParseChunkAsAsciiDouble(dataBuffer, offset, 12);
            }

            return new ScanResult(
                Resolution: n,
                WindowLength: windowLength_m,
                PointCount: actualPoints,
                Y: y,
                RawBytes: rawBytes,
                TerminatedByBang: terminatedByBang
            );
        }

        public record ScanResult(
            double Resolution,          // 解析到的空间分辨率 n（首行或兜底）
            double WindowLength,        // m（调用方传入，来自 WR）
            int PointCount,             // 实际解析的点数
            double[] Y,                 // 纵坐标数据
            int RawBytes,               // 实际读取的有效数据区字节数（不含 '!'）
            bool TerminatedByBang       // 是否读到 '!' 结束符
        );

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
                var lines = await ReadAllLinesAsync(TimeSpan.FromSeconds(20), ct);
                Log.Debug($"Recv: {lines}");

                if (lines is null) break;

                if (lines.Contains("INPUT_ERROR", StringComparison.OrdinalIgnoreCase))
                    throw new Exception("INPUT_ERROR from device");

                if (!lines.StartsWith("OP_", StringComparison.OrdinalIgnoreCase))
                    break;

                var segs = lines.Split('_', StringSplitOptions.RemoveEmptyEntries);
                if (segs.Length < 6) throw new Exception($"Unexpected response: {lines}");

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

        private async Task SendLineAsync(string line)
        {
            var buf = Encoding.ASCII.GetBytes(line + "\n");
            try
            {
                await _stream!.WriteAsync(buf, 0, buf.Length);
            }
            catch (Exception ex)
            {
                Log.Error($"SendLineAsync Exception: {ex.Message}");
            }
        }

        private async Task<string> ReadFrameAsync(
            TimeSpan timeout,
            CancellationToken ct,
            string startMarker = "OP",
            string endMarker = "PO",
            Encoding? encoding = null,
            int maxFrameBytes = 64 * 1024 // 最大帧长度防御性限制
        )
        {
            if (_stream is null) throw new InvalidOperationException("_stream is null.");

            encoding ??= Encoding.ASCII;

            // 将标记转换为字节序列（避免逐字符 cast）
            byte[] startBytes = encoding.GetBytes(startMarker);
            byte[] endBytes = encoding.GetBytes(endMarker);

            // 读块缓冲区（可根据吞吐调整大小）
            byte[] readBuffer = ArrayPool<byte>.Shared.Rent(2048);
            var acc = new List<byte>(4096); // 累计原始字节
            int startPos = -1; // startMarker 之后的载荷开始位置（字节级）
            int searchPos = 0;  // 下一次查找起点，避免每次从头扫描

            // 统一的截止时间（deadline），防止循环超时不精确
            var deadline = DateTime.UtcNow + timeout;

            try
            {
                while (true)
                {
                    // 计算剩余超时；若已到期，抛超时
                    var remaining = deadline - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero)
                        throw new TimeoutException("ReadFrameAsync timed out while waiting for device frame.");

                    // 不给 ReadAsync 传取消（有些流不响应），用 WhenAny 自己做超时
                    var readTask = _stream!.ReadAsync(readBuffer.AsMemory(0, readBuffer.Length)).AsTask();
                    var winTask = await Task.WhenAny(readTask, Task.Delay(remaining, ct)).ConfigureAwait(false);

                    if (winTask != readTask)
                    {
                        // Delay/取消赢了：要么外部取消，要么超时
                        ct.ThrowIfCancellationRequested(); // 外部取消优先
                        throw new TimeoutException("ReadFrameAsync timed out while waiting for device frame.");
                    }

                    int n = await readTask.ConfigureAwait(false);
                    if (n == 0)
                    {
                        // EOF：连接关闭
                        if (startPos >= 0)
                        {
                            // 已经看到 OP 但未完整结束，返回目前载荷（也可选择抛异常）
                            var payload = acc.GetRange(startPos, acc.Count - startPos);
                            return encoding.GetString(CollectionsMarshal.AsSpan(payload)).TrimEnd('\r', '\n');
                        }
                        throw new InvalidOperationException("Connection closed before a complete frame was received.");
                    }

                    // 附加到累积缓冲
                    if (acc.Count + n > maxFrameBytes)
                        throw new InvalidOperationException($"Frame exceeds max length ({maxFrameBytes} bytes).");

                    acc.AddRange(readBuffer.AsSpan(0, n).ToArray());

                    // 1) 若还没找到开始标记，先在 [searchPos..acc.Count) 查找 startMarker
                    if (startPos < 0)
                    {
                        int idxStart = IndexOf(acc, startBytes, searchPos);
                        if (idxStart >= 0)
                        {
                            startPos = idxStart + startBytes.Length;
                            searchPos = startPos; // 之后从载荷起点继续找 endMarker
                        }
                        else
                        {
                            // 为避免跨块错过匹配，保留最多 (markerLen-1) 的回退空间
                            searchPos = Math.Max(acc.Count - (startBytes.Length - 1), 0);
                            continue; // 继续读下一块
                        }
                    }

                    // 2) 找到开始后，在 [searchPos..acc.Count) 查找结束标记
                    int idxEnd = IndexOf(acc, endBytes, searchPos);
                    if (idxEnd >= 0)
                    {
                        // [startPos .. idxEnd) 是完整负载，不含 endMarker
                        int payloadLen = idxEnd - startPos;
                        ReadOnlySpan<byte> payloadSpan = CollectionsMarshal.AsSpan(acc).Slice(startPos, payloadLen);
                        string frame = encoding.GetString(payloadSpan).TrimEnd('\r', '\n');

                        return frame;
                    }
                    else
                    {
                        // 更新搜索起点，保留跨块匹配所需的回退长度
                        searchPos = Math.Max(acc.Count - (endBytes.Length - 1), startPos);
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(readBuffer);
            }

            // 局部函数：在 List<byte> 中查找子序列（朴素算法，已足够）
            static int IndexOf(List<byte> source, byte[] pattern, int from)
            {
                if (pattern.Length == 0) return from;
                int last = source.Count - pattern.Length;
                for (int i = from; i <= last; i++)
                {
                    int j = 0;
                    for (; j < pattern.Length; j++)
                        if (source[i + j] != pattern[j])
                            break;
                    if (j == pattern.Length)
                        return i;
                }
                return -1;
            }
        }

        private async Task<string> ReadAllLinesAsync(TimeSpan timeout, CancellationToken ct)
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

        #region 工具方法

        private static async Task ReadAtLeastAsync(NetworkStream stream, List<byte> target, int size, CancellationToken ct)
        {
            int remaining = size;
            byte[] buf = ArrayPool<byte>.Shared.Rent(Math.Min(4096, size));
            try
            {
                while (remaining > 0)
                {
                    int n = await stream.ReadAsync(buf.AsMemory(0, Math.Min(buf.Length, remaining)), ct);
                    if (n <= 0) throw new IOException("Stream ended before expected bytes were read.");
                    target.AddRange(buf.AsSpan(0, n).ToArray());
                    remaining -= n;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buf);
            }
        }

        // 解析首行分辨率：优先数值（可带单位 m），否则回退
        private static double TryParseResolution(string line, double fallback)
        {
            string s = line.Replace("m", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double n) && n > 0)
                return n;
            return fallback;
        }

        // 将固定宽度 ASCII 12B 解析为 double
        private static double ParseChunkAsAsciiDouble(List<byte> buf, int offset, int len)
        {
            var slice = buf.Skip(offset).Take(len).ToArray();
            string s = Encoding.ASCII.GetString(slice).Trim();
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                return v;

            // 如果你的设备的 12B 是二进制而非 ASCII，请把这里替换为二进制解析。
            throw new FormatException($"无法解析 12B 数据块为数值：\"{s}\"");
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

        #endregion 工具方法
    }
}