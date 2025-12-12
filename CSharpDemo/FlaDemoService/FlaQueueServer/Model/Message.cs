public record AckMessage(string op, string taskId);

public record StatusMessage(string op, string taskId, string status, string? error = null);

public record ResultMessage(string op, string taskId, bool success, object? data = null, string? error = null);