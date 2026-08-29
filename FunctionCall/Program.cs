
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using System.ComponentModel;
using System.Text.Json;

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT_NEW");
var model = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")??"gpt-5-mini";
AIFunction rawRefundFunction=AIFunctionFactory.Create(FinanceTools.IssueRefund);
AIFunction secureRefundTool=new ApprovalRequiredAIFunction(rawRefundFunction);


AIAgent agent=new AzureOpenAIClient(
            new Uri(endpoint),
            new AzureCliCredential())
            .GetChatClient(model)
            // .AsAIAgent(
            //     name:"LogisticsAgent",
            //     instructions: "You are a helpful logistics assistant. You can provide information about the shipping status of enterprise logistics orders. If the user provides an Order ID, you can retrieve the current shipping status using the GetOrderStatus tool."
            //     +"If the user does not provide an Order ID, politely ask them to provide one.",
            //     tools:[AIFunctionFactory.Create(LogisticsTools.GetOrderStatus)]                
            // );
            .AsAIAgent(
                name:"FinanceSupport",
                instructions: "You are a customer support Agent with billing priviliges. You must help customer process their refund.",
                tools:[secureRefundTool]                
            );
// Logistics Agent
// Console.WriteLine($"Agent '{agent.Name}' is online\n. Type 'exit' to end the conversation\n");
// // Execution Phase

// Console.WriteLine("--- Synchronous Execution ----");
// string prompt="What is the shipping status of order ORD-12345?";
// Console.WriteLine($"User: {prompt}");
// AgentResponse response=await agent.RunAsync(prompt);
// Console.WriteLine($"Agent: {response.Text}\n");

// Finance Agent

AgentSession session=await agent.CreateSessionAsync();
Console.WriteLine($"Agent '{agent.Name}' is online\n. Ready for secure requests\n");
string userPrompt="I would like to request a refund for order ORD-12345 for the amount of $50.00.";
Console.WriteLine($"User: {userPrompt}");
AgentResponse response=await agent.RunAsync(userPrompt,session);

// Check if the Agent paused to request human approval

var approvalRequests=response.Messages
                    .SelectMany(a=>a.Contents.OfType<ToolApprovalRequestContent>())
                    .ToList();

if(approvalRequests.Any())
{
    ToolApprovalRequestContent approvalRequest=approvalRequests.First();
    var requestToolCall=(FunctionCallContent)approvalRequest.ToolCall;
    string toolArguments=JsonSerializer.Serialize(requestToolCall.Arguments);

    // Display the AI's intent to the human Manager
    Console.ForegroundColor=ConsoleColor.Yellow;
    Console.WriteLine($"--- Human Approval Required ---{requestToolCall.Name}");
    Console.WriteLine($"Proposed Arguments: {toolArguments}");
    Console.WriteLine("Do you approve this action? [Y/N]");
    Console.ResetColor();
    string? input=Console.ReadLine();
    bool isApproved=input?.Trim().ToUpper()=="Y";

    // Send the human's decision back to the Agent
    var approvalMessage=new Microsoft.Extensions.AI.ChatMessage(
        role:ChatRole.User,
        new []{approvalRequest.CreateResponse(isApproved) }
        );  
        response=await agent.RunAsync(approvalMessage,session);
}
Console.WriteLine($"Agent: {response.Text}\n");

public static class LogisticsTools
{
    [Description("Retrieves the current shipping status of an enterprise logistics order. Invoke this tool ONLY when the user explicitly provides an Order ID.")]
    public static string GetOrderStatus([Description("The exact, case-sensitive alphanumeric order identifier. Format must be 'ORD-' followed by 5 digits.(e.g., ORD-12345)")] string orderId)
    {
        if(orderId=="ORD-12345") return "Order ORD-12345 has been shipped and is expected to arrive on 2026-08-15.";
        if(orderId=="ORD-67890") return "Order ORD-67890 is currently being processed and is expected to ship on 2026-08-20.";
        // Simulate retrieving order status from a database or API
        return $"Order {orderId} is not present in the system.";
    }
    
} 

// Human In Loop Functionality 
public static class FinanceTools
{

    [Description("Issues a refund for a specific order. Invoke this tool ONLY when the user explicitly requests a refund for an OrderID")]
    public static string IssueRefund(
        [Description("The Order ID to refund (e.g., 'ORD-12345)")] string orderId, 
        [Description("The amount to refund in USD.")] decimal amount)
    {
        // Simulate issuing a refund for the specified order
        Console.WriteLine($"Executing a secure transaction, Issuing a refund of ${amount} for order {orderId}...");
        return $"A refund of ${amount} has been issued for order {orderId}.";
    }
}