public enum MeasureType
{ Otdr, OpticalPower, Wavelength, Osnr, Bert }

public record MeasureTask(
    string TaskId,
    string DeviceId,
    MeasureType Type,
    Dictionary<string, string> Params,
    DateTime CreatedAt
);

public enum TaskStatus
{ Queued, Running, Success, Failed }

public record MeasureResult(
    string TaskId,
    string DeviceId,
    MeasureType Type,
    DateTime Timestamp,
    bool Success,
    string? Error,
    Dictionary<string, double> Scalars,
    string? TraceJson,
    Dictionary<string, string>? Metadata
);

public record SubmitRequest(
    string DeviceId,
    MeasureType Type,
    Dictionary<string, string> Params
);

public record SubmitResponse(string TaskId);

public record StatusResponse(string TaskId, TaskStatus Status, string? Error);

public record ResultResponse(string TaskId, MeasureResult? Result, string? Error);

public static class ParamDefaults
{
    public static Dictionary<string, string> Normalize(Dictionary<string, string>? p)
    {
        var d = p is null ? new Dictionary<string, string>() : new Dictionary<string, string>(p);
        // 默认参数（仅在未提供时）
        if (!d.ContainsKey("op_mode")) d["op_mode"] = "scan"; // scan 或 auto_peak
        if (!d.ContainsKey("sr_mode")) d["sr_mode"] = "0";    // SR_0
        if (!d.ContainsKey("gain")) d["gain"] = "1";          // G_1
        if (!d.ContainsKey("wr_len")) d["wr_len"] = "10.00";  // 5字符
        if (!d.ContainsKey("x_center")) d["x_center"] = "000.0"; // 5字符
        return d;
    }
}