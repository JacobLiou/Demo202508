using FlaQueueServer;

public record MeasureTask(
    string TaskId,
    ClientSession Session,
    int Channel,
    string Mode,
    Dictionary<string, string> Params,
    DateTime CreatedAt
);

public record SubmitRequest(
    string? Op,
    int Channel,
    string Mode,
    Dictionary<string, string>? Params
);

public record AckMessage(string op, string taskId);

public record StatusMessage(string op, string taskId, string status, string? error = null);

public record ResultMessage(string op, string taskId, bool success, object? data = null, string? error = null);