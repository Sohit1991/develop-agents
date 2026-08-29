using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI.Workflows;

public record TicketState(string UserQuery, string Category = "Unassigned", string FinalResolution = "");
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

        //1 The Router  
        AIAgent triageAgent = chatClient.AsAIAgent
            (
            name: "TriageAgent",
            instructions: "Analyze the user's IT request. Categorise it strictly as either 'Hardware' or 'Software'. Output only the category word."
            );

        AIAgent hardWareAgent = chatClient.AsAIAgent
           (
           name: "HardWareSupport",
           instructions: "You are an enterprise hardware specialist. Provide concise troubleshooting steps for physcial device issues."
           );

        AIAgent softWareAgent = chatClient.AsAIAgent
           (
           name: "softWareSupport",
           instructions: "You are an enterprise software specialist. Provide concise troubleshooting steps for applications, OD and network issues."
           );

        // WorkFlow to route the user request to the appropriate agent based on the triage agent's output

        Func<TicketState, TicketState> triageFunc = state =>
        {
            Console.WriteLine($"[Triage] Analyzing ticket: '{state.UserQuery}'");
            AgentResponse response = triageAgent.RunAsync(state.UserQuery).GetAwaiter().GetResult();
            string category = response.Text.Trim();
            Console.WriteLine($"[Triage] Decision: Routed to {category} Department.");
            return state with { Category = category };
        };

        var triageNode = triageFunc.BindAsExecutor("TriageNode");
        // Hardware Node Execution Logic

        Func<TicketState, TicketState> hardWareFunc = state =>
        {
            Console.WriteLine($"[HardWare Support] Generating resolution.....");
            AgentResponse response = hardWareAgent.RunAsync(state.UserQuery).GetAwaiter().GetResult();

            return state with { FinalResolution = response.Text };
        };

        var hardwareNode = hardWareFunc.BindAsExecutor("HardwareNode");
        // Software Node Execution Logic

        Func<TicketState, TicketState> softWareFunc = state =>
        {
            Console.WriteLine($"[Software Support] Generating resolution.....");
            AgentResponse response = softWareAgent.RunAsync(state.UserQuery).GetAwaiter().GetResult();

            return state with { FinalResolution = response.Text };
        };

        var softwareNode = hardWareFunc.BindAsExecutor("SoftwareNode");

        // Build the Graph with Conditional Edges

        var workflow = new WorkflowBuilder(triageNode)
                    // If triage says Hardware, route to the Hardware Agent
                    .AddEdge<TicketState>(triageNode, hardwareNode, condition: state => state != null && state.Category.Contains("Hardware", StringComparison.OrdinalIgnoreCase))
                    // If Triage says Software, route to the Software Agent
                    .AddEdge<TicketState>(triageNode, softwareNode, condition: state => state != null && state.Category.Contains("Software", StringComparison.OrdinalIgnoreCase))
                    .Build();


        Console.WriteLine("---- Incoming Enterprise IT Ticket ----\n");
        var initialTicket = new TicketState("My laptop screeen is flickering aggressively and the hinge feels loosen");

        // Execute the Workflow Graph

        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, initialTicket);

        TicketState? finalState = null;

        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is ExecutorCompletedEvent executorComplete)
            {
                Console.WriteLine($"[System]-> Node '{executorComplete.ExecutorId}' completed.");
                // Cast Data to TicketState
                if (executorComplete.Data is TicketState ticketState)
                {
                    finalState= ticketState;
                    Console.WriteLine($"   state: Category='{ticketState.Category}', Resolution=''");
                }

            }
        }

        Console.WriteLine($"\n --- Final Resoltion --- \n {finalState?.FinalResolution}");
    }
}


