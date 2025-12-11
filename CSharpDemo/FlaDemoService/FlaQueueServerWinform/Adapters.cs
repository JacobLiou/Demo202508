using Serilog;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;

namespace OTMS
{
    // FLA instrument adapter - TCP 4300 with OCI handshake, SR/G/WR/X/SCAN and AUTO PEAK
    public class FlaInstrumentAdapter
    {
        private readonly string _host; private readonly int _port; private TcpClient? _client; private NetworkStream? _stream;

        public FlaInstrumentAdapter(string host, int port)
        { _host = host; _port = port; }

        public async Task ConnectAsync(CancellationToken ct)
        { _client = new TcpClient(); await _client.ConnectAsync(_host, _port, ct); _stream = _client.GetStream(); Log.Debug("FLA TCP connected {Host}:{Port}", _host, _port); var ok = await ReadUntilAsync("OCI", TimeSpan.FromSeconds(5), ct); if (!ok) throw new Exception("Handshake failed: OCI not received"); Log.Debug("FLA handshake OCI received"); }

        public async Task DisconnectAsync()
        { try { _stream?.Dispose(); } catch { } try { _client?.Close(); } catch { } }

        public async Task SetResolutionAsync(string srMode, CancellationToken ct)
        { await SendAndExpectOkAsync($"SR_{srMode}", ct); }

        public async Task SetGainAsync(string gain, CancellationToken ct)
        { var gmap = gain switch { "1" => "1", "2" => "2", "5" => "3", "10" => "4", _ => "1" }; await SendAndExpectOkAsync($"G_{gmap}", ct); }

        public async Task SetWindowAsync(string wr, CancellationToken ct)
        { await SendAndExpectOkAsync($"WR_{Fmt5(wr)}", ct); }

        public async Task SetCenterAsync(string x, CancellationToken ct)
        { await SendAndExpectOkAsync($"X_{Fmt5(x)}", ct); }

        public async Task<(double resolution_m, int pointsCount)> ScanAsync(CancellationToken ct)
        { await SendLineAsync("SCAN", ct); var resLine = await ReadLineAsync(ct); double resolution = ParseNumber(resLine); var payload = await ReadUntilBangAsync(ct); var bytes = Encoding.ASCII.GetBytes(payload); int chunk = 12; int count = 0; for (int i = 0; i + chunk <= bytes.Length; i += chunk) count++; return (resolution, count); }

        public async Task<(double pos_m, double db, double id, string sn, double sum)> AutoPeakAsync(string start, string end, string count, string algo, string width, string thr, string id, string sn, CancellationToken ct)
        { var sum = CalcSum(new[] { start, end, count, algo, width, thr, id, sn }); var cmd = $"SCAN_{start}_{end}_{count}_{algo}_{width}_{thr}_{id}_{sn}_{sum:F3}_NACS"; await SendLineAsync(cmd, ct); var line = await ReadLineAsync(ct); if (line.Contains("INPUT_ERROR", StringComparison.OrdinalIgnoreCase)) throw new Exception("INPUT_ERROR from device"); var segs = line.Split('_', StringSplitOptions.RemoveEmptyEntries); if (segs.Length < 6 || !segs[0].Equals("OP", StringComparison.OrdinalIgnoreCase)) throw new Exception($"Unexpected response: {line}"); double pos = double.Parse(segs[1]); double db = double.Parse(segs[2]); double idv = double.Parse(segs[3]); string snv = segs[4]; double sumDev = double.Parse(segs[5]); var calc = CalcSum(new[] { pos.ToString(), db.ToString(), idv.ToString(), snv }); if (Math.Abs(calc - sumDev) > 0.1) throw new Exception($"Checksum mismatch: calc={calc:F3}, dev={sumDev:F3}"); return (pos, db, idv, snv, sumDev); }

        private async Task SendAndExpectOkAsync(string cmd, CancellationToken ct)
        { await SendLineAsync(cmd, ct); var line = await ReadLineAsync(ct); if (!line.Contains("OK", StringComparison.OrdinalIgnoreCase)) throw new Exception($"Device did not return OK for {cmd}: '{line}'"); }

        private async Task SendLineAsync(string line, CancellationToken ct)
        { var buf = Encoding.ASCII.GetBytes(line + "\n"); await _stream!.WriteAsync(buf, 0, buf.Length, ct); }

