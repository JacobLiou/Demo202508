using System;
using System.Runtime.Serialization;

namespace Shared
{
    /// <summary>
    /// 节点间传递的消息 DTO，抽象通用。
    /// </summary>
    [DataContract]
    public class MessageDto
    {
        [DataMember(Name = "from")]
        public string From { get; set; }

        [DataMember(Name = "to")]
        public string To { get; set; }

        [DataMember(Name = "content")]
        public string Content { get; set; }

        [DataMember(Name = "time")]
        public string Time { get; set; }

        public MessageDto()
        {
            Time = DateTimeOffset.UtcNow.ToString("o");
        }

        public MessageDto(string from, string to, string content) : this()
        {
            From = from;
            To = to;
            Content = content;
        }
    }
}
