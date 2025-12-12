using Serilog;

namespace FlaQueueServer.Device
{
    public static class FlaInstrumentAdapterMock
    {
        private static readonly ILogger Log = Serilog.Log.Logger;

        // 生成一个简单的峰值与长度模拟；实际可替换为真实设备通讯
        public static object Run(int channel, string mode, Dictionary<string, string> p)
        {
            var rand = new Random(unchecked(Environment.TickCount + channel));
            double peakPos = Math.Round(10 + rand.NextDouble() * 20, 3); // 10~30m
            double peakDb = Math.Round(-30 - rand.NextDouble() * 20, 3); // -30~-50dB
            double length = Math.Round(peakPos + rand.NextDouble() * 1.5, 3);

            Log.Information(mode == "scan"
                ? "Mock SCAN on channel {Channel}: length={Length}m"
                : "Mock AUTO_PEAK on channel {Channel}: peak_pos={PeakPos}m, peak_db={PeakDb}dB",
                channel, length, peakPos, peakDb);
            return new
            {
                channel,
                mode,
                peak_pos_m = peakPos,
                peak_db = peakDb,
                length_m = length
            };
        }
    }
}