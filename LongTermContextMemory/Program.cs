using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using System.Text;
using System.Text.Json;


var repository = new MockCosmoDbRepository();
var agentService=new StatelessAgentService(repository);

string userId = "user-1234";

Console.WriteLine("---Monday Morning ----");

string response1=await agentService.HandleUserMessageAsync(userId, "Good morning! I am planning to take a vacation to London. Can you help me with that?");
Console.WriteLine($"Agent: {response1}\n");

// SHut the application down and restart it to simulate a new session

Console.WriteLine("---Friday Morning (Simulating a new Server request)---");
string response2=await agentService.HandleUserMessageAsync(userId, "Do you remember where I said I am plannning a trip to London I am back from my vacation. Can you remind me what I asked you on Monday?");
Console.WriteLine($"Agent: {response2}\n");




public interface ISessionRepository
{
    Task<string?> GetSessionJsonAsync(string sessionId);

    Task SaveSessionJsonAsync(string sessionId, string jsonPayload);
}

public class MockCosmoDbRepository : ISessionRepository
{
    private readonly Dictionary<string, string> _datastore = new();
    public Task<string?> GetSessionJsonAsync(string sessionId) => Task.FromResult(_datastore.TryGetValue(sessionId, out var json) ? json : null);


    public Task SaveSessionJsonAsync(string sessionId, string jsonPayload)
    {
        _datastore[sessionId] = jsonPayload;
        return Task.CompletedTask;
    }
}

class StatelessAgentService
{
    private readonly AIAgent _agent;
    private readonly ISessionRepository _repository;

    public StatelessAgentService(ISessionRepository repository)
    {
        this._repository = repository;

        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT_NEW")
                       ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT environment variable is not set. Set it to your Azure OpenAI resource endpoint, for example https://<resource-name>.openai.azure.com/");
        }

        var model = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? "gpt-5-mini";

        _agent = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
            .GetChatClient(model)
            .AsAIAgent(
            name: "PersistentGuide",
            instructions: "You are a helpful assistant that remember details across long periods of time."
            );
    }

    // 2 Ststeless Execution methid called by ASP>NEt

    public async Task<string> HandleUserMessageAsync(string sessionId, string userMessage)
    {
        AgentSession session;

        // Step A : Attempt to retriev historical state from db

        string? savedSessionJson = await _repository.GetSessionJsonAsync(sessionId);
        if (!string.IsNullOrEmpty(savedSessionJson))
        {
            using JsonDocument doc = JsonDocument.Parse(savedSessionJson);

            session = await _agent.DeserializeSessionAsync(doc.RootElement);
            Console.WriteLine($"[SYSTEM LOG] successfullly restored session {sessionId} from db.");
        }
        else
        {
            // Fallback
            session = await _agent.CreateSessionAsync();
            Console.WriteLine($"[SYSTEM LOG] created new session for {sessionId}");
        }

        // step D : Execute the agent with laoded session

        AgentResponse response = await _agent.RunAsync(userMessage, session);

        JsonElement updatedSesionElement = await _agent.SerializeSessionAsync(session);
        string updatedJsonString = JsonSerializer.Serialize(updatedSesionElement);

        await _repository.SaveSessionJsonAsync(sessionId, updatedJsonString);

        return response.Text;

    }

}