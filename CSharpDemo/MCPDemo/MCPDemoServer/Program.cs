using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging to stderr so it doesn't interfere with stdio transport
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
await app.RunAsync();

[McpServerToolType]
public static class EchoTool
{
    [McpServerTool, Description("Echoes the message back to the client.")]
    public static string Echo(string message) => $"[C# Server] Echo: {message}";
}

[McpServerToolType]
public static class CalculatorTool
{
    [McpServerTool, Description("Adds two numbers.")]
    public static double Add(double a, double b) => a + b;

    [McpServerTool, Description("Subtracts two numbers.")]
    public static double Subtract(double a, double b) => a - b;

    [McpServerTool, Description("Multiplies two numbers.")]
    public static double Multiply(double a, double b) => a * b;

    [McpServerTool, Description("Divides two numbers.")]
    public static double Divide(double a, double b)
    {
        if (b == 0) throw new DivideByZeroException("Cannot divide by zero.");
        return a / b;
    }
}
