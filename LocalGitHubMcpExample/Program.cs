using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OpenAI.Chat;

Console.WriteLine("Hello, World!");


//1. Establish the Local MCP Connection

await using var mcpClient = await McpClient.CreateAsync(new StdioClientTransport(new()
{
    Name = "MCPServer",
    Command = "npx",
    Arguments = ["-y","--verbose","@modelcontextprotocol/server-github"]
}));

//2 Capability Discovery
// Ask the MCP Server what tools it exposes ( e.g. Search_repositories, get_commit,list_issues)

var mcpTools = await mcpClient.ListToolsAsync().ConfigureAwait(false);
Console.WriteLine($"[System] Discovered {mcpTools.Count()} tools from the local GitHub MCP Server.");

// 3 Initialize the Enterprise Agent

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT_NEW")
                      ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");

if (string.IsNullOrWhiteSpace(endpoint))
{
    throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT environment variable is not set. Set it to your Azure OpenAI resource endpoint, for example https://<resource-name>.openai.azure.com/");
}
var model = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? "gpt-5-mini";

AIAgent releaseAgent = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
            .GetChatClient(model)
            .AsAIAgent(
                name: "ReleaseManager",
                instructions: "You are a DevOps Release Manager. You must only answer quetsions related to GitHub repositories. Use your tools to fetch commit history and summarize it into professional release notes.",
                tools: [.. mcpTools.Cast<AITool>()]
    );
// Execution

string prompt = "Fetch the last 3 commits from the Sohit1991/develop-agents repository and summarize them for our v1.2 release notes.";
Console.WriteLine($"\n[User]: {prompt}");
Console.WriteLine("[Syetm] Agent is autonomously querying GitHub...");

AgentResponse response = await releaseAgent.RunAsync(prompt);
Console.WriteLine($"\nRelease Manager:\n {response.Text}");

