using System.Collections.Concurrent;

namespace FlaQueueServer.Core
{
    /// <summary>
    /// 简单的运行中任务跟踪器（单例）
    /// </summary>
    public sealed class RunningTaskTracker
    {
        private static readonly Lazy<RunningTaskTracker> _lazy = new(() => new RunningTaskTracker(), isThreadSafe: true);
        public static RunningTaskTracker Instance => _lazy.Value;

        private readonly ConcurrentDictionary<string, byte> _running = new();

        private RunningTaskTracker() { }

        public void MarkRunning(string taskId) => _running.TryAdd(taskId, 0);

        public void MarkFinished(string taskId) => _running.TryRemove(taskId, out _);

        public bool IsRunning(string taskId) => _running.ContainsKey(taskId);

        public int Count => _running.Count;
    }
}
