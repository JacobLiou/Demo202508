using OFDRCentralControlServer.Devices;
using OFDRCentralControlServer.Models;
using Serilog;
using System.Text.Json;
using System.Threading.Channels;

namespace OFDRCentralControlServer.Core
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
                    Log.Information("Task start {TaskId} ClientId={ClientId} mode={Mode}", task.TaskId, task.ClientId, task.Mode);

                    // 标记为运行中并通知客户端
                    RunningTaskTracker.Instance.MarkRunning(task.TaskId);
                    await _server.SendResultAsync(task, new ResultMessage("result", task.TaskId, status: "running"), ct);

                    await RetryAsync(
                        async () => await _switch.ConnectAsync(),
                        "switch",
                        RETRY_SWITCH_MAX,
                        BASE_DELAY_SWITCH,
                        ct,
                        Log
                    );

                    // 切光开关 —— 带重试
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

                    var result = new ResultMessage("result", task.TaskId, status: "complete", success: false, data: null, error: null);
                    object data;
                    if (task.Mode.Equals("scan", StringComparison.OrdinalIgnoreCase))
                    {
                        double zero_length = ParseDouble(task.Params.GetValueOrDefault("zero_length", "5"), 5);

                        // SCAN —— 带重试（如果设备忙或网络瞬断）
                        var res = await RetryAsync(
                            async () => await _fla.ScanLengthAsync(zero_length, ct),
                            "scan",
                            RETRY_MEASURE_MAX,
                            BASE_DELAY_MEASURE,
                            ct,
                            Log
                        );

                        if (res.Scan_Len > 0)
                        {
                            data = new
                            {
                                task.ClientId,
                                mode = task.Mode,
                                scan_length = res.Scan_Len,
                                scan_Db = res.Scan_Db
                            };
                            result = new ResultMessage("result", task.TaskId, status: "complete", success: true, data: data, error: null);
                        }

                        Log.Debug(JsonSerializer.Serialize(result));
                        Log.Information("FLA scan done for task {TaskId}: scan_length={Res}", task.TaskId, res);
                    }
                    else if (task.Mode.Equals("zero", StringComparison.OrdinalIgnoreCase))
                    {
                        // 执行归零操作
                        var zero = await RetryAsync(
                            async () => await _fla.ZeroLengthAsync(ct),
                            "zero",
                            RETRY_MEASURE_MAX,
                            BASE_DELAY_MEASURE,
                            ct,
                            Log
                        );

                        if (zero.Zero_Len > 0)
                        {
                            data = new
                            {
                                task.ClientId,
                                mode = task.Mode,
                                zero_length = zero.Zero_Len,
                                zero_db = zero.Zero_Db
                            };
                            result = new ResultMessage("result", task.TaskId, status: "complete", success: true, data: data, error: null);
                        }

                        Log.Debug(JsonSerializer.Serialize(result));
                        Log.Information("FLA zero done for task {TaskId}", task.TaskId);
                    }
                    else
                    {
                        throw new Exception($"unknown mode {task.Mode}");
                    }

                    await _server.SendResultAsync(task, result, ct);
                    Log.Information("Task success {TaskId}", task.TaskId);
                    HourlyResultStore.Instance.AddOrUpdate(task.TaskId, result);
                    // 标记完成（脱离 running）
                    RunningTaskTracker.Instance.MarkFinished(task.TaskId);
                }
                catch (Exception ex)
                {
                    // 最终失败返回（status = complete, success=false）
                    var failResult = new ResultMessage("result", task.TaskId, status: "complete", success: false, data: null, error: ex.Message);
                    Log.Error(JsonSerializer.Serialize(failResult));
                    await _server.SendResultAsync(task, failResult, ct);
                    HourlyResultStore.Instance.AddOrUpdate(task.TaskId, failResult);
                    // 标记完成（即使失败也不在 running）
                    RunningTaskTracker.Instance.MarkFinished(task.TaskId);
                    Log.Error(ex, "Task failed {TaskId}", task.TaskId);
                }
                finally
                {
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

        private static double ParseDouble(string s, double def)
            => double.TryParse(s, out var v) ? v : def;
    }
}