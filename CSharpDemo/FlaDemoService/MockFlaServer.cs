using System.Net;
using System.Net.Sockets;
using System.Text;

public class MockFlaServer
{
    private readonly int _port;

    private readonly double _resolutionM;

    private readonly int _points;

    private readonly bool _verbose;

    public MockFlaServer(int port = 4300, double resolutionM = 0.005, int points = 200, bool verbose = true)
    {
        _port = port;
        _resolutionM = resolutionM;
        _points = points;
        _verbose = verbose;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var listener = new TcpListener(IPAddress.Loopback, _port);
        listener.Start();
        Log($"[MockFLA] Listening on tcp://127.0.0.1:{_port}");
        while (!ct.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(ct);
            _ = HandleClient(client, ct);
        }
    }

    private async Task HandleClient(TcpClient client, CancellationToken ct)
    {
        using var stream = client.GetStream();
        var reader = new StreamReader(stream, Encoding.ASCII);
        var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };
        await writer.WriteLineAsync("OCI");
        Log("[MockFLA] Client connected, OCI sent.");
        string sr = "0"; string gain = "1"; double wr = 10.00; double x = 0.0;
        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(); if (line is null) break; line = line.Trim(); Log($"<= {line}");
            if (line == "QUIT") { await writer.WriteLineAsync("BYE"); break; }
            else if (line.StartsWith("SR_")) { sr = line[3..]; await writer.WriteLineAsync("SET OK"); }
            else if (line.StartsWith("G_")) { gain = line[2..]; await writer.WriteLineAsync("SET OK"); }
            else if (line.StartsWith("WR_")) { wr = TryParse(line[3..], 10.0); await writer.WriteLineAsync("SET OK"); }
            else if (line.StartsWith("X_")) { x = TryParse(line[2..], 0.0); await writer.WriteLineAsync("SET OK"); }
            else if (line == "SCAN")
            {
                await writer.WriteLineAsync(_resolutionM.ToString("F6"));
                var ys = GenerateCurve(_points);
                var payload = new StringBuilder();
                foreach (var y in ys) payload.Append(y.ToString("F6").PadRight(12));
                payload.Append('!'); await writer.WriteAsync(payload.ToString()); await writer.FlushAsync();
            }
            else if (line.StartsWith("SCAN_"))
            {
                var parts = line.Split('_'); if (parts.Length < 11) { await writer.WriteLineAsync("INPUT_ERROR"); continue; }
                var id = parts[7]; var sn = parts[8]; double pos = 13.226; double db = -47.820;
                double sum = Math.Abs(pos) + Math.Abs(db) + Math.Abs(double.Parse(id)) + SumDigits(sn);
                await writer.WriteLineAsync($"OP_{pos:F3}_{db:F3}_{id}_{sn}_{sum:F3}_PO");
            }
            else { await writer.WriteLineAsync("UNSUPPORTED"); }
        }
        Log("[MockFLA] Client disconnected.");
    }

    private static double TryParse(string s, double defV)
    {
        return double.TryParse(s, out var v) ? v : defV;
    }

    private static double SumDigits(string s)
    {
        double t = 0;
        foreach (var ch in s)
        {
            if (char.IsDigit(ch))
                t += ch - '0';
        }

        return t;
    }

    private static List<double> GenerateCurve(int n)
    {
        var list = new List<double>(n);
        var rand = new Random(42);
        for (int i = 0; i < n; i++)
        {
            double x = i / (double)n;
            double noise = (rand.NextDouble() - 0.5) * 0.5;
            double peak = -38.0 * Math.Exp(-Math.Pow((x - 0.33) / 0.02, 2));
            double baseLine = -60.0 + noise;
            double y = baseLine - Math.Abs(peak);
            list.Add(y);
        }

        return list;
    }

    private void Log(string s)
    {
        if (_verbose)
            Console.WriteLine("[MockFLA] " + s);
    }
}