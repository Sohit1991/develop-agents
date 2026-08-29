using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT_NEW");
//var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
if (string.IsNullOrWhiteSpace(endpoint))
{
    throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT environment variable is not set. Set it to your Azure OpenAI resource endpoint, for example https://<resource-name>.openai.azure.com/");
}
//var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT_NEW")??throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT environment variable is not set.");

//var normalizedEndpoint = NormalizeAzureOpenAIEndpoint(endpoint);

var model = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")??"gpt-5-mini";
IChatClient chatClient=new AzureOpenAIClient(
            new Uri(endpoint),
            new AzureCliCredential())
            .GetChatClient(model)
            .AsIChatClient();

// Create the Agent
AIAgent agent=chatClient.AsAIAgent(
    name:"HistoryBuff",
    instructions: "You are a helpful history teacher. Your answer questions and help students make connections better"
    );       

  // Create the Session (the mempory COntainer)
  // accumalete the conversation history and context
  AgentSession  session= await agent.CreateSessionAsync();
  Console.WriteLine($"Agent '{agent.Name}' is online\n. Type 'exit' to end the conversation\n");

  // 4 THe Conversation Loop
while(true)
{
    Console.Write("User: ");
    string? input=Console.ReadLine();
    if(string.IsNullOrWhiteSpace(input) || input.Equals("exit",StringComparison.OrdinalIgnoreCase))
    {
        break;
    }
    AgentResponse response=await agent.RunAsync(input,session);
    Console.WriteLine($"Agent: {response.Text}\n");
}

static string NormalizeAzureOpenAIEndpoint(string endpoint)
{
    var uri = new Uri(endpoint);

    if (uri.Host.Contains(".services.ai.azure.com", StringComparison.OrdinalIgnoreCase))
    {
        var normalizedHost = uri.Host.Replace(".services.ai.azure.com", ".openai.azure.com", StringComparison.OrdinalIgnoreCase);
        return $"{uri.Scheme}://{normalizedHost}{(uri.IsDefaultPort ? string.Empty : $":{uri.Port}")}";
    }

    return endpoint;
}