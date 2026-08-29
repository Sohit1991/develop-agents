using Microsoft.Agents.AI.Workflows;

public record CustomrPayload(string CompanyName, string Industry, bool IsValidated = false, string Status = "New");
class Program
{
    static async Task Main(string[] args)
    {
        // 1a . The Validation Node
        Func<CustomrPayload, CustomrPayload> validateFunc = payload =>
        {
            Console.WriteLine($"[Validator] Inspecting payload for : {payload.CompanyName}");
            bool isValid = !string.IsNullOrWhiteSpace(payload.CompanyName);
            return payload with { IsValidated = isValid, Status = isValid ? "Validated" : "Invalid" };
        };

        var validatorExecutor = validateFunc.BindAsExecutor("ValidationNode");

        //1b. The Enrichment Node
        Func<CustomrPayload, CustomrPayload> enrichPayload = payload =>
        {
            Console.WriteLine($"[Enricher] Applying '{payload.Industry}' enterprise templates...");
            return payload with { Status = "Enriched" };
        };

        var enricherExecutor=enrichPayload.BindAsExecutor("EnrichmentNode");

        //1c The Audit Node
        Func<CustomrPayload, CustomrPayload> auditFunc = payload =>
        {
            Console.WriteLine($"[Auditor] Logging Final state to database. Final status: {payload.Status}");
            return payload;
        };

        var auditExecutor=auditFunc.BindAsExecutor("AuditNode");

        //2 Construct the WorkFlow Graph

        var workFlow = new WorkflowBuilder(validatorExecutor)
                        .AddEdge<CustomrPayload>(validatorExecutor, enricherExecutor, condition: p => p?.IsValidated == true)
                        .AddEdge<CustomrPayload>(validatorExecutor, auditExecutor, condition: p => p?.IsValidated == false)
                        .AddEdge(enricherExecutor, auditExecutor)
                        .Build();

        Console.WriteLine("---- Starting Work Flow Execution -----");

        var initialPayload=new CustomrPayload(CompanyName: "Contoso Ltd", Industry: "Technology");

        //3 Execute the Graph
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workFlow, initialPayload);

        // 4 Listen to the stream to observe the nodes completing their work

        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is ExecutorCompletedEvent executorCompleted)
            {
                Console.WriteLine($"[Syetem] -> Node '{executorCompleted.ExecutorId} completed successfully.\n'");
            }
        }
        Console.WriteLine("---Workflow complete ----");
    }
    

}