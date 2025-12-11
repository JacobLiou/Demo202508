using Serilog;
using System.Threading.Channels;

namespace OTMS
{
    public class MeasurementWorker
    {
        private readonly Channel<MeasureTask> _queue; private readonly TcpServer _server; private readonly FlaInstrumentAdapter _fla; private readonly OpticalSwitchController _switch; private readonly SemaphoreSlim _deviceLock = new(1, 1);

        public MeasurementWorker(Channel<MeasureTask> queue, TcpServer server, FlaInstrumentAdapter fla, OpticalSwitchController sw)
        { _queue = queue; _server = server; _fla = fla; _switch = sw; }

        public async Task StartAsync(CancellationToken ct)
        {
            Log.Information("Worker started");
            while (!ct.IsCancellationRequested)
            {
                MeasureTask task; try { task = await _queue.Reader.ReadAsync(ct); } catch (OperationCanceledException) { break; }
                await _deviceLock.WaitAsync(ct);
                try
                {
                    Log.Information("Task start {TaskId} ch={Channel} mode={Mode}", task.TaskId, task.Channel, task.Mode);
                    await _server.SendResultAsync(task, new StatusMessage("status", task.TaskId, "switching"), ct);
                    await _switch.SwitchToOutputAsync(task.Channel, ct);
                    await _server.SendResultAsync(task, new StatusMessage("status", task.TaskId, "running"), ct);

                    await _fla.ConnectAsync(ct);
                    object data;
                    if (task.Mode.Equals("scan", StringComparison.OrdinalIgnoreCase))
                    {
                        await _fla.SetResolutionAsync(task.Params.GetValueOrDefault("sr_mode", "0"), ct);
                        await _fla.SetGainAsync(task.Params.GetValueOrDefault("gain", "1"), ct);
                        await _fla.SetWindowAsync(task.Params.GetValueOrDefault("wr_len", "10.00"), ct);
                        await _fla.SetCenterAsync(task.Params.GetValueOrDefault("x_center", "000.0"), ct);
                        var res = await _fla.ScanAsync(ct);
                        var segmentLen = res.resolution_m * res.pointsCount;
                        data = new { channel = task.Channel, mode = task.Mode, resolution_m = res.resolution_m, point_count = res.pointsCount, segment_length_m = Math.Round(segmentLen, 3) };
                    }
                    else if (task.Mode.Equals("auto_peak", StringComparison.OrdinalIgnoreCase))
                    {
                        var peak = await _fla.AutoPeakAsync(
                            task.Params["start_m"], task.Params["end_m"],
                            task.Params.GetValueOrDefault("count_mode", "2"),
                            task.Params.GetValueOrDefault("algo", "2"),
                            task.Params["width_m"], task.Params["threshold_db"],
                            task.Params["id"], task.Params["sn"], ct);
                        data = new { channel = task.Channel, mode = task.Mode, peak_pos_m = peak.pos_m, peak_db = peak.db, id = peak.id, sn = peak.sn };
                    }
                    else throw new Exception($"unknown mode {task.Mode}");

                    await _server.SendResultAsync(task, new ResultMessage("result", task.TaskId, true, data, null), ct);
                    Log.Information("Task success {TaskId}", task.TaskId);
                }
                catch (Exception ex)
                {
                    await _server.SendResultAsync(task, new ResultMessage("result", task.TaskId, false, null, ex.Message), ct);
                    Log.Error(ex, "Task failed {TaskId}", task.TaskId);
                }
                finally
                {
                    await _fla.DisconnectAsync();
                    _deviceLock.Release();
                }
            }
            Log.Information("Worker stopped");
        }
    }
}