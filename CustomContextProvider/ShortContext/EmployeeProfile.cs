using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;


namespace CustomContextProvider.ShortContext
{
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
                var result = await _chatClient. GetResponseAsync<EmployeeProfile>(
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

        public class CustomSessionAgentCall
        {
            public async Task TestCustomSessionAgent(ChatClient chatClient)
            {
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



            }
        }
    }
}
