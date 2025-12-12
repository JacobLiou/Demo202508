namespace FlaQueueServer.Models
{
    public record SubmitRequest(
        string? Command,
        int Channel,
        string Mode,
        Dictionary<string, string>? Params
    );
}