using FlaQueueServer.Core;

namespace FlaQueueServer.Models
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