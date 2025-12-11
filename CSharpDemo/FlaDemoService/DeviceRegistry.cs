public class DeviceInfo
{
    public string DeviceId { get; init; } = default!;
    public string Vendor { get; init; } = default!;
    public string Model { get; init; } = default!;
    public string Host { get; init; } = default!;
    public int Port { get; init; } = 4300;
    public string AdapterType { get; init; } = "DonglongFlaAdapter";
}

public interface IDeviceRegistry
{
    DeviceInfo? Get(string deviceId);

    IEnumerable<DeviceInfo> List();
}

public class DeviceRegistry : IDeviceRegistry
{
    private readonly Dictionary<string, DeviceInfo> _devices;

    public DeviceRegistry(IConfiguration cfg)
    {
        var section = cfg.GetSection("Devices");
        _devices = section.Get<Dictionary<string, DeviceInfo>>() ?? new();
    }

    public DeviceInfo? Get(string deviceId) => _devices.TryGetValue(deviceId, out var d) ? d : null;

    public IEnumerable<DeviceInfo> List() => _devices.Values;
}