        private async Task<string> ReadLineAsync(CancellationToken ct)
        { var sb = new StringBuilder(); var b = new byte[1]; while (true) { var n = await _stream!.ReadAsync(b.AsMemory(0, 1), ct); if (n == 0) break; var ch = (char)b[0]; if (ch == '\n') break; sb.Append(ch); } return sb.ToString().Trim(); }

        private async Task<bool> ReadUntilAsync(string token, TimeSpan timeout, CancellationToken ct)
        { var end = DateTime.UtcNow + timeout; var buf = new byte[1024]; var mem = new MemoryStream(); while (DateTime.UtcNow < end) { if (_stream!.DataAvailable) { var n = await _stream.ReadAsync(buf, ct); if (n > 0) mem.Write(buf, 0, n); var s = Encoding.ASCII.GetString(mem.ToArray()); if (s.Contains(token)) return true; } await Task.Delay(50, ct); } return false; }

        private async Task<string> ReadUntilBangAsync(CancellationToken ct)
        { var mem = new MemoryStream(); var buf = new byte[1024]; while (true) { var n = await _stream!.ReadAsync(buf, ct); if (n <= 0) break; int idx = Array.IndexOf(buf, (byte)'!'); if (idx >= 0) { mem.Write(buf, 0, idx); break; } mem.Write(buf, 0, n); } return Encoding.ASCII.GetString(mem.ToArray()); }

        private static string Fmt5(string raw)
        { var s = raw.Trim(); if (s.Length > 5) s = s[..5]; return s.PadLeft(5, '0'); }

        private static double ParseNumber(string s)
        { double.TryParse(new string(s.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray()), out var v); return v; }

        private static double CalcSum(IEnumerable<string> parts)
        { double t = 0; foreach (var s in parts) foreach (var x in ExtractNumbers(s)) t += Math.Abs(x); return t; }

        private static IEnumerable<double> ExtractNumbers(string s)
        { var list = new List<double>(); var token = new StringBuilder(); foreach (var ch in s) { if (char.IsDigit(ch) || ch == '.' || ch == '-') token.Append(ch); else { if (token.Length > 0 && double.TryParse(token.ToString(), out var vd)) list.Add(vd); token.Clear(); } } if (token.Length > 0 && double.TryParse(token.ToString(), out var v)) list.Add(v); return list; }
    }

    // Optical switch controller - RS232 (ASCII + CRLF, 'OK' and '>' prompt)
    public class OpticalSwitchController : IDisposable
    {
        private readonly string _portName; private readonly int _baud; private readonly int _switchIndex; private readonly int _inputChannel; private readonly SerialPort _port;

        public OpticalSwitchController(string portName, int baud, int switchIndex, int inputChannel)
        { _portName = portName; _baud = baud; _switchIndex = switchIndex; _inputChannel = inputChannel; _port = new SerialPort(_portName, _baud, Parity.None, 8, StopBits.One) { Handshake = Handshake.None, ReadTimeout = 1000, WriteTimeout = 1000, NewLine = "\r\n" }; try { _port.Open(); Log.Information("Switch opened {Port}@{Baud}", _portName, _baud); } catch (Exception ex) { Log.Error(ex, "Switch open failed {Port}@{Baud}", _portName, _baud); } }

        public async Task SwitchToOutputAsync(int outputChannel, CancellationToken ct)
        { var cmd = $"SW {_switchIndex} SPOS {_inputChannel} {outputChannel}\r\n"; Log.Debug("SWITCH send: {Cmd}", cmd.Replace("\r\n", "\\r\\n")); await WriteAsync(cmd, ct); var line = await ReadLineAsync(ct); Log.Debug("SWITCH recv: {Line}", line); if (line is null || !line.Contains("OK", StringComparison.OrdinalIgnoreCase)) throw new Exception($"Switch command failed: {line ?? "(no response)"}"); await ReadUntilPromptAsync(ct); Log.Debug("SWITCH prompt '>' received"); }

        private Task WriteAsync(string cmd, CancellationToken ct) => Task.Run(() => { _port.Write(cmd); }, ct);

        private Task<string?> ReadLineAsync(CancellationToken ct) => Task.Run(() => { try { return _port.ReadLine(); } catch { return null; } }, ct);

        private Task ReadUntilPromptAsync(CancellationToken ct) => Task.Run(() => { try { while (true) { var ch = (char)_port.ReadChar(); if (ch == '>') break; } } catch { } }, ct);

        public void Dispose()
        { try { _port?.Close(); } catch { } try { _port?.Dispose(); } catch { } }
    }
}