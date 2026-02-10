using MeshNetwork.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MeshNetwork.NodeApp
{
    class Program
    {
        static void Main(string[] args)
        {
            // Expected args: [port] [peer1_port] [peer2_port] ...
            // Example: 9001 9002 9003 9004

            if (args.Length == 0)
            {
                Console.WriteLine("Usage: MeshNetwork.Node.exe <port> [peer ports...]");
                return;
            }

            if (!int.TryParse(args[0], out int port))
            {
                Console.WriteLine("Invalid port number.");
                return;
            }

            var peers = new List<string>();
            for (int i = 1; i < args.Length; i++)
            {
                if (int.TryParse(args[i], out int peerPort))
                {
                    // Don't add self as peer
                    if (peerPort != port)
                    {
                        peers.Add($"http://localhost:{peerPort}/");
                    }
                }
            }

            using (var node = new Node(port, peers))
            {
                node.OnMessageReceived += (msg) =>
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\n[RECEIVED] {msg}");
                    Console.ResetColor();
                    Console.Write("> "); // Restore prompt
                };

                node.Start();

                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine($"Node initialized on port {port}");
                Console.WriteLine($"Peers: {string.Join(", ", peers)}");
                Console.WriteLine("Type a message and press Enter to broadcast.");
                Console.WriteLine("Type '@<port> <message>' for private message (e.g., @9002 Hello).");
                Console.WriteLine("Type 'exit' to quit.");
                Console.WriteLine("--------------------------------------------------");

                Console.Write("> ");
                while (true)
                {
                    string input = Console.ReadLine();
                    if (string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    if (!string.IsNullOrWhiteSpace(input))
                    {
                        if (input.StartsWith("@"))
                        {
                            // Point-to-Point Format: @<port> <message>
                            var parts = input.Split(new[] { ' ' }, 2);
                            if (parts.Length > 1)
                            {
                                string portStr = parts[0].Substring(1); // Remove '@'
                                string message = parts[1];

                                if (int.TryParse(portStr, out int targetPort))
                                {
                                    string targetUrl = $"http://localhost:{targetPort}/";
                                    var task = node.SendToTargetAsync(targetUrl, $"From {port} (Private): {message}");
                                }
                                else
                                {
                                    Console.WriteLine("Invalid port format. Use @<port> <message>");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Invalid format. Use @<port> <message>");
                            }
                        }
                        else
                        {
                            // Broadcast
                            var task = node.BroadcastAsync($"From {port}: {input}");
                        }
                    }
                    Console.Write("> ");
                }
            }
        }
    }
}
