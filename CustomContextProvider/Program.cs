using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using System.Text;
using System.Text.Json;

// namespace removed so top-level statements can be used

// --- Program (top-level statements) -------------------------------------------------
// Configure a minimal Host and register the ChatClientFactory for DI so other components can consume it.
//var host = Host.CreateDefaultBuilder(args)
//    .ConfigureServices(services =>
//    {
//        services.AddSingleton<IChatClientFactory, ChatClientFactory>();
//    })
//    .Build();

//using var scope = host.Services.CreateScope();
//var factory = scope.ServiceProvider.GetRequiredService<IChatClientFactory>();
//var chatClient = factory.CreateFromEnvironment();

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT_NEW")
                       ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");

if (string.IsNullOrWhiteSpace(endpoint))
{
    throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT environment variable is not set. Set it to your Azure OpenAI resource endpoint, for example https://<resource-name>.openai.azure.com/");
}

var model = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? "gpt-5-mini";



ChatClient chatClient = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
            .GetChatClient(model);


AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions()
{
    Name = "CorporateGuide",
    ChatOptions = new() { Instructions = "You are a helpful assistant that collects employee profile information." },
    AIContextProviders = new[] { new EmployeeProileProvider(chatClient.AsIChatClient()) }
});

// 4. Create a new session

AgentSession session = await agent.CreateSessionAsync();
Console.WriteLine("----Starting Fresh Session ----");

try
{
    Console.WriteLine(await agent.RunAsync("What is the company policy on remote work?", session));
    Console.WriteLine(await agent.RunAsync("My Name is Sohit and I work in the IT Department", session));
}
catch (Exception ex)
{
    Console.WriteLine(ex);
    //throw;
}

// 5. Serialise the session (This automactically captures the extracted EmployeeProfile)
JsonElement serializedSession = await agent.SerializeSessionAsync(session);

Console.WriteLine("\n --- Simulating a New Day (Deserializing Session) ----");

// 6. Deserialize and resume

var resumedSession = await agent.DeserializeSessionAsync(serializedSession);

Console.WriteLine(await agent.RunAsync("Can you remind me of my department", resumedSession));
// 7. Accessing the strongly-typed memory directly from code
var profileProvider = agent.GetService<EmployeeProileProvider>();
var profile = profileProvider?.GetProfile(resumedSession);
Console.WriteLine("\n[SYSTEM DIAGOSTICS] Explicitly reading memory component:");
Console.WriteLine($"Extracted Name: {profile?.EmployeeName}\n Department {profile?.Department} ");



//CustomSessionAgentCall cu = new CustomSessionAgentCall();

//cu.TestCustomSessionAgent(chatClient);


public class EmployeeProfile
{
    public string? EmployeeName { get; set; }
    public string? Department { get; set; }
}

public sealed class EmployeeProileProvider : AIContextProvider
{
    private readonly ProviderSessionState<EmployeeProfile> _sessionState;
    private readonly IChatClient _chatClient;

    public EmployeeProileProvider(IChatClient chatClient) : base(null, null)
    {
        _sessionState = new ProviderSessionState<EmployeeProfile>(
            _ => new EmployeeProfile(),
            this.GetType().Name);
        _chatClient = chatClient;
    }

    public override IReadOnlyList<string> StateKeys => new[] { _sessionState.StateKey };

    public EmployeeProfile GetProfile(AgentSession session) => _sessionState.GetOrInitializeState(session);

    // Phase 1 : Pre-Invocation (Injecting context)
    protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        var profile = _sessionState.GetOrInitializeState(context.Session);
        StringBuilder instructions = new();

        instructions.AppendLine(profile.EmployeeName is null ?
            "Ask the user for their name and politely decline to answer corporate questions until they provide they provide it."
            : $"The user's name is {profile.EmployeeName}")
        .AppendLine(profile.Department is null ?
            "Ask the user for their department and politely decline to answer corporate questions until they provide it."
            : $"The user's department is {profile.Department} Tailor your answers to this department.");

        return new ValueTask<AIContext>(new AIContext { Instructions = instructions.ToString() });
    }

    // Phase 2 : Post-Invocation (Storing context)
    protected override async ValueTask StoreAIContextAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        var profile = _sessionState.GetOrInitializeState(context.Session);
        if ((profile.EmployeeName is null || profile.Department is null))
        //&& context.RequestMessages.Any(a=>a.Role))
        {
            var result = await _chatClient.GetResponseAsync<EmployeeProfile>(
                context.RequestMessages,
                new ChatOptions()
                {
                    Instructions = "Extract the user's name and department from the conversation. If you cannot find them, return null for those fields."
                },
                cancellationToken: cancellationToken);
            profile.EmployeeName ??= result.Result?.EmployeeName;
            profile.Department ??= result.Result?.Department;
        }
        _sessionState.SaveState(context.Session, profile);
    }
}

//// Create the Agent
//AIAgent agent = chatClient.AsAIAgent(
//    name: "HistoryBuff",
//    instructions: "You are a helpful history teacher. Your answer questions and help students make connections better"
//    );

//// Create the Session (the memory container)
//AgentSession session = await agent.CreateSessionAsync();
//Console.WriteLine($"Agent '{agent.Name}' is online\n. Type 'exit' to end the conversation\n");

//// Conversation Loop
//while (true)
//{
//    Console.Write("User: ");
//    string? input = Console.ReadLine();
//    if (string.IsNullOrWhiteSpace(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
//    {
//        break;
//    }
//    AgentResponse response = await agent.RunAsync(input, session);
//    Console.WriteLine($"Agent: {response.Text}\n");
//}
