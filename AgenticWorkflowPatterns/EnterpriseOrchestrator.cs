using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace AgenticWorkflowPatterns
{
    public static class EnterpriseOrchestrator
    {
        public static async Task<List<ChatMessage>> RunWorkflowAsync(Workflow workflow, List<ChatMessage> messages)
        {
            string? lastExecutorId = null;
            // Push the conversational state into the workflow

            await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages);

            // Instruct the engine to emit events for the active turn
            await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

            await foreach (WorkflowEvent evt in run.WatchStreamAsync())
            {
                // Stream token generation in real-time
                if (evt is AgentResponseUpdateEvent responseEvent)
                {
                    if (responseEvent.ExecutorId != lastExecutorId)
                    {
                        lastExecutorId = responseEvent.ExecutorId;
                        Console.WriteLine($"\n\n--[Active Node: {responseEvent.ExecutorId}] ----");
                    }
                    Console.WriteLine(responseEvent.Update.Text);

                    // Log autonomous tool executions
                    if (responseEvent.Update.Contents.OfType<FunctionCallContent>().FirstOrDefault() is FunctionCallContent call)
                    {
                        Console.WriteLine($"\n [System] -> Executing Tool '{call.Name}' with payload:{JsonSerializer.Serialize(call)}");
                    }
                }
                else if (evt is WorkflowOutputEvent output)
                {
                    Console.WriteLine("\n\n--- WorkFlow Terminated ---");
                    return output.As<List<ChatMessage>>()!;
                }
            }
            return [];



        }
    }
}
