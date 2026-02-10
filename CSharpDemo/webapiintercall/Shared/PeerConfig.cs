using System;
using System.Collections.Generic;

namespace Shared
{
    /// <summary>
    /// 本节点配置：名称、监听地址、所有对等节点基地址（用于互相通信）。
    /// </summary>
    public class PeerConfig
    {
        public string MyName { get; set; }
        public string ListenPrefix { get; set; }
        public List<string> PeerBaseUrls { get; set; }

        public PeerConfig()
        {
            PeerBaseUrls = new List<string>();
        }

        /// <summary>
        /// 创建四节点默认配置。每个控制台只需改 MyName 和 ListenPrefix，PeerBaseUrls 为其余三节点。
        /// </summary>
        public static PeerConfig DefaultFourPeers(string myName, int myPort)
        {
            const string host = "http://localhost:";
            var ports = new[] { 5001, 5002, 5003, 5004 };
            var config = new PeerConfig
            {
                MyName = myName,
                ListenPrefix = host + myPort + "/"
            };
            foreach (var p in ports)
            {
                if (p == myPort) continue;
                config.PeerBaseUrls.Add(host + p);
            }
            return config;
        }
    }
}
