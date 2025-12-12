using Serilog;
using System.Collections.Concurrent;
using System.Globalization;

namespace FlaQueueServer.Core
{
    /// <summary>
    /// 单例：按天存储 <taskId, ResultMessage>，只保留当天（或最近 N 天）数据。
    /// 线程安全，支持定期清理。
    /// </summary>
    public sealed class DailyResultStore : IDisposable
    {
        private static readonly Lazy<DailyResultStore> _lazy =
            new(() => new DailyResultStore(), isThreadSafe: true);

        public static DailyResultStore Instance => _lazy.Value;

        private readonly ConcurrentDictionary<DateOnly, ConcurrentDictionary<string, ResultMessage>> _buckets
            = new();

        private Timer? _gcTimer;

        private TimeSpan _gcInterval = TimeSpan.FromMinutes(10);

        private int _retentionDays = 1;

        private readonly object _gcLock = new();

        private readonly ILogger Log = Serilog.Log.ForContext<DailyResultStore>();

        private DailyResultStore()
        {
            // 默认启动定时清理（首轮 1 分钟后）
            _gcTimer = new Timer(_ => TryGc(), state: null,
                                 dueTime: TimeSpan.FromMinutes(1),
                                 period: _gcInterval);
        }

        /// <summary>
        /// 运行时配置清理周期和保留天数。若已启动定时器会更新周期。
        /// </summary>
        public void Configure(TimeSpan? gcInterval = null, int? retentionDays = null)
        {
            if (gcInterval is { } gi && gi > TimeSpan.Zero)
            {
                _gcInterval = gi;
                try
                {
                    _gcTimer?.Change(dueTime: _gcInterval, period: _gcInterval);
                }
                catch { /* ignore */ }
            }

            if (retentionDays is { } rd && rd >= 1)
            {
                _retentionDays = rd;
            }
        }

        /// <summary>
        /// 添加或更新结果。
        /// </summary>
        public void AddOrUpdate(string taskId, ResultMessage result)
        {
            if (!TryParseDateFromTaskId(taskId, out var date))
                date = DateOnly.FromDateTime(DateTime.UtcNow);

            var bucket = _buckets.GetOrAdd(date, _ => new ConcurrentDictionary<string, ResultMessage>());
            bucket[taskId] = result;
            Log.Information("Store Result: " + result);
        }

        /// <summary>
        /// 尝试获取结果。
        /// </summary>
        public bool TryGet(string taskId, out ResultMessage? result)
        {
            result = null;
            if (!TryParseDateFromTaskId(taskId, out var date))
            {
                // 未能解析日期：遍历所有桶（数量有限，通常为当天/近 N 天）
                foreach (var kv in _buckets)
                {
                    if (kv.Value.TryGetValue(taskId, out var r))
                    {
                        result = r;
                        return true;
                    }
                }

                return false;
            }

            return _buckets.TryGetValue(date, out var bucket)
                && bucket.TryGetValue(taskId, out result);
        }

        /// <summary>
        /// 移除某个 taskId。
        /// </summary>
        public bool Remove(string taskId)
        {
            if (!TryParseDateFromTaskId(taskId, out var date))
            {
                foreach (var kv in _buckets)
                {
                    if (kv.Value.TryRemove(taskId, out _))
                        return true;
                }
                return false;
            }

            return _buckets.TryGetValue(date, out var bucket)
                && bucket.TryRemove(taskId, out _);
        }

        /// <summary>当前总项数（监控用途）。</summary>
        public int Count
        {
            get
            {
                int total = 0;
                foreach (var bucket in _buckets.Values)
                    total += bucket.Count;
                return total;
            }
        }

        /// <summary>手
        /// 动触发清理。
        /// </summary>
        public void RunGc() => TryGc(force: true);

        private void TryGc(bool force = false)
        {
            if (!Monitor.TryEnter(_gcLock))
                return;

            try
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var minDateKept = today.AddDays(-(_retentionDays - 1));

                foreach (var kv in _buckets)
                {
                    var date = kv.Key;
                    if (date < minDateKept)
                    {
                        _buckets.TryRemove(date, out _);
                    }
                }
            }
            finally
            {
                Monitor.Exit(_gcLock);
            }
        }

        /// <summary>
        /// 从 taskId 解析日期；示例格式：TyyyyMMddHHmmssfff-xxxxxxxx
        /// </summary>
        private static bool TryParseDateFromTaskId(string taskId, out DateOnly date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(taskId)) return false;

            try
            {
                int start = taskId.StartsWith("T") ? 1 : 0;
                if (taskId.Length < start + 8) return false;     // 至少有 yyyyMMdd

                var ymd = taskId.Substring(start, 8);
                if (DateTime.TryParseExact(ymd, "yyyyMMdd",
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
                {
                    date = DateOnly.FromDateTime(dt);
                    return true;
                }
            }
            catch { /* ignore */ }

            return false;
        }

        public void Dispose()
        {
            try { _gcTimer?.Dispose(); } catch { }
            _buckets.Clear();
        }
    }
}