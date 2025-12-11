public interface IInstrumentAdapter
{
    string DeviceId { get; }

    Task ConnectAsync(CancellationToken ct);

    Task<bool> SelfTestAsync(CancellationToken ct);

    Task<MeasureResult> ExecuteAsync(MeasureTask task, CancellationToken ct);

    Task DisconnectAsync();
}

public interface IInstrumentAdapterFactory
{
    IInstrumentAdapter Create(DeviceInfo info, IConfiguration cfg);
}

public class InstrumentAdapterFactory : IInstrumentAdapterFactory
{
    public IInstrumentAdapter Create(DeviceInfo info, IConfiguration cfg)
    {
        return info.AdapterType switch
        {
            "DonglongFlaAdapter" => new DonglongFlaAdapter(info),
            _ => throw new NotSupportedException($"AdapterType {info.AdapterType} not supported")
        };
    }
}