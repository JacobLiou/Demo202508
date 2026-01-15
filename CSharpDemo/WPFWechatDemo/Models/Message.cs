using System;

namespace WPFWechatDemo.Models
{
    public class Message
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsSent { get; set; } // true表示发送的消息，false表示接收的消息
        public string ContactId { get; set; } = string.Empty;
    }
}


