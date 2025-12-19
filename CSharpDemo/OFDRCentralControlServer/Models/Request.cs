namespace OFDRCentralControlServer.Models
{
    public class Request
    {
        public virtual string? Command { get; set; }
    }

    public class SubmitRequest : Request
    {
        public override string? Command { get; set; } = "submit";

        public int ClientId { get; set; }

        public string? Mode { get; set; }

        public Dictionary<string, string>? Params { get; set; }
    }

    public class ResultRequest : Request
    {
        public override string? Command { get; set; } = "result";

        public string? TaskId { get; set; }
    }
}