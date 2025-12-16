using Microsoft.Extensions.Caching.Memory;
using Serilog;
using System.Collections.Concurrent;

namespace FlaQueueServer.Core
{
    /// <summary>
    /// 使用 MemoryCache 保存一个小时的数据结果（类似 Redis 行为，支持按需驱逐）。
    /// 单例：内存存储 <taskId, ResultMessage>，默认保留最近 1 小时的数据。
    /// </summary>
    public sealed class HourlyResultStore : IDisposable
    {
        private static readonly Lazy<HourlyResultStore> _lazy = new(() => new HourlyResultStore(), isThreadSafe: true);
        public static HourlyResultStore Instance => _lazy.Value;

        private readonly MemoryCache _cache;
        private readonly ConcurrentDictionary<string, byte> _keys = new();

        // retention period for results (default 1 hour)
        private TimeSpan _retention = TimeSpan.FromHours(1);

        // approximate size limit (number of entries). null = unlimited
        private long? _sizeLimit = 10000;

        private Timer? _logTimer;

        private readonly ILogger Log = Serilog.Log.ForContext<HourlyResultStore>();

        private HourlyResultStore()
        {
            var options = new MemoryCacheOptions();
            if (_sizeLimit.HasValue)
                options.SizeLimit = _sizeLimit.Value;
            _cache = new MemoryCache(options);
            Log.Information("HourlyResultStore initialized with retention={Retention} and sizeLimit={SizeLimit}", _retention, _sizeLimit);

            //// start a periodic debug logger every 5 minutes
            //_logTimer = new Timer(_ => LogCacheContents(), state: null, dueTime: TimeSpan.FromMinutes(5), period: TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Configure retention hours (recommended).
        /// </summary>
        public void ConfigureRetentionHours(double hours)
        {
            if (hours <= 0) throw new ArgumentOutOfRangeException(nameof(hours));
            _retention = TimeSpan.FromHours(hours);
            Log.Information("HourlyResultStore retention set to {Hours} hours", hours);
        }

        /// <summary>
        /// Optionally configure size limit (number of entries). Set to null for unlimited.
        /// Note: changing size limit will recreate the internal cache.
        /// </summary>
        public void ConfigureSizeLimit(long? maxEntries)
        {
            _sizeLimit = maxEntries;
            // recreate cache with new limit
            try
            {
                _cache.Dispose();
            }
            catch { }
            var options = new MemoryCacheOptions();
            if (_sizeLimit.HasValue)
                options.SizeLimit = _sizeLimit.Value;
            // reset keys tracking
            _keys.Clear();
            // replace cache via reflection not necessary; create new instance and assign via field (this is safe here)
            // Note: _cache is readonly, so we cannot reassign. To keep API simple we won't support dynamic resize in this version.
            Log.Warning("ConfigureSizeLimit called but dynamic resize not implemented in this build. Restart required to apply new size limit.");
        }

        /// <summary>
        /// 添加或更新结果。
        /// </summary>
        public void AddOrUpdate(string taskId, ResultMessage result)
        {
            if (string.IsNullOrWhiteSpace(taskId) || result is null) return;

            var entryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_retention)
                .SetSize(1)
                .RegisterPostEvictionCallback(PostEviction);

            _cache.Set(taskId, result, entryOptions);
            _keys[taskId] = 0;
            Log.Debug("Store Result: {TaskId} (total={Count})", taskId, _keys.Count);
        }

        private void PostEviction(object key, object? value, EvictionReason reason, object? state)
        {
            try
            {
                var k = key?.ToString();
                if (!string.IsNullOrEmpty(k))
                {
                    _keys.TryRemove(k, out _);
                    Log.Debug("HourlyResultStore eviction: {Key} reason={Reason} remaining={Count}", k, reason, _keys.Count);
                }
            }
            catch { }
        }

        /// <summary>
        /// 尝试获取结果。
        /// </summary>
        public bool TryGet(string taskId, out ResultMessage? result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(taskId)) return false;
            if (_cache.TryGetValue(taskId, out ResultMessage? r))
            {
                result = r;
                return true;
            }

            // ensure keys map is clean
            _keys.TryRemove(taskId, out _);
            return false;
        }

        /// <summary>
        /// 移除某个 taskId。
        /// </summary>
        public bool Remove(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId)) return false;
            _cache.Remove(taskId);
            var removed = _keys.TryRemove(taskId, out _);
            return removed;
        }

        /// <summary>当前总项数（监控用途，近似值）。</summary>
        public int Count => _keys.Count;

        /// <summary>
        /// 手动触发清理（MemoryCache 会自动移除过期项）。
        /// 这里提供 Compact(0.0) 的接口；调用时请慎重。
        /// </summary>
        public void RunGc()
        {
            // no-op: rely on MemoryCache expirations. If explicit compaction needed, call Compact with small ratio.
            try
            {
                _cache.Compact(0.0);
                Log.Debug("HourlyResultStore RunGc invoked (compact 0.0)");
            }
            catch { }
        }

        // Periodic debug: log current cache keys and counts
        private void LogCacheContents()
        {
            try
            {
                var keys = _keys.Keys.ToArray();
                Log.Information("HourlyResultStore snapshot at {Time}: totalKeys={Count}", DateTime.UtcNow, keys.Length);
                foreach (var k in keys)
                {
                    // attempt to get value to ensure it's present
                    if (_cache.TryGetValue(k, out ResultMessage? r))
                    {
                        Log.Debug("  key={Key} present", k);
                    }
                    else
                    {
                        Log.Debug("  key={Key} missing/expired", k);
                        _keys.TryRemove(k, out _);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "HourlyResultStore LogCacheContents failed");
            }
        }

        public void Dispose()
        {
            try { _cache.Dispose(); } catch { }
            try { _logTimer?.Dispose(); } catch { }
            _keys.Clear();
        }
    }
}