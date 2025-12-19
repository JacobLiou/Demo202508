namespace OFDRCentralControlServer.Models
{
    public record SwitchStatus(
        int SwitchIndex,
        SwitchState? Actual,   // POS
        SwitchState? Setting,  // SPOS
        long? Count            // CNT
    );

    public record SwitchRoute(int SwitchIndex, int Input, int Output);

    public record SwitchState(int Input, int Output);
}