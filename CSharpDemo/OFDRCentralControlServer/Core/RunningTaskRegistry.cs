using System.Collections.Concurrent;

namespace OFDRCentralControlServer.Core
{
    /// <summary>
    /// Track currently running task IDs (in-memory, thread-safe).
    /// </summary>
    public static class RunningTaskRegistry
    {
        private static readonly ConcurrentDictionary<string, DateTime> _running = new();

        public static void MarkRunning(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId)) return;
            _running[taskId] = DateTime.UtcNow;
        }

        public static void Clear(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId)) return;
            _running.TryRemove(taskId, out _);
        }

        public static bool IsRunning(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId)) return false;
            return _running.ContainsKey(taskId);
        }

        public static IReadOnlyCollection<string> ListRunning() => _running.Keys.ToArray();
    }
}