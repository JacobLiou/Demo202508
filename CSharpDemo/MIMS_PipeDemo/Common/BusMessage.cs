namespace MIMS.Common
{
    public class BusMessage
    {
        public string Type { get; set; }            // Register, Ping, Pong, Data, Reply, ACK
        public string From { get; set; }
        public string To { get; set; }
        public string CorrelationId { get; set; }
        public string Payload { get; set; }
    }
}