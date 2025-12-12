namespace FlaQueueServer.Models
{
    public record SubmitRequest(
        string? Op,
        int Channel,
        string Mode,
        Dictionary<string, string>? Params
    );
}