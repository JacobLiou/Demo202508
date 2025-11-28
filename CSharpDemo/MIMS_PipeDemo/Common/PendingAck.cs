using System;

namespace MIMS.Common
{
    public class PendingAck
    {
        public PendingAck(BusMessage msg, DateTime now)
        {
            this.Message = msg;
            this.Timestamp = now;
        }

        public BusMessage Message { get; set; }
        public DateTime Timestamp { get; set; }
    }
}