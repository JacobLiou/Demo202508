using System.Text.Json;

namespace OFDRCentralControlServer
{
    public class ServerConfig
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
    }

    public static class ConfigLoader
    {
        private const string FileName = "ServerConfig.json";

        public static ServerConfig Load()
        {
            try
            {
                if (!File.Exists(FileName))
                {
                    var def = new ServerConfig();
                    Save(def);
                    return def;
                }
                var json = File.ReadAllText(FileName);
                var cfg = JsonSerializer.Deserialize<ServerConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return cfg ?? new ServerConfig();
            }
            catch
            {
                return new ServerConfig();
            }
        }

        public static void Save(ServerConfig cfg)
        {
            var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FileName, json);
        }
    }
}