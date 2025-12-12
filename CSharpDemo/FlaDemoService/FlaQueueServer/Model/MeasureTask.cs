using FlaQueueServer.Core;

namespace FlaQueueServer.Model
{
    public record MeasureTask(
        string TaskId,
        ClientSession Session,
        int Channel,
        string Mode,
        Dictionary<string, string> Params,
        DateTime CreatedAt
    );
}