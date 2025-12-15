using FlaQueueServer.Devices;
using FlaQueueServer.Models;
using Serilog;
using System.Threading.Channels;

namespace FlaQueueServer.Core
{
    public class MeasurementWorker
    {
        private readonly Channel<MeasureTask> _queue;
        private readonly TcpServer _server;
        private readonly FlaInstrumentAdapter _fla;
        private readonly OpticalSwitchController _switch;
        private readonly SemaphoreSlim _deviceLock = new(1, 1);
        private static readonly Random _jitter = new Random();

        // 切开关最多重试 3 次
        private const int RETRY_SWITCH_MAX = 3;

        // FLA 连接最多重试 3 次
        private const int RETRY_CONNECT_MAX = 3;

        // SR/G/WR/X 设置最多重试 3 次
        private const int RETRY_SET_MAX = 3;

        // SCAN/AutoPeak 测量最多重试 3 次
        private const int RETRY_MEASURE_MAX = 3;

        private static readonly TimeSpan BASE_DELAY_SWITCH = TimeSpan.FromMilliseconds(200);

        private static readonly TimeSpan BASE_DELAY_CONNECT = TimeSpan.FromMilliseconds(300);

        private static readonly TimeSpan BASE_DELAY_SET = TimeSpan.FromMilliseconds(150);

        private static readonly TimeSpan BASE_DELAY_MEASURE = TimeSpan.FromMilliseconds(300);

        private readonly ILogger Log = Serilog.Log.ForContext<MeasurementWorker>();

        public MeasurementWorker(Channel<MeasureTask> queue, TcpServer server, FlaInstrumentAdapter adapter, OpticalSwitchController sw)
        {
            _queue = queue;
            _server = server;
            _fla = adapter;
            _switch = sw;
        }

