using FlaQueueServer.Models;
using Serilog;
using System.Threading.Channels;

namespace FlaQueueServer.Core
{
    public class MeasurementWorkerMock
    {
        private readonly Channel<MeasureTask> _queue;
        private readonly TcpServer _server;
        private readonly SemaphoreSlim _deviceLock = new(1, 1);
        private readonly ILogger Log = Serilog.Log.ForContext<MeasurementWorkerMock>();
        private readonly int _switchDelayMs;
        private readonly (int minMs, int maxMs) _scanDelayMs;
        private readonly (int minMs, int maxMs) _peakDelayMs;
        private readonly Random _rand = new Random(Environment.TickCount);

        public MeasurementWorkerMock(Channel<MeasureTask> queue, TcpServer server,
            int switchDelayMs = 200,
            int scanMinMs = 800, int scanMaxMs = 1500,
            int peakMinMs = 500, int peakMaxMs = 1000)
        {
            _queue = queue;
            _server = server;
            _switchDelayMs = switchDelayMs;
            _scanDelayMs = (scanMinMs, scanMaxMs);
            _peakDelayMs = (peakMinMs, peakMaxMs);
        }

        public async Task StartAsync(CancellationToken ct)
        {
            Log.Information("[MOCK] Worker started");
            while (!ct.IsCancellationRequested)
            {
                MeasureTask task;
                try { task = await _queue.Reader.ReadAsync(ct); }
                catch (OperationCanceledException) { break; }

                await _deviceLock.WaitAsync(ct);
                try
                {
                    Log.Information("[MOCK] Task start {TaskId} ch={Channel} mode={Mode}", task.TaskId, task.Channel, task.Mode);

                    RunningTaskTracker.Instance.MarkRunning(task.TaskId);
                    await Task.Delay(_switchDelayMs, ct);
                    Log.Information("[MOCK] Switch set to {Channel} for task {TaskId}", task.Channel, task.TaskId);
                    //await _server.SendResultAsync(task, new StatusMessage("status", task.TaskId, "running"), ct);

                    object data;
                    if (task.Mode.Equals("scan", StringComparison.OrdinalIgnoreCase))
                    {
                        var delay = _rand.Next(_scanDelayMs.minMs, _scanDelayMs.maxMs + 1);
                        await Task.Delay(delay, ct);
                        double[] resOptions = new[] { 0.0025, 0.005, 0.01, 0.02 };
                        double resolution = resOptions[_rand.Next(resOptions.Length)];
                        int pointCount = _rand.Next(180, 420);
                        double segmentLen = Math.Round(resolution * pointCount, 3);
                        data = new { channel = task.Channel, mode = task.Mode, resolution_m = resolution, point_count = pointCount, segment_length_m = segmentLen };
                        Log.Information("[MOCK] Scan done {TaskId}: res={Res}m count={Count} len={Len}m", task.TaskId, resolution, pointCount, segmentLen);
                    }
                    else if (task.Mode.Equals("auto_peak", StringComparison.OrdinalIgnoreCase))
                    {
                        var delay = _rand.Next(_peakDelayMs.minMs, _peakDelayMs.maxMs + 1);
                        await Task.Delay(delay, ct);
                        double start = ParseDouble(task.Params.GetValueOrDefault("start_m", "0.5"), 0.5);
                        double end = ParseDouble(task.Params.GetValueOrDefault("end_m", "25"), 25);
                        double pos = Math.Round(start + _rand.NextDouble() * Math.Max(0.01, end - start), 3);
                        double db = Math.Round(-30 - _rand.NextDouble() * 25, 3);
                        string id = task.Params.GetValueOrDefault("id", "01");
                        string sn = task.Params.GetValueOrDefault("sn", $"SN{task.Channel}A1");
                        data = new { channel = task.Channel, mode = task.Mode, peak_pos_m = pos, peak_db = db, id, sn };
                        Log.Information("[MOCK] Peak done {TaskId}: pos={Pos}m db={Db}dB id={Id} sn={Sn}", task.TaskId, pos, db, id, sn);
                    }
                    else
                    {
                        throw new Exception($"unknown mode {task.Mode}");
                    }

                    var result = new ResultMessage("result", task.TaskId, true, data, null);
                    await _server.SendResultAsync(task, result, ct);
                    Log.Information("[MOCK] Task success {TaskId}", task.TaskId);
                    DailyResultStore.Instance.AddOrUpdate(task.TaskId, result);
                    RunningTaskTracker.Instance.MarkFinished(task.TaskId);
                }
                catch (Exception ex)
                {
                    var failResult = new ResultMessage("result", task.TaskId, false, null, ex.Message);
                    await _server.SendResultAsync(task, failResult, ct);
                    Log.Error(ex, "[MOCK] Task failed {TaskId}", task.TaskId);
                    DailyResultStore.Instance.AddOrUpdate(task.TaskId, failResult);
                    RunningTaskTracker.Instance.MarkFinished(task.TaskId);
                }
                finally
                {
                    _deviceLock.Release();
                }
            }
            Log.Information("[MOCK] Worker stopped");
        }

        private static double ParseDouble(string s, double def)
            => double.TryParse(s, out var v) ? v : def;
    }
}