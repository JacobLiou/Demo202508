using System.Text.Json;

namespace FlaQueueServer
{
    public class FlaConfig
    {
        public string RunMode { get; set; } = "real"; // real | mock
        public int ListenPort { get; set; } = 5600;
        public string FlaHost { get; set; } = "192.168.1.1";
        public int FlaPort { get; set; } = 4300;
        public string SwitchCom { get; set; } = "COM3";
        public int SwitchBaud { get; set; } = 115200;
        public int SwitchIndex { get; set; } = 1;
        public int SwitchInput { get; set; } = 1;
        public int LogRetainedDays { get; set; } = 14;
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