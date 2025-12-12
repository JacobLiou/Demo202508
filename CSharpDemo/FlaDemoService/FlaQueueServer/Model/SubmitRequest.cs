namespace FlaQueueServer.Model
{
    public record SubmitRequest(
        string? Op,
        int Channel,
        string Mode,
        Dictionary<string, string>? Params
    );
}