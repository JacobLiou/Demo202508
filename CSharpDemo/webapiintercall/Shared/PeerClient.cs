using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    /// <summary>
    /// 向对等节点发送消息的通用客户端（HttpClient）。
    /// </summary>
    public class PeerClient
    {
        private readonly HttpClient _client;
        private readonly string _myName;
        private readonly IReadOnlyList<string> _peerBaseUrls;

        public PeerClient(string myName, IReadOnlyList<string> peerBaseUrls)
        {
            _client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            _myName = myName;
            _peerBaseUrls = peerBaseUrls ?? new List<string>();
        }

        /// <summary>
        /// 向所有对等节点广播消息（to 为空表示广播）。
        /// </summary>
        public async Task BroadcastAsync(string content, string to = null)
        {
            var msg = new MessageDto(_myName, to ?? "*", content);
            var json = JsonHelper.Serialize(msg);
            var tasks = new List<Task>();
            foreach (var baseUrl in _peerBaseUrls)
            {
                var url = baseUrl.TrimEnd('/') + "/api/message";
                tasks.Add(SendAsync(url, json));
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <summary>
        /// 向指定节点发送（baseUrl 为对方根地址，如 http://localhost:5002）。
        /// </summary>
        public async Task SendToAsync(string peerBaseUrl, MessageDto msg)
        {
            var url = peerBaseUrl.TrimEnd('/') + "/api/message";
            await SendAsync(url, JsonHelper.Serialize(msg)).ConfigureAwait(false);
        }

        private async Task SendAsync(string url, string json)
        {
            try
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var resp = await _client.PostAsync(url, content).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                    Console.WriteLine("[PeerClient] " + url + " -> " + resp.StatusCode);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[PeerClient] " + url + " error: " + ex.Message);
            }
        }
    }
}
