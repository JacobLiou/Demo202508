using MIMS.Common;
using System;

internal class Program
{
    private static void Main()
    {
        var client = new PipeClient("mims-bus", "ClientA");
        client.Start();
        System.Threading.Thread.Sleep(1000);
        Console.WriteLine("ClientA started. Commands: \n  send <TargetId> <Text>\n  demo (auto A->B, B->C, D->A)\n");
        while (true)
        {
            var input = System.Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;
            var parts = input.Split(new[] { ' ' }, 3);
            if (parts[0].Equals("send", StringComparison.OrdinalIgnoreCase) && parts.Length >= 3)
            {
                client.SendTo(parts[1], parts[2]);
            }
            else if (parts[0].Equals("demo", StringComparison.OrdinalIgnoreCase))
            {
                // Demo routes: A->B, B->C, D->A (each client only triggers the path it owns)
                if ("ClientA" == "ClientA") client.SendTo("ClientB", "Hello B from A");
                if ("ClientA" == "ClientB") client.SendTo("ClientC", "Hello C from B");
                if ("ClientA" == "ClientD") client.SendTo("ClientA", "Hello A from D");
            }
            else
            {
                // default: send to Hub for broadcast
                client.SendTo("", input);
            }
        }
    }
}