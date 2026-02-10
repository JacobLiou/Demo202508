using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Shared
{
    /// <summary>
    /// 使用内置 DataContractJsonSerializer，无额外依赖。
    /// </summary>
    public static class JsonHelper
    {
        private static readonly DataContractJsonSerializer Serializer = new DataContractJsonSerializer(typeof(MessageDto));

        public static string Serialize(MessageDto msg)
        {
            using (var ms = new MemoryStream())
            {
                Serializer.WriteObject(ms, msg);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        public static MessageDto Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (MessageDto)Serializer.ReadObject(ms);
            }
        }
    }
}