        public async Task StartAsync(CancellationToken ct)
        {
            Log.Information("Worker started");
            while (!ct.IsCancellationRequested)
            {
                MeasureTask task;
                try { task = await _queue.Reader.ReadAsync(ct); }
                catch (OperationCanceledException) { break; }

                await _deviceLock.WaitAsync(ct);
                try
                {
                    Log.Information("Task start {TaskId} ch={Channel} mode={Mode}", task.TaskId, task.ClientId, task.Mode);
                    // notify switching (use unified ResultMessage with status)
                    await _server.SendResultAsync(task, new ResultMessage("result", task.TaskId, status: "switching"), ct);

                    // 1) 切光开关 —— 带重试
                    await RetryAsync(
                        async () => await _switch.SwitchToOutputAsync(task.ClientId, ct),
                        "switch",
                        RETRY_SWITCH_MAX,
                        BASE_DELAY_SWITCH,
                        ct,
                        Log,
                        failDetail: $"channel={task.ClientId}"
                    );
                    Log.Information("Switch set to {Channel} for task {TaskId}", task.ClientId, task.TaskId);

                    // 标记为运行中并通知客户端
                    RunningTaskTracker.Instance.MarkRunning(task.TaskId);
                    await _server.SendResultAsync(task, new ResultMessage("result", task.TaskId, status: "running"), ct);

                    // 2) 连接 FLA —— 带重试
                    await RetryAsync(
                        async () => await _fla.ConnectAsync(ct),
                        "connect",
                        RETRY_CONNECT_MAX,
                        BASE_DELAY_CONNECT,
                        ct,
                        Log,
                        failDetail: "FLA tcp handshake"
                    );
                    Log.Information("FLA connected for task {TaskId}", task.TaskId);

                    object data;

                    if (task.Mode.Equals("scan", StringComparison.OrdinalIgnoreCase))
                    {
                        // 3) 设置 SR/G/WR/X —— 每条指令带重试
                        await RetryAsync(async () => await _fla.SetResolutionAsync(task.Params.GetValueOrDefault("sr_mode", "0"), ct), "set-SR", RETRY_SET_MAX, BASE_DELAY_SET, ct, Log);
                        await RetryAsync(async () => await _fla.SetGainAsync(task.Params.GetValueOrDefault("gain", "1"), ct), "set-G", RETRY_SET_MAX, BASE_DELAY_SET, ct, Log);
                        await RetryAsync(async () => await _fla.SetWindowAsync(task.Params.GetValueOrDefault("wr_len", "10.00"), ct), "set-WR", RETRY_SET_MAX, BASE_DELAY_SET, ct, Log);
                        await RetryAsync(async () => await _fla.SetCenterAsync(task.Params.GetValueOrDefault("x_center", "000.0"), ct), "set-X", RETRY_SET_MAX, BASE_DELAY_SET, ct, Log);

                        // 4) 执行 SCAN —— 带重试（如果设备忙或网络瞬断）
                        var res = await RetryAsync(
                            async () => await _fla.ScanAsync(ct),
                            "scan",
                            RETRY_MEASURE_MAX,
                            BASE_DELAY_MEASURE,
                            ct,
                            Log
                        );

                        var segmentLen = res.resolution_m * res.pointsCount;
                        data = new
                        {
                            ClientId = task.ClientId,
                            mode = task.Mode,
                            res.resolution_m,
                            point_count = res.pointsCount,
                            segment_length_m = Math.Round(segmentLen, 3)
                        };

                        Log.Information("FLA scan done for task {TaskId}: res={Res}m, count={Count}", task.TaskId, res.resolution_m, res.pointsCount);
                    }
                    else if (task.Mode.Equals("zero", StringComparison.OrdinalIgnoreCase))
                    {
                        // 3) 执行归零操作
                        var peak = await RetryAsync(
                            async () => await _fla.AutoPeakAsync(
                                task.Params["start_m"], task.Params["end_m"],
                                task.Params.GetValueOrDefault("count_mode", "2"),
                                task.Params.GetValueOrDefault("algo", "2"),
                                task.Params["width_m"], task.Params["threshold_db"],
                                task.Params["id"], task.Params["sn"], ct),
                            "auto-peak",
                            RETRY_MEASURE_MAX,
                            BASE_DELAY_MEASURE,
                            ct,
                            Log,
                            failDetail: $"id={task.Params.GetValueOrDefault("id", "")}, sn={task.Params.GetValueOrDefault("sn", "")}"
                        );

                        data = new
                        {
                            channel = task.ClientId,
                            mode = task.Mode,
                            peak_pos_m = peak.pos_m,
                            peak_db = peak.db,
                            peak.id,
                            peak.sn
                        };

                        Log.Information("FLA auto-peak done for task {TaskId}: pos={Pos}m db={Db}dB", task.TaskId, peak.pos_m, peak.db);
                    }
                    else if (task.Mode.Equals("auto_peak", StringComparison.OrdinalIgnoreCase))
                    {
                        // 3) 执行寻峰 —— 带重试
                        var peak = await RetryAsync(
                            async () => await _fla.AutoPeakAsync(
                                task.Params["start_m"], task.Params["end_m"],
                                task.Params.GetValueOrDefault("count_mode", "2"),
                                task.Params.GetValueOrDefault("algo", "2"),
                                task.Params["width_m"], task.Params["threshold_db"],
                                task.Params["id"], task.Params["sn"], ct),
                            "auto-peak",
                            RETRY_MEASURE_MAX,
                            BASE_DELAY_MEASURE,
                            ct,
                            Log,
                            failDetail: $"id={task.Params.GetValueOrDefault("id", "")}, sn={task.Params.GetValueOrDefault("sn", "")}"
                        );

                        data = new
                        {
                            channel = task.ClientId,
                            mode = task.Mode,
                            peak_pos_m = peak.pos_m,
                            peak_db = peak.db,
                            peak.id,
                            peak.sn
                        };

                        Log.Information("FLA auto-peak done for task {TaskId}: pos={Pos}m db={Db}dB", task.TaskId, peak.pos_m, peak.db);
                    }
                    else
                    {
                        throw new Exception($"unknown mode {task.Mode}");
                    }

                    // 成功返回（status = complete）
                    var result = new ResultMessage("result", task.TaskId, status: "complete", success: true, data: data, error: null);
                    await _server.SendResultAsync(task, result, ct);
                    Log.Information("Task success {TaskId}", task.TaskId);
                    DailyResultStore.Instance.AddOrUpdate(task.TaskId, result);
                    // 标记完成（脱离 running）
                    RunningTaskTracker.Instance.MarkFinished(task.TaskId);
                }
                catch (Exception ex)
                {
                    // 最终失败返回（status = complete, success=false）
                    var failResult = new ResultMessage("result", task.TaskId, status: "complete", success: false, data: null, error: ex.Message);
                    await _server.SendResultAsync(task, failResult, ct);
                    DailyResultStore.Instance.AddOrUpdate(task.TaskId, failResult);
                    // 标记完成（即使失败也不在 running）
                    RunningTaskTracker.Instance.MarkFinished(task.TaskId);
                    Log.Error(ex, "Task failed {TaskId}", task.TaskId);
                }
                finally
                {
                    // 无论成功失败都断开 FLA（避免连接泄露）
                    try { await _fla.DisconnectAsync(); } catch { }
                    Log.Debug("FLA disconnected for task {TaskId}", task.TaskId);
                    _deviceLock.Release();
                }
            }

            Log.Information("Worker stopped");
        }

