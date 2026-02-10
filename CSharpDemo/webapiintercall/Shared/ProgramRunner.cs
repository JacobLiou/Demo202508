using System;
using System.Threading.Tasks;

namespace Shared
{
    /// <summary>
    /// 通用启动逻辑：启动自托管 API + 控制台输入广播到所有对等节点。
    /// 四个 Console 仅通过不同 (name, port) 调用此方法即可互相通信。
    /// </summary>
    public static class ProgramRunner
    {
        public static void Run(string myName, int myPort)
        {
            var config = PeerConfig.DefaultFourPeers(myName, myPort);
            var server = new SimpleHttpServer(config.ListenPrefix, config.MyName, msg =>
            {
                Console.WriteLine("[收到] {0} -> {1}: {2} ({3})", msg.From, msg.To, msg.Content, msg.Time);
            });
            server.Start();
            Console.WriteLine("[{0}] 已启动，监听 {1}。输入内容回车即广播到其余节点，输入 exit 退出。", config.MyName, config.ListenPrefix);

            var client = new PeerClient(config.MyName, config.PeerBaseUrls);
            while (true)
            {
                var line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase)) break;
                try
                {
                    client.BroadcastAsync(line.Trim()).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("发送失败: " + ex.Message);
                }
            }
            server.Stop();
        }
    }
}
