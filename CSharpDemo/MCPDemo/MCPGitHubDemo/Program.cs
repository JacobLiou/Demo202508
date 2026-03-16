using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Octokit;
using System.ComponentModel;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging to stderr
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Get GitHub Token from environment variable
string? githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

builder.Services.AddSingleton<GitHubClient>(sp =>
{
    var client = new GitHubClient(new ProductHeaderValue("MCP-GitHub-Demo"));
    if (!string.IsNullOrEmpty(githubToken))
    {
        client.Credentials = new Credentials(githubToken);
    }
    return client;
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
await app.RunAsync();

[McpServerToolType]
public static class GitHubTools
{
    [McpServerTool, Description("Checks the current GitHub user connection status.")]
    public static async Task<string> GetUserInfo(IServiceProvider services)
    {
        var client = services.GetRequiredService<GitHubClient>();
        try
        {
            var user = await client.User.Current();
            return $"Connected as: {user.Login} ({user.Name})";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}. Make sure GITHUB_TOKEN is set.";
        }
    }

    [McpServerTool, Description("Uploads markdown content to a GitHub repository.")]
    public static async Task<string> UploadMarkdown(
        IServiceProvider services,
        [Description("Repository name (e.g., 'owner/repo')")] string repoFullName,
        [Description("Path for the new file (e.g., 'docs/README.md')")] string path,
        [Description("The markdown content to upload")] string content,
        [Description("Commit message")] string message = "Update via MCP")
    {
        var client = services.GetRequiredService<GitHubClient>();
        var parts = repoFullName.Split('/');
        if (parts.Length != 2) return "Invalid repository format. Use 'owner/repo'.";

        string owner = parts[0];
        string repoName = parts[1];

        try
        {
            // Try to create or update the file
            await client.Repository.Content.CreateFile(owner, repoName, path, new CreateFileRequest(message, content));
            return $"Successfully uploaded to {repoFullName}/{path}";
        }
        catch (Exception ex)
        {
            return $"Failed to upload: {ex.Message}";
        }
    }

    [McpServerTool, Description("Creates a GitHub Gist with the provided markdown content.")]
    public static async Task<string> CreateMarkdownGist(
        IServiceProvider services,
        [Description("Description of the Gist")] string description,
        [Description("Filename for the Gist")] string fileName,
        [Description("Markdown content")] string content,
        [Description("Whether the Gist should be public")] bool isPublic = false)
    {
        var client = services.GetRequiredService<GitHubClient>();
        try
        {
            var gist = new NewGist
            {
                Description = description,
                Public = isPublic
            };
            gist.Files.Add(fileName, content);

            var createdGist = await client.Gist.Create(gist);
            return $"Gist created successfully! URL: {createdGist.HtmlUrl}";
        }
        catch (Exception ex)
        {
            return $"Failed to create Gist: {ex.Message}";
        }
    }
}

// NOTE: Integration with MarkItDown MCP would typically happen by this server 
// acting as an MCP Client. In a real scenario, you would use McpClient to 
// connect to the MarkItDown MCP server and call its tools.