        /// <summary>
        /// 重试 策略助手（无返回值）
        /// </summary>
        /// <param name="action"></param>
        /// <param name="opName"></param>
        /// <param name="maxAttempts"></param>
        /// <param name="baseDelay"></param>
        /// <param name="ct"></param>
        /// <param name="log"></param>
        /// <param name="failDetail"></param>
        /// <returns></returns>
        private static async Task RetryAsync(
            Func<Task> action,
            string opName,
            int maxAttempts,
            TimeSpan baseDelay,
            CancellationToken ct,
            ILogger log,
            string? failDetail = null)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    await action();
                    return; // 成功
                }
                catch (OperationCanceledException)
                {
                    throw; // 尊重取消，不重试
                }
                catch (Exception ex)
                {
                    if (attempt >= maxAttempts)
                    {
                        // 最终失败
                        log.Error(ex, "[Retry:{Op}] failed after {Attempts} attempts{Detail}",
                                  opName, attempt,
                                  string.IsNullOrEmpty(failDetail) ? "" : $" ({failDetail})");
                        throw;
                    }

                    // 记录重试警告
                    log.Warning(ex, "[Retry:{Op}] attempt {Attempt}/{Max} failed{Detail}",
                                opName, attempt, maxAttempts,
                                string.IsNullOrEmpty(failDetail) ? "" : $" ({failDetail})");

                    // 指数退避 + 抖动
                    var delayMs = (int)(baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                    delayMs += _jitter.Next(0, Math.Max(20, delayMs / 4)); // jitter
                    var delay = TimeSpan.FromMilliseconds(delayMs);

                    await Task.Delay(delay, ct);
                }
            }
        }

        /// <summary>
        /// 重试策略助手（有返回值）
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="action"></param>
        /// <param name="opName"></param>
        /// <param name="maxAttempts"></param>
        /// <param name="baseDelay"></param>
        /// <param name="ct"></param>
        /// <param name="log"></param>
        /// <param name="failDetail"></param>
        /// <returns></returns>
        private static async Task<T> RetryAsync<T>(
            Func<Task<T>> action,
            string opName,
            int maxAttempts,
            TimeSpan baseDelay,
            CancellationToken ct,
            ILogger log,
            string? failDetail = null)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    var result = await action();
                    return result; // 成功
                }
                catch (OperationCanceledException)
                {
                    throw; // 尊重取消，不重试
                }
                catch (Exception ex)
                {
                    if (attempt >= maxAttempts)
                    {
                        log.Error(ex, "[Retry:{Op}] failed after {Attempts} attempts{Detail}",
                                  opName, attempt,
                                  string.IsNullOrEmpty(failDetail) ? "" : $" ({failDetail})");
                        throw;
                    }

                    log.Warning(ex, "[Retry:{Op}] attempt {Attempt}/{Max} failed{Detail}",
                                opName, attempt, maxAttempts,
                                string.IsNullOrEmpty(failDetail) ? "" : $" ({failDetail})");

                    var delayMs = (int)(baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                    delayMs += _jitter.Next(0, Math.Max(20, delayMs / 4)); // jitter
                    var delay = TimeSpan.FromMilliseconds(delayMs);

                    await Task.Delay(delay, ct);
                }
            }
        }
    }
}