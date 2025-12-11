using System.Threading.Channels;

namespace FlaQueueServer
{
    public class MeasurementWorker
    {
        private readonly Channel<MeasureTask> _queue;
        private readonly TcpServer _server;
        private readonly FlaInstrumentAdapter _adapter;
        private readonly SemaphoreSlim _deviceLock = new(1, 1); // 共享设备串行

        public MeasurementWorker(Channel<MeasureTask> queue, TcpServer server, FlaInstrumentAdapter adapter)
        { _queue = queue; _server = server; _adapter = adapter; }

        public async Task StartAsync(CancellationToken ct)
        {
            Console.WriteLine("[Worker] Started.");
            while (!ct.IsCancellationRequested)
            {
                MeasureTask task;
                try { task = await _queue.Reader.ReadAsync(ct); }
                catch (OperationCanceledException) { break; }

                await _deviceLock.WaitAsync(ct);
                try
                {
                    await _server.SendResultAsync(task, new StatusMessage("status", task.TaskId, "running"), ct);

                    // 连接设备（握手 OCI）
                    await _adapter.ConnectAsync(ct);

                    object data;
                    if (task.Mode.Equals("scan", StringComparison.OrdinalIgnoreCase))
                    {
                        // 参数：sr_mode, gain, wr_len, x_center
                        await _adapter.SetResolutionAsync(task.Params.GetValueOrDefault("sr_mode", "0"), ct);
                        await _adapter.SetGainAsync(task.Params.GetValueOrDefault("gain", "1"), ct);
                        await _adapter.SetWindowAsync(task.Params.GetValueOrDefault("wr_len", "10.00"), ct);
                        await _adapter.SetCenterAsync(task.Params.GetValueOrDefault("x_center", "000.0"), ct);

                        var res = await _adapter.ScanAsync(ct);
                        // 计算段长度
                        var segmentLen = res.resolution_m * res.pointsCount;
                        data = new
                        {
                            channel = task.Channel,
                            mode = task.Mode,
                            resolution_m = res.resolution_m,
                            point_count = res.pointsCount,
                            segment_length_m = Math.Round(segmentLen, 3)
                        };
                    }
                    else if (task.Mode.Equals("auto_peak", StringComparison.OrdinalIgnoreCase))
                    {
                        // 参数：start_m, end_m, count_mode, algo, width_m, threshold_db, id, sn
                        var peak = await _adapter.AutoPeakAsync(
                            task.Params["start_m"], task.Params["end_m"],
                            task.Params.GetValueOrDefault("count_mode", "2"),
                            task.Params.GetValueOrDefault("algo", "2"),
                            task.Params["width_m"], task.Params["threshold_db"],
                            task.Params["id"], task.Params["sn"], ct);

                        data = new
                        {
                            channel = task.Channel,
                            mode = task.Mode,
                            peak_pos_m = peak.pos_m,
                            peak_db = peak.db,
                            id = peak.id,
                            sn = peak.sn
                        };
                    }
                    else
                    {
                        throw new Exception($"unknown mode {task.Mode}");
                    }

                    await _server.SendResultAsync(task, new ResultMessage("result", task.TaskId, true, data, null), ct);
                }
                catch (Exception ex)
                {
                    await _server.SendResultAsync(task, new ResultMessage("result", task.TaskId, false, null, ex.Message), ct);
                }
                finally
                {
                    await _adapter.DisconnectAsync();
                    _deviceLock.Release();
                }
            }
            Console.WriteLine("[Worker] Stopped.");
        }
    }
}