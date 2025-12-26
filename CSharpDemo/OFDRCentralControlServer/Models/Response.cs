public record AckMessage(string command, string taskId, int clientId, string? mode = null);

public record ResultMessage(string command, string taskId, string? status = null, object? data = null, string? error = null);