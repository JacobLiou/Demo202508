using Serilog;
using System.Threading.Channels;

namespace FlaQueueServer
{
    public class MeasurementWorker
    {
        private readonly Channel<MeasureTask> _queue;
        private readonly TcpServer _server;
        private readonly FlaInstrumentAdapter _fla;
        private readonly OpticalSwitchController _switch;
        private readonly SemaphoreSlim _deviceLock = new(1, 1); // 共享设备串行

        private readonly ILogger Log = Serilog.Log.ForContext<MeasurementWorker>();

        public MeasurementWorker(
            Channel<MeasureTask> queue,
            TcpServer server,
            FlaInstrumentAdapter adapter,
            OpticalSwitchController sw)
        {
            _queue = queue;
            _server = server;
            _fla = adapter;
            _switch = sw;
        }

        public async Task StartAsync(CancellationToken ct)
        {
            Log.Information("Worker started");
            Console.WriteLine("[Worker] Started.");
            while (!ct.IsCancellationRequested)
            {
                MeasureTask task;
                try
                {
                    task = await _queue.Reader.ReadAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                await _deviceLock.WaitAsync(ct);
                try
                {
                    Log.Information("Task start {TaskId} ch={Channel} mode={Mode}", task.TaskId, task.Channel, task.Mode);
                    Console.WriteLine("Task start {TaskId} ch={Channel} mode={Mode}", task.TaskId, task.Channel, task.Mode);
                    await _server.SendResultAsync(task, new StatusMessage("status", task.TaskId, "switching"), ct);

                    // 1) 切光开关到指定通道（RS232）
                    await _switch.SwitchToOutputAsync(task.Channel, ct);
                    Log.Information("Switch set to {Channel} for task {TaskId}", task.Channel, task.TaskId);
                    Console.WriteLine("Switch set to {Channel} for task {TaskId}", task.Channel, task.TaskId);

                    await _server.SendResultAsync(task, new StatusMessage("status", task.TaskId, "running"), ct);

                    // 2) 连接 FLA 与执行测量
                    await _fla.ConnectAsync(ct);
                    Log.Information("FLA connected for task {TaskId}", task.TaskId);
                    Console.WriteLine("FLA connected for task {TaskId}", task.TaskId);

                    object data;
                    if (task.Mode.Equals("scan", StringComparison.OrdinalIgnoreCase))
                    {
                        await _fla.SetResolutionAsync(task.Params.GetValueOrDefault("sr_mode", "0"), ct);
                        await _fla.SetGainAsync(task.Params.GetValueOrDefault("gain", "1"), ct);
                        await _fla.SetWindowAsync(task.Params.GetValueOrDefault("wr_len", "10.00"), ct);
                        await _fla.SetCenterAsync(task.Params.GetValueOrDefault("x_center", "000.0"), ct);
                        Log.Debug("FLA scan settings applied for task {TaskId}: sr={Sr}, gain={Gain}, wr={Wr}, x={X}", task.TaskId, task.Params.GetValueOrDefault("sr_mode", "0"), task.Params.GetValueOrDefault("gain", "1"), task.Params.GetValueOrDefault("wr_len", "10.00"), task.Params.GetValueOrDefault("x_center", "000.0"));

                        var res = await _fla.ScanAsync(ct);
                        var segmentLen = res.resolution_m * res.pointsCount;
                        data = new { channel = task.Channel, mode = task.Mode, resolution_m = res.resolution_m, point_count = res.pointsCount, segment_length_m = Math.Round(segmentLen, 3) };
                        Log.Information("FLA scan done for task {TaskId}: res={Res}m, count={Count}", task.TaskId, res.resolution_m, res.pointsCount);
                        Console.WriteLine("FLA scan done for task {TaskId}: res={Res}m, count={Count}", task.TaskId, res.resolution_m, res.pointsCount);
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
                        Log.Information("FLA auto-peak done for task {TaskId}: pos={Pos}m db={Db}dB", task.TaskId, peak.pos_m, peak.db);
                        Console.WriteLine("FLA auto-peak done for task {TaskId}: pos={Pos}m db={Db}dB", task.TaskId, peak.pos_m, peak.db);
                    }
                    else
                        throw new Exception($"unknown mode {task.Mode}");

                    await _server.SendResultAsync(task, new ResultMessage("result", task.TaskId, true, data, null), ct);
                    Log.Information("Task success {TaskId}", task.TaskId);
                    Console.WriteLine("Task success {TaskId}", task.TaskId);
                }
                catch (Exception ex)
                {
                    await _server.SendResultAsync(task, new ResultMessage("result", task.TaskId, false, null, ex.Message), ct);
                    Console.WriteLine(ex.Message, "Task failed {TaskId}", task.TaskId);
                    Log.Error(ex, "Task failed {TaskId}", task.TaskId);
                }
                finally
                {
                    await _fla.DisconnectAsync();
                    Log.Debug("FLA disconnected for task {TaskId}", task.TaskId);
                    Console.WriteLine("FLA disconnected for task {TaskId}", task.TaskId);
                    _deviceLock.Release();
                }
            }

            Console.WriteLine("[Worker] stopped.");
            Log.Information("Worker stopped");
        }
    }
}