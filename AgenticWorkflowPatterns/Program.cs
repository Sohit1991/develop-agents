

using AgenticWorkflowPatterns;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

class Program
{
    static async Task Main(string[] args)
    {
        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT_NEW")
                       ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT environment variable is not set. Set it to your Azure OpenAI resource endpoint, for example https://<resource-name>.openai.azure.com/");
        }
        var model = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? "gpt-5-mini";

        IChatClient chatClient = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
                    .GetChatClient(model)
                    .AsIChatClient();

        Console.WriteLine("Select Enterprise Topology:");
        Console.WriteLine("1. Sequential (Localization Pipline)");
        Console.WriteLine("2. Concurrent (Parallel Analysis)");
        Console.WriteLine("3. Handoff (Triage & Support Routing)");
        Console.WriteLine("4. Group Chat (Crisis Management)");
        Console.WriteLine("Choice");

        switch (Console.ReadLine())
        {
            case "1":
                // Sequential Pipine
                // Data flows strictly from French -> Spanish -> English
                var sequentialWorkflow = AgentWorkflowBuilder.BuildSequential(
                    from lang in (string[])["French", "Spanish", "English"]
                    select GetTranslationAgent(lang, chatClient)
                    );
                await EnterpriseOrchestrator.RunWorkflowAsync(sequentialWorkflow,
                    [new ChatMessage(ChatRole.User, "The new enterprise software update will be deployed at midnight")]
                    );
                break;

            case "2":
                // Concurrent Pipine
                // All three Agents process the identical payload simultaneously to reduce latency
                var concurrentWorkflow = AgentWorkflowBuilder.BuildConcurrent(
                    from lang in (string[])["French", "Spanish", "English"]
                    select GetTranslationAgent(lang, chatClient)
                    );
                await EnterpriseOrchestrator.RunWorkflowAsync(concurrentWorkflow,
                    [new ChatMessage(ChatRole.User, "The new enterprise software update will be deployed at midnight")]
                    );
                break;

            case "3":
                // HandOFF ROUTING
                // Triage analyzes the user intent and delegates execution to the correct specialist
                ChatClientAgent networkAdmin = new(chatClient,
                    "You resolve network connectivity and DNS issues. Explain technical steps clearly.",
                    "Network_Admin", "Specialist for networking");

                ChatClientAgent billingSupport = new(chatClient,
                    "You handle enterprise invoice and licensing queries.",
                    "Billing_Support", "Specialist for licensing and billing");

                ChatClientAgent triageRouter = new(chatClient,
                    "Determine if the user needs Network or Billing support. Always handoff to the appropriate agents.",
                    "Triage_Router", "Route message to Specialist");

#pragma warning disable MAAIW001 // This is for evulation purposes only and is subject to change or removal in
                var handoffWorkflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageRouter)
                    //Define the forward transition edges
                    .WithHandoffs(triageRouter, [networkAdmin, billingSupport])
                    //Define the reverse transition edges to return to triage iof needed
                    .WithHandoffs([networkAdmin, billingSupport], triageRouter)
                    .Build();
#pragma warning restore MAAIW001

                List<ChatMessage> conversation = [];
                while (true)
                {
                    Console.WriteLine("\nEnterprise User: ");
                    conversation.Add(new ChatMessage(ChatRole.User, Console.ReadLine()!));
                    // The workflow manages the handoff and returns the updated state

                    var newMessage = await EnterpriseOrchestrator.RunWorkflowAsync(handoffWorkflow, conversation);
                    conversation.AddRange(newMessage);
                }
            case "4":
                // Group CHAT (COLLABORATIVE SWARM) ----
                // Agents converse in a shared context window until iteration limit is reached
                ChatClientAgent secOps = new(chatClient, "You are SecOps. Focus on security liabilities.", "SecOps");
                ChatClientAgent devOps = new(chatClient, "You are DevOps. Focus on uptime and deployment safety.", "DevOps");
                ChatClientAgent legalReview = new(chatClient, "You are Legal. Focus on compliane.", "Legal");

                var groupChatWorkflow = AgentWorkflowBuilder.CreateGroupChatBuilderWith(
                    agents => new RoundRobinGroupChatManager(agents)
                    {
                        MaximumIterationCount = 4
                    }).AddParticipants([secOps, devOps, legalReview])
                    .Build();

                await EnterpriseOrchestrator.RunWorkflowAsync(
                    groupChatWorkflow,
                    [new ChatMessage(ChatRole.User, "We need to push an emergency hotfix to the payment gateway database. Review the implications")]
                    );

                break;

            default:
                break;
        }

    }

    private static ChatClientAgent GetTranslationAgent(string targetLang, IChatClient chatClient) =>
        new(chatClient,
            $"You are a localization expert. Translate the input into {targetLang}. Prepend your response with '[{targetLang}]:'",
            name: $"{targetLang}_Translator");

}