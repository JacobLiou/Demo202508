using Serilog;
using System.Collections.Concurrent;
using System.Linq;

namespace FlaQueueServer.Core
{
    /// <summary>
    /// 模仿Redis保存一个小时的数据结果。
    /// 单例：内存存储 <taskId, ResultMessage>，仅保留最近一段时间的数据（默认 1 小时）。
    /// 线程安全，支持定期清理。
    /// </summary>
    public sealed class HourlyResultStore : IDisposable
    {
        private static readonly Lazy<HourlyResultStore> _lazy =
            new(() => new HourlyResultStore(), isThreadSafe: true);

        public static HourlyResultStore Instance => _lazy.Value;

        // store: taskId -> (result, timestamp)
        private readonly ConcurrentDictionary<string, (ResultMessage Result, DateTime Timestamp)> _store = new();

        private Timer? _gcTimer;

        private TimeSpan _gcInterval = TimeSpan.FromMinutes(10);

        // retention period for results (default 1 hour)
        private TimeSpan _retention = TimeSpan.FromHours(1);

        private readonly object _gcLock = new();

        private readonly ILogger Log = Serilog.Log.ForContext<HourlyResultStore>();

        private HourlyResultStore()
        {
            // 默认启动定时清理（首轮 1 分钟后）
            _gcTimer = new Timer(_ => TryGc(), state: null,
                                 dueTime: TimeSpan.FromMinutes(1),
                                 period: _gcInterval);
        }

        /// <summary>
        /// 运行时配置清理周期（保留时长请使用 ConfigureRetentionHours）
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

            if (retentionDays is { } rd && rd >= 0)
            {
                // 兼容旧接口：如果传入天数则按天计算
                _retention = TimeSpan.FromDays(rd);
            }
        }

        /// <summary>
        /// 以小时为单位配置保留时长（推荐使用此方法）。
        /// </summary>
        public void ConfigureRetentionHours(double hours)
        {
            if (hours <= 0) throw new ArgumentOutOfRangeException(nameof(hours));
            _retention = TimeSpan.FromHours(hours);
            Log.Information("HourlyResultStore retention set to {Hours} hours", hours);
        }

        /// <summary>
        /// 添加或更新结果。
        /// </summary>
        public void AddOrUpdate(string taskId, ResultMessage result)
        {
            if (string.IsNullOrWhiteSpace(taskId) || result is null) return;
            _store[taskId] = (result, DateTime.UtcNow);
            Log.Debug("Store Result: {TaskId} (total={Count})", taskId, _store.Count);
        }

        /// <summary>
        /// 尝试获取结果。
        /// </summary>
        public bool TryGet(string taskId, out ResultMessage? result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(taskId)) return false;

            if (_store.TryGetValue(taskId, out var entry))
            {
                if (DateTime.UtcNow - entry.Timestamp <= _retention)
                {
                    result = entry.Result;
                    return true;
                }

                // expired -> remove
                _store.TryRemove(taskId, out _);
            }

            return false;
        }

        /// <summary>
        /// 移除某个 taskId。
        /// </summary>
        public bool Remove(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId)) return false;
            return _store.TryRemove(taskId, out _);
        }

        /// <summary>当前总项数（监控用途）。</summary>
        public int Count => _store.Count;

        /// <summary>
        /// 手动触发清理。
        /// </summary>
        public void RunGc() => TryGc(force: true);

        private void TryGc(bool force = false)
        {
            if (!Monitor.TryEnter(_gcLock))
                return;

            try
            {
                var cutoff = DateTime.UtcNow - _retention;
                // collect expired keys first to avoid modifying dictionary during enumeration
                var expired = _store.Where(kv => kv.Value.Timestamp < cutoff).Select(kv => kv.Key).ToArray();
                if (expired.Length == 0) return;

                foreach (var k in expired)
                {
                    _store.TryRemove(k, out _);
                }

                Log.Information("HourlyResultStore GC removed {Count} entries, remaining={Remaining}", expired.Length, _store.Count);
            }
            finally
            {
                Monitor.Exit(_gcLock);
            }
        }

        public void Dispose()
        {
            try { _gcTimer?.Dispose(); } catch { }
            _store.Clear();
        }
    }
}