using System.Net.Sockets;
using System.Text;

public class DonglongFlaAdapter : IInstrumentAdapter
{
    public string DeviceId => _info.DeviceId;
    private readonly DeviceInfo _info;
    private TcpClient? _client;
    private NetworkStream? _stream;

    public DonglongFlaAdapter(DeviceInfo info)
    {
        _info = info;
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        _client = new TcpClient();
        await _client.ConnectAsync(_info.Host, _info.Port, ct); // 4300
        _stream = _client.GetStream();

        // 连接成功后应收到 "OCI"
        var banner = await ReadUntilAsync("OCI", TimeSpan.FromSeconds(5), ct);
        if (!banner) throw new Exception("Handshake failed: OCI not received");
    }

    public Task<bool> SelfTestAsync(CancellationToken ct) => Task.FromResult(true);

    public async Task<MeasureResult> ExecuteAsync(MeasureTask task, CancellationToken ct)
    {
        if (task.Type != MeasureType.Otdr)
            throw new NotSupportedException("This adapter currently supports Otdr only.");

        var p = task.Params;
        var mode = p.TryGetValue("op_mode", out var m) ? m.ToLowerInvariant() : "scan";

        if (mode == "scan")
        {
            if (p.TryGetValue("sr_mode", out var sr)) await SendLineAsync($"SR_{sr}", ct);
            if (p.TryGetValue("gain", out var g))
            {
                var gx = g switch { "1" => "1", "2" => "2", "5" => "3", "10" => "4", _ => "1" };
                await SendLineAsync($"G_{gx}", ct);
            }
            if (p.TryGetValue("wr_len", out var wr)) await SendLineAsync($"WR_{Fmt5(wr)}", ct);
            if (p.TryGetValue("x_center", out var x)) await SendLineAsync($"X_{Fmt5(x)}", ct);

            await SendLineAsync("SCAN", ct);

            var (resolution, ys) = await ParseScanPayload(ct);

            double wrLen = GetDoubleSafe(p, "wr_len", 0.0);
            double xCenter = GetDoubleSafe(p, "x_center", 0.0);
            var start = xCenter - wrLen / 2.0;
            var points = new List<double[]>(ys.Count);
            for (int i = 0; i < ys.Count; i++)
            {
                var xPos = start + i * resolution;
                points.Add(new[] { xPos, ys[i] });
            }

            var traceJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                x_unit = "distance_m",
                y_unit = "reflectance_db",
                resolution_m = resolution,
                window_len_m = wrLen,
                center_m = xCenter,
                points = points
            });

            var scalars = new Dictionary<string, double>
            {
                ["resolution_m"] = resolution,
                ["window_len_m"] = wrLen,
                ["center_m"] = xCenter,
                ["point_count"] = ys.Count
            };

            return new MeasureResult(task.TaskId, DeviceId, task.Type, DateTime.UtcNow,
                Success: true, Error: null, Scalars: scalars, TraceJson: traceJson, Metadata: null);
        }
        else if (mode == "auto_peak")
        {
            var cmd = BuildAutoPeakCommand(p);
            await SendLineAsync(cmd, ct);

            var line = await ReadLineAsync(ct);
            if (line.Contains("INPUT_ERROR", StringComparison.OrdinalIgnoreCase))
                throw new Exception("INPUT_ERROR from device");

            var peak = ParseAutoPeakResponse(line);
            var scalars = new Dictionary<string, double>
            {
                ["peak_pos_m"] = peak.pos_m,
                ["peak_db"] = peak.db,
                ["id"] = peak.id,
                ["checksum"] = peak.sum
            };
            var meta = new Dictionary<string, string> { ["sn"] = peak.sn, ["raw"] = line };

            return new MeasureResult(task.TaskId, DeviceId, task.Type, DateTime.UtcNow,
                Success: true, Error: null, Scalars: scalars, TraceJson: null, Metadata: meta);
        }
        else
        {
            throw new NotSupportedException($"Unknown op_mode: {mode}");
        }
    }

    public Task DisconnectAsync()
    {
        _stream?.Dispose();
        _client?.Close();
        return Task.CompletedTask;
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

    private static string Fmt5(string raw)
    {
        var s = raw.Trim();
        if (s.Length > 5) s = s[..5];
        return s.PadLeft(5, '0');
    }

    private static double GetDoubleSafe(Dictionary<string, string> p, string key, double defV)
        => p.TryGetValue(key, out var v) && double.TryParse(v, out var d) ? d : defV;

    private async Task<(double resolution, List<double> ys)> ParseScanPayload(CancellationToken ct)
    {
        var resLine = await ReadLineAsync(ct);
        double resolution = 0.0;
        double.TryParse(new string(resLine.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray()), out resolution);

        var ys = new List<double>();
        var payload = await ReadUntilBangAsync(ct);

        var bytes = Encoding.ASCII.GetBytes(payload);
        int chunk = 12;
        for (int i = 0; i + chunk <= bytes.Length; i += chunk)
        {
            var s = Encoding.ASCII.GetString(bytes, i, chunk).Trim();
            if (double.TryParse(s, out var v)) ys.Add(v);
        }

        return (resolution, ys);
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

    private static string BuildAutoPeakCommand(Dictionary<string, string> p)
    {
        var start = p["start_m"]; var end = p["end_m"];
        var count = p.GetValueOrDefault("count_mode", "2");
        var algo = p.GetValueOrDefault("algo", "2");
        var width = p["width_m"];
        var thr = p["threshold_db"];
        var id = p["id"];
        var sn = p["sn"]; // 形如 SN1A234

        var sum = CalcSum(new[] { start, end, count, algo, width, thr, id, sn });
        var cmd = $"SCAN_{start}_{end}_{count}_{algo}_{width}_{thr}_{id}_{sn}_{sum:F3}_NACS";
        return cmd;
    }

    private static double CalcSum(IEnumerable<string> parts)
    {
        double total = 0.0;
        foreach (var s in parts)
        {
            var nums = ExtractNumbers(s);
            foreach (var x in nums) total += Math.Abs(x);
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

    private static (double pos_m, double db, double id, string sn, double sum) ParseAutoPeakResponse(string line)
    {
        var segs = line.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (segs.Length < 6 || !segs[0].Equals("OP", StringComparison.OrdinalIgnoreCase))
            throw new Exception($"Unexpected auto-peak response: {line}");

        double pos = double.Parse(segs[1]);
        double db = double.Parse(segs[2]);
        double id = double.Parse(segs[3]);
        string sn = segs[4];
        double sum = double.Parse(segs[5]);

        var calc = CalcSum(new[] { pos.ToString(), db.ToString(), id.ToString(), sn });
        if (Math.Abs(calc - sum) > 0.1)
            throw new Exception($"Checksum mismatch: calc={calc:F3}, dev={sum:F3}");

        return (pos, db, id, sn, sum);
    }
}