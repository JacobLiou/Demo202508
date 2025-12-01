using MIMS.Common;
using System;

internal class Program
{
    private static void Main()
    {
        var hub = new PipeHub("mims-bus");
        hub.Start();
        Console.WriteLine("Hub running. Type any text to broadcast.");
        while (true)
        {
            var line = Console.ReadLine();
            hub.Broadcast(new BusMessage { Type = "Data", From = "Hub", Payload = line });
        }
    }
}