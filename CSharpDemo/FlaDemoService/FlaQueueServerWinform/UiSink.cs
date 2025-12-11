using Serilog.Core;
using Serilog.Events;

namespace OTMS
{
    // A simple Serilog sink that forwards rendered log lines to a UI callback
    public class UiSink : ILogEventSink
    {
        private readonly Action<string> _append;
        private readonly IFormatProvider _fmt = null;

        public UiSink(Action<string> append)
        { _append = append; }

        public void Emit(LogEvent logEvent)
        {
            var rendered = logEvent.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff ") + logEvent.Level.ToString().PadRight(5) + " " + logEvent.RenderMessage();
            _append(rendered);
        }
    }
}