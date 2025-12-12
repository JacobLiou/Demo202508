using System.Net.Sockets;
using System.Text;

namespace FlaQueueServer.Devices
{
    public class FlaInstrumentAdapter
    {
        private readonly string _host;
        private readonly int _port; // 4300
        private TcpClient? _client;
        private NetworkStream? _stream;

        public FlaInstrumentAdapter(string host, int port)
        { _host = host; _port = port; }

        public async Task ConnectAsync(CancellationToken ct)
        {
            _client = new TcpClient();
            await _client.ConnectAsync(_host, _port, ct);
            _stream = _client.GetStream();
            // 握手：设备会发 "OCI" 表示连接成功（协议文档）
            var ok = await ReadUntilAsync("OCI", TimeSpan.FromSeconds(5), ct);
            if (!ok) throw new Exception("Handshake failed: OCI not received");
        }

        public async Task DisconnectAsync()
        {
            try { _stream?.Dispose(); } catch { }
            try { _client?.Close(); } catch { }
        }

        public async Task SetResolutionAsync(string srMode, CancellationToken ct)
            => await SendAndExpectOkAsync($"SR_{srMode}", ct);

        public async Task SetGainAsync(string gain, CancellationToken ct)
        {
            // 协议中：G_1=×1, G_2=×2, G_3=×5, G_4=×10
            var gmap = gain switch { "1" => "1", "2" => "2", "5" => "3", "10" => "4", _ => "1" };
            await SendAndExpectOkAsync($"G_{gmap}", ct);
        }

        public async Task SetWindowAsync(string wr, CancellationToken ct)
            => await SendAndExpectOkAsync($"WR_{Fmt5(wr)}", ct);

        public async Task SetCenterAsync(string x, CancellationToken ct)
            => await SendAndExpectOkAsync($"X_{Fmt5(x)}", ct);

        public async Task<(double resolution_m, int pointsCount)> ScanAsync(CancellationToken ct)
        {
            await SendLineAsync("SCAN", ct);
            // 首行分辨率
            var resLine = await ReadLineAsync(ct);
            double resolution = ParseNumber(resLine);
            // 读到 '!' 为止的负载；每点12字节（ASCII），仅纵坐标
            var payload = await ReadUntilBangAsync(ct);
            var bytes = Encoding.ASCII.GetBytes(payload);
            int chunk = 12; int count = 0;
            for (int i = 0; i + chunk <= bytes.Length; i += chunk)
            {
                // 可选：解析 double 值，这里只计数
                count++;
            }
            return (resolution, count);
        }

        public async Task<(double pos_m, double db, double id, string sn, double sum)> AutoPeakAsync(
            string start, string end, string count, string algo, string width, string thr, string id, string sn, CancellationToken ct)
        {
            var sum = CalcSum(new[] { start, end, count, algo, width, thr, id, sn });
            var cmd = $"SCAN_{start}_{end}_{count}_{algo}_{width}_{thr}_{id}_{sn}_{sum:F3}_NACS";
            await SendLineAsync(cmd, ct);
            var line = await ReadLineAsync(ct);
            if (line.Contains("INPUT_ERROR", StringComparison.OrdinalIgnoreCase))
                throw new Exception("INPUT_ERROR from device");
            var segs = line.Split('_', StringSplitOptions.RemoveEmptyEntries);
            if (segs.Length < 6 || !segs[0].Equals("OP", StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Unexpected response: {line}");
            double pos = double.Parse(segs[1]);
            double db = double.Parse(segs[2]);
            double idv = double.Parse(segs[3]);
            string snv = segs[4];
            double sumDev = double.Parse(segs[5]);
            var calc = CalcSum(new[] { pos.ToString(), db.ToString(), idv.ToString(), snv });
            if (Math.Abs(calc - sumDev) > 0.1)
                throw new Exception($"Checksum mismatch: calc={calc:F3}, dev={sumDev:F3}");
            return (pos, db, idv, snv, sumDev);
        }

        private async Task SendAndExpectOkAsync(string cmd, CancellationToken ct)
        {
            await SendLineAsync(cmd, ct);
            // 文档描述设置类指令回传 "SET OK"
            var line = await ReadLineAsync(ct);
            if (!line.Contains("OK", StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Device did not return OK for {cmd}: '{line}'");
        }

        private async Task SendLineAsync(string line, CancellationToken ct)
        {
            var buf = Encoding.ASCII.GetBytes(line + "\n");
            await _stream!.WriteAsync(buf, 0, buf.Length, ct);
        }

        private async Task<string> ReadLineAsync(CancellationToken ct)
        {
            var sb = new StringBuilder();
            var buffer = new byte[1];
            while (true)
            {
                var n = await _stream!.ReadAsync(buffer.AsMemory(0, 1), ct);
                if (n == 0) break;
                var ch = (char)buffer[0];
                if (ch == '\n') break;
                sb.Append(ch);
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

        private async Task<string> ReadUntilBangAsync(CancellationToken ct)
        {
            var mem = new MemoryStream();
            var buf = new byte[1024];
            while (true)
            {
                var n = await _stream!.ReadAsync(buf, ct);
                if (n <= 0) break;
                int bangIdx = Array.IndexOf(buf, (byte)'!');
                if (bangIdx >= 0)
                {
                    mem.Write(buf, 0, bangIdx);
                    break;
                }
                mem.Write(buf, 0, n);
            }
            return Encoding.ASCII.GetString(mem.ToArray());
        }

        private static string Fmt5(string raw)
        {
            var s = raw.Trim();
            if (s.Length > 5) s = s[..5];
            return s.PadLeft(5, '0');
        }

        private static double ParseNumber(string s)
        {
            double.TryParse(new string(s.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray()), out var v);
            return v;
        }

        private static double CalcSum(IEnumerable<string> parts)
        {
            double total = 0.0;
            foreach (var s in parts)
            {
                foreach (var x in ExtractNumbers(s)) total += Math.Abs(x);
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
                else { if (token.Length > 0 && double.TryParse(token.ToString(), out var vd)) list.Add(vd); token.Clear(); }
            }
            if (token.Length > 0 && double.TryParse(token.ToString(), out var v)) list.Add(v);
            return list;
        }
    }
}