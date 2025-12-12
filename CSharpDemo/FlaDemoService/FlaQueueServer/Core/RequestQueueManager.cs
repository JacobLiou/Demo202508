using FlaQueueServer.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FlaQueueServer.Core
{
    public class RequestQueueManager
    {
        private readonly ConcurrentQueue<ClientRequest> _requestQueue = new();

        private readonly IOpticalSwitchController _opticalSwitch;
        private readonly IFlaDeviceCommunicator _flaCommunicator;

        private volatile bool _isProcessing = false; 

        public RequestQueueManager(
            IOpticalSwitchController opticalSwitch,
            IFlaDeviceCommunicator flaCommunicator)
        {
            _opticalSwitch = opticalSwitch;
            _flaCommunicator = flaCommunicator;
            Task.Run(ProcessQueueLoop); // 启动后台处理循环
        }

        public void Enqueue(ClientRequest request)
        {
            _requestQueue.Enqueue(request);
            Console.WriteLine($"请求队列新增任务. 队列长度: {_requestQueue.Count}");
        }

        private async Task ProcessQueueLoop()
        {
            while (true) // 保持后台任务运行
            {
                if (_requestQueue.TryDequeue(out var request))
                {
                    if (_isProcessing) // 确保一次只处理一个请求
                    {
                        // 如果正在处理，将请求放回队列头部（这可能导致饥饿，但简单有效）
                        _requestQueue.Enqueue(request);
                        await Task.Delay(100); // 短暂等待再尝试
                        continue;
                    }

                    _isProcessing = true;
                    Console.WriteLine($"开始处理客户端 {request.ClientId} 的请求...");
                    await ProcessSingleRequest(request);
                    _isProcessing = false;
                }
                else
                {
                    await Task.Delay(50); // 队列为空时短暂休眠
                }
            }
        }

        private async Task ProcessSingleRequest(ClientRequest request)
        {
            try
            {
                // 1. 设置光通道
                int channel = GetChannelForClient(request.ClientId);
                await _opticalSwitch.SetChannelAsync(channel);

                // 2. 执行扫描命令并与FLA通信
                string result = await _flaCommunicator.ExecuteScanCommandAsync(request.ScanCommand);

                // 3. 发送结果回客户端
                if (request.Client.Connected)
                {
                    using (var stream = request.Client.GetStream())
                    {
                        await SendResponse(stream, result);
                    }
                }
                else
                {
                    Console.WriteLine($"客户端 {request.ClientId} 已断开，无法发送结果。");
                }

                request.Status = RequestStatus.Completed;
                Console.WriteLine($"客户端 {request.ClientId} 的请求处理完成。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理客户端 {request.ClientId} 请求时出错: {ex.Message}");
                request.Status = RequestStatus.Failed;
                if (request.Client.Connected)
                {
                    using (var stream = request.Client.GetStream())
                    {
                        await SendResponse(stream, $"ERROR: {ex.Message}");
                    }
                }
            }
        }

        private static async Task SendResponse(NetworkStream stream, string response)
        {
            byte[] data = Encoding.UTF8.GetBytes(response + "\n");
            await stream.WriteAsync(data, 0, data.Length);
            Console.WriteLine($"已向客户端发送响应: {response}");
        }

        // --- 简化的通道分配逻辑 ---
        private static int GetChannelForClient(string clientId)
        {
            int hash = clientId.GetHashCode();
            return Math.Abs(hash % 16) + 1; // 1-16
        }
    }
}
