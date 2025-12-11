namespace OTMS
{
    public record Settings(int ListenPort, string FlaHost, int FlaPort, string SwitchCom, int SwitchBaud, int SwitchIndex, int SwitchInput)
    {
        public static Settings Default() => new Settings(5600, "192.168.1.1", 4300, "COM3", 115200, 1, 1);
    }
}