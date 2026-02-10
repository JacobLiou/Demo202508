using Microsoft.Owin.Hosting;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace MeshNetwork.Core
{
    public class Node : IDisposable
    {
        private IDisposable _webApp;
        private readonly string _baseAddress;
        private readonly List<string> _peers;
        private readonly HttpClient _client;

        public event Action<string> OnMessageReceived;

        public Node(int port, IEnumerable<string> initialPeers)
        {
            _baseAddress = $"http://localhost:{port}/";
            _peers = new List<string>(initialPeers);
            _client = new HttpClient();
            
            // Static reference for controller access (simple singleton pattern for demo)
            Current = this;
        }

        public static Node Current { get; private set; }

        public void Start()
        {
            try
            {
                _webApp = WebApp.Start<Startup>(url: _baseAddress);
                Console.WriteLine($"Node running at {_baseAddress}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start node at {_baseAddress}: {ex.Message}");
                throw;
            }
        }

        public async Task BroadcastAsync(string message)
        {
            Console.WriteLine($"Broadcasting: {message}");
            foreach (var peerUrl in _peers)
            {
                await SendToPeer(peerUrl, message);
            }
        }

        public async Task SendToTargetAsync(string targetUrl, string message)
        {
            Console.WriteLine($"Sending to {targetUrl}: {message}");
            await SendToPeer(targetUrl, message);
        }

        private async Task SendToPeer(string peerUrl, string message)
        {
            try
            {
                // Ensure peerUrl ends with /
                if (!peerUrl.EndsWith("/")) peerUrl += "/";
                
                var content = new StringContent($"\"{message}\"", System.Text.Encoding.UTF8, "application/json");
                var response = await _client.PostAsync($"{peerUrl}api/message", content);
                
                if (response.IsSuccessStatusCode)
                {
                    // Console.WriteLine($"Sent to {peerUrl} successfully.");
                }
                else
                {
                    Console.WriteLine($"Failed to send to {peerUrl}: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending to {peerUrl}: {ex.Message}");
            }
        }

        public void ReceiveMessage(string message)
        {
            OnMessageReceived?.Invoke(message);
        }

        public void Dispose()
        {
            _webApp?.Dispose();
            _client?.Dispose();
        }
    }
}
