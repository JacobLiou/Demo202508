public record AckMessage(string command, string taskId);

public record StatusMessage(string command, string taskId, string status, string? error = null);

public record ResultMessage(string command, string taskId, bool success, object? data = null, string? error = null);