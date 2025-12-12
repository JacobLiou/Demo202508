using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FlaQueueServer.Core
{
    class Qwen_FLA
    {
        // 配置参数
        private const int SERVER_PORT = 4300; // FLA设备的TCP端口
        private const string FLA_DEVICE_IP = "192.168.1.1"; // FLA设备的IP地址
        private const string SERIAL_PORT_NAME = "COM3"; // 光开关串口名称
        private const int BAUD_RATE = 9600; // 串口波特率

        // 全局状态
        private static TcpListener _server;
        private static SerialPort _serialPort;
        private static ConcurrentQueue<ClientRequest> _requestQueue = new ConcurrentQueue<ClientRequest>();
        private static bool _isProcessing = false;
        private static object _lockObject = new object();

        static async Task MainFunc(string[] args)
        {
            Console.WriteLine("=== Wavemeter Shared Server 启动中 ===");
            Console.WriteLine($"监听客户端连接: 0.0.0.0:{SERVER_PORT}");
            Console.WriteLine($"连接FLA设备: {FLA_DEVICE_IP}:{SERVER_PORT}");
            Console.WriteLine($"连接光开关: {SERIAL_PORT_NAME} @ {BAUD_RATE}");

            try
            {
                // 初始化串口（光开关）
                InitializeSerialPort();

                // 启动TCP服务器，监听客户端连接
                _server = new TcpListener(IPAddress.Any, SERVER_PORT);
                _server.Start();
                Console.WriteLine("TCP服务器已启动，等待客户端连接...");

                // 启动后台任务处理队列
                Task.Run(() => ProcessRequestQueue());

                // 主循环：接受新的客户端连接
                while (true)
                {
                    TcpClient client = await _server.AcceptTcpClientAsync();
                    HandleClient(client);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"服务器发生严重错误: {ex.Message}");
            }
            finally
            {
                Cleanup();
            }
        }

        /// <summary>
        /// 初始化串口连接光开关
        /// </summary>
        private static void InitializeSerialPort()
        {
            _serialPort = new SerialPort(SERIAL_PORT_NAME, BAUD_RATE, Parity.None, 8, StopBits.One);
            _serialPort.DataReceived += SerialPort_DataReceived;
            _serialPort.Open();
            Console.WriteLine("光开关串口已打开。");
        }

        /// <summary>
        /// 处理客户端连接
        /// </summary>
        /// <param name="client">新连接的客户端</param>
        private static void HandleClient(TcpClient client)
        {
            var request = new ClientRequest
            {
                Client = client,
                ClientId = Guid.NewGuid().ToString(),
                StartTime = DateTime.Now
            };

            // 将请求加入队列
            _requestQueue.Enqueue(request);
            Console.WriteLine($"新客户端连接: {request.ClientId}. 当前队列长度: {_requestQueue.Count}");

            // 启动一个独立的任务来处理该客户端的后续通信
            Task.Run(async () =>
            {
                try
                {
                    using (var stream = client.GetStream())
                    {
                        byte[] buffer = new byte[1024];
                        int bytesRead;

                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                            Console.WriteLine($"来自客户端 {request.ClientId} 的消息: {message}");

                            // 解析客户端指令并放入队列
                            if (message.StartsWith("SCAN_") && message.EndsWith("_NACS"))
                            {
                                // 这是一个自动寻峰指令
                                request.ScanCommand = message;
                                request.Status = RequestStatus.Queued;
                                Console.WriteLine($"客户端 {request.ClientId} 的寻峰指令已入队。");

                                // 如果当前没有正在处理的请求，则尝试立即处理
                                lock (_lockObject)
                                {
                                    if (!_isProcessing)
                                    {
                                        _isProcessing = true;
                                        // 在这里可以触发处理，但为了保持队列逻辑，我们让ProcessRequestQueue来处理
                                    }
                                }
                            }
                            else if (message == "QUIT")
                            {
                                // 客户端请求断开
                                request.Status = RequestStatus.Completed;
                                break;
                            }
                            else
                            {
                                // 发送错误响应
                                SendResponse(stream, "INPUT_ERROR: 未知指令");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"客户端 {request.ClientId} 通信异常: {ex.Message}");
                }
                finally
                {
                    client.Close();
                    Console.WriteLine($"客户端 {request.ClientId} 已断开连接。");
                }
            });
        }

        /// <summary>
        /// 后台任务：持续处理请求队列
        /// </summary>
        private static async Task ProcessRequestQueue()
        {
            while (true)
            {
                if (_requestQueue.TryDequeue(out ClientRequest request))
                {
                    Console.WriteLine($"开始处理客户端 {request.ClientId} 的请求...");

                    try
                    {
                        // 1. 设置光通道 (根据客户端ID或其他标识映射到通道号)
                        int channel = GetChannelForClient(request.ClientId);
                        SetOpticalSwitchChannel(channel);

                        // 2. 连接到FLA设备
                        using (TcpClient flaClient = new TcpClient())
                        {
                            await flaClient.ConnectAsync(FLA_DEVICE_IP, SERVER_PORT);
                            using (NetworkStream stream = flaClient.GetStream())
                            {
                                // 等待连接成功确认 (OCI)
                                byte[] responseBuffer = new byte[1024];
                                int bytes = await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);
                                string response = Encoding.UTF8.GetString(responseBuffer, 0, bytes).Trim();
                                if (response != "OCI")
                                {
                                    throw new Exception($"FLA设备连接失败，未收到OCI: {response}");
                                }

                                // 3. 发送扫描指令
                                await SendCommand(stream, request.ScanCommand);

                                // 4. 接收并解析结果
                                string result = await ReceiveResult(stream);

                                // 5. 将结果发送回客户端
                                if (request.Client.Connected)
                                {
                                    using (var clientStream = request.Client.GetStream())
                                    {
                                        await SendResponse(clientStream, result);
                                    }
                                }

                                Console.WriteLine($"客户端 {request.ClientId} 的请求处理完成。");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"处理客户端 {request.ClientId} 请求时出错: {ex.Message}");
                        // 向客户端发送错误信息
                        if (request.Client.Connected)
                        {
                            using (var clientStream = request.Client.GetStream())
                            {
                                await SendResponse(clientStream, $"ERROR: {ex.Message}");
                            }
                        }
                    }
                    finally
                    {
                        // 标记为已完成
                        request.Status = RequestStatus.Completed;
                        // 释放资源或重置状态
                        lock (_lockObject)
                        {
                            _isProcessing = false;
                        }
                    }
                }
                else
                {
                    // 队列为空，短暂休眠避免CPU空转
                    await Task.Delay(100);
                }
            }
        }

        /// <summary>
        /// 根据客户端ID获取对应的光开关通道号
        /// </summary>
        /// <param name="clientId">客户端唯一标识</param>
        /// <returns>通道号 (1-16)</returns>
        private static int GetChannelForClient(string clientId)
        {
            // 这里是简化实现，实际应用中应根据业务规则映射
            // 例如，可以根据clientId的哈希值或预设配置表进行映射
            int hash = clientId.GetHashCode();
            return Math.Abs(hash % 16) + 1; // 返回1-16之间的通道号
        }

        /// <summary>
        /// 设置光开关到指定通道
        /// </summary>
        /// <param name="channel">通道号 (1-16)</param>
        private static void SetOpticalSwitchChannel(int channel)
        {
            if (channel < 1 || channel > 16)
                throw new ArgumentOutOfRangeException(nameof(channel), "通道号必须在1-16之间。");

            string command = $"CH{channel}\r\n"; // 假设光开关命令格式为 CHn\r\n
            _serialPort.Write(command);
            Console.WriteLine($"已设置光开关至通道 {channel}。");

            // 可选：等待光开关完成切换
            Thread.Sleep(500);
        }

        /// <summary>
        /// 串口数据接收事件处理
        /// </summary>
        private static void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data = _serialPort.ReadExisting();
                Console.WriteLine($"光开关返回: {data}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取光开关数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 向网络流发送命令
        /// </summary>
        /// <param name="stream">网络流</param>
        /// <param name="command">要发送的命令</param>
        private static async Task SendCommand(NetworkStream stream, string command)
        {
            byte[] data = Encoding.UTF8.GetBytes(command + "\n");
            await stream.WriteAsync(data, 0, data.Length);
            Console.WriteLine($"已发送指令至FLA设备: {command}");
        }

        /// <summary>
        /// 从网络流接收结果
        /// </summary>
        /// <param name="stream">网络流</param>
        /// <returns>接收到的结果字符串</returns>
        private static async Task<string> ReceiveResult(NetworkStream stream)
        {
            StringBuilder result = new StringBuilder();
            byte[] buffer = new byte[1024];

            // 等待并接收数据直到遇到结束符
            while (true)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    string partialResult = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    result.Append(partialResult);

                    // 检查是否包含结束标记 (例如OP...PO)
                    if (partialResult.Contains("PO"))
                    {
                        break;
                    }
                }
                else
                {
                    // 连接关闭
                    break;
                }
            }

            return result.ToString().Trim();
        }

        /// <summary>
        /// 向客户端发送响应
        /// </summary>
        /// <param name="stream">客户端网络流</param>
        /// <param name="response">响应内容</param>
        private static async Task SendResponse(NetworkStream stream, string response)
        {
            byte[] data = Encoding.UTF8.GetBytes(response + "\n");
            await stream.WriteAsync(data, 0, data.Length);
            Console.WriteLine($"已向客户端发送响应: {response}");
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private static void Cleanup()
        {
            if (_server != null)
            {
                _server.Stop();
                Console.WriteLine("TCP服务器已停止。");
            }

            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
                Console.WriteLine("光开关串口已关闭。");
            }
        }
    }

    /// <summary>
    /// 客户端请求类
    /// </summary>
    public class ClientRequest
    {
        public TcpClient Client { get; set; }
        public string ClientId { get; set; }
        public string ScanCommand { get; set; }
        public DateTime StartTime { get; set; }
        public RequestStatus Status { get; set; } = RequestStatus.Pending;
    }

    /// <summary>
    /// 请求状态枚举
    /// </summary>
    public enum RequestStatus
    {
        Pending,
        Queued,
        Processing,
        Completed,
        Failed
    }
}