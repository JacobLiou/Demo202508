using MIMS.Common;
using System;

internal class Program
{
    private static void Main()
    {
        var client = new PipeClient("mims-bus", "ClientD");
        client.Start();
        System.Threading.Thread.Sleep(1000);
        Console.WriteLine("ClientC started. Commands: \n  send <TargetId> <Text>\n");
        while (true)
        {
            var input = System.Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;
            var parts = input.Split(new[] { ' ' }, 3);
            if (parts[0].Equals("send", StringComparison.OrdinalIgnoreCase) && parts.Length >= 3)
            {
                client.SendTo(parts[1], parts[2]);
            }
        }
    }
}