using OFDRCentralControlServer;

namespace OFDRCentralControlServer.Interfaces
{
    public interface IFlaDeviceCommunicator
    {
        Task SetConfigAsync(FlaConfig config, CancellationToken ct = default);

        Task SetResolutionAsync(string mode, CancellationToken ct = default);

        Task SetGainAsync(string gainUi, CancellationToken ct = default);

        Task SetWindowAsync(string windowRaw5, CancellationToken ct = default);

        Task SetCenterAsync(string centerRaw5, CancellationToken ct = default);

        Task<ResultMessage> ScanAsync(CancellationToken ct = default);

        Task<ResultMessage> AutoPeakAsync(
            string start, string end, string count, string algo,
            string width, string thr, string id, string sn,
            CancellationToken ct = default);
    }
}