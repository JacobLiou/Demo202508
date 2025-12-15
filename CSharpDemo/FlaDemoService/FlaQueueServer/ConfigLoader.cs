using System.Text.Json;

namespace FlaQueueServer
{
    public class FlaConfig
    {
        // real | mock
        public string RunMode { get; set; } = "real";

        public int ListenPort { get; set; } = 5600;

        public string FlaHost { get; set; } = "192.168.1.1";

        public int FlaPort { get; set; } = 4300;

        public string SwitchCom { get; set; } = "COM3";

        public int SwitchBaud { get; set; } = 115200;

        public int SwitchIndex { get; set; } = 1;

        public int SwitchInput { get; set; } = 1;

        // 是否对 FLA 使用长连接（默认 false，短连接：每任务连接一次并在任务结束断开）
        public bool KeepFlaConnection { get; set; } = true;
    }

    public static class ConfigLoader
    {
        private const string FileName = "FlaConfig.json";

        public static FlaConfig Load()
        {
            try
            {
                if (!File.Exists(FileName))
                {
                    var def = new FlaConfig();
                    Save(def);
                    return def;
                }
                var json = File.ReadAllText(FileName);
                var cfg = JsonSerializer.Deserialize<FlaConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return cfg ?? new FlaConfig();
            }
            catch
            {
                return new FlaConfig();
            }
        }

        public static void Save(FlaConfig cfg)
        {
            var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FileName, json);
        }
    }
}