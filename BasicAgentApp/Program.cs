using System.ClientModel;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;



var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT_NEW");
if (string.IsNullOrWhiteSpace(endpoint))
{
    throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT environment variable is not set. Set it to your Azure OpenAI resource endpoint, for example https://<resource-name>.openai.azure.com/");
}

var model = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");
if (string.IsNullOrWhiteSpace(model))
{
    throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT environment variable is not set. Set it to the deployment name you created in Azure OpenAI.");
}

var normalizedEndpoint = NormalizeAzureOpenAIEndpoint(endpoint);

try
{
    IChatClient chatClient = new AzureOpenAIClient(
        new Uri(normalizedEndpoint),
        new AzureCliCredential())
        //new ApiKeyCredential("828DzyYiWyGGk7Ti76QTWi8cFF8qmtanvK2oRWtkrKD7ply2pyueJQQJ99CHACHYHv6XJ3w3AAAAACOGFgyh"))
        .GetChatClient(model)
        .AsIChatClient();
//.AsAIAgent(instructions: "You are a helpful assistant. Keep your answer brief.");
    AIAgent supportAgent=chatClient.AsAIAgent
    (
        name:"NetworkAgent",
        instructions: "You are a Tier 1 IT Support Agent. Your answer must be consise, professional and limited strictly to the question asked. If you do not know the answer, respond with 'I don't know'"
    );

    Console.WriteLine($"Agent '{supportAgent.Name}' is online\n");

    string userIssue="I am getting a DNS resolution error when trying to access the company VPN from a coffee shop";
    Console.WriteLine($"User Issue: {userIssue}");  
    // Non-streaming Response
    //AgentResponse response = await supportAgent.RunAsync(userIssue);
    //Console.WriteLine($"Agent Response: {response.Text}");

    // Continue Straming Responsep
    await foreach(var response in supportAgent.RunStreamingAsync(userIssue))
    {
        Console.Write(response.Text);
    }
}
catch (ClientResultException ex) when (ex.Status == 404)
{
    Console.Error.WriteLine("The Azure OpenAI endpoint or deployment could not be resolved.");
    Console.Error.WriteLine("Verify that AZURE_OPENAI_ENDPOINT points to your Azure OpenAI resource (for example https://<resource-name>.openai.azure.com/)");
    Console.Error.WriteLine("and that AZURE_OPENAI_DEPLOYMENT matches a deployed model in that resource.");
    throw;
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