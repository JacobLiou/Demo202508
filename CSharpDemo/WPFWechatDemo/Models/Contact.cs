namespace WPFWechatDemo.Models
{
    public class Contact
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public string LastMessage { get; set; } = string.Empty;
        public string LastMessageTime { get; set; } = string.Empty;
        public int UnreadCount { get; set; } = 0;
    }
}


