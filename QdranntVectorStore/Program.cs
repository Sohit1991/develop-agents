using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using QdranntVectorStore;
using Qdrant.Client;

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

// Initialize the Embedding Generator (The translator)

var embeddingGenerator = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
                        .GetEmbeddingClient("text-embedding-3-small")
                        .AsIEmbeddingGenerator();

// connect to Qdrant Db

var qdrantCLient = new QdrantClient("localhost", 6334);
var vectorStore = new QdrantVectorStore(qdrantCLient, ownsClient: true);

// Get the specific collection contain our Architectural records

var adrCollection = vectorStore.GetCollection<Guid, ArchitectureDecision>("enterprise_Adrs");
await adrCollection.EnsureCollectionExistsAsync();

// 4 Send the collection with sample ADR records

var sampleAdrs = new List<ArchitectureDecision>
{
    new()
    {
        DocumentId=Guid.Parse("111111111-1111-1111-1111-111111111111"),
        Title="ADR-001: grpc for Internal Microservices Communication",
        Content="In January 2024, we decided to adopt gRPC over Rest for intenal microservices commuication."
    },
     new()
    {
        DocumentId=Guid.Parse("222222222-2222-2222-2222-222222222222"),
        Title="ADR-002: PostgreSQL as Primary Database",
        Content="In March 2024, we selected PostgreSQl over MongoDB for our primary database."
    },
      new()
    {
        DocumentId=Guid.Parse("333333333-3333-3333-3333-333333333333"),
        Title="ADR-003: Event-Driven Architecture with KAFKA",
        Content="In Feb 2024, we adopted Apache Kafka for event-driven communication."
    }
};

foreach (var adr in sampleAdrs)
{
    var embedding = await embeddingGenerator.GenerateAsync(adr.Content);
    adr.ContentVector = embedding.Vector;
    await adrCollection.UpsertAsync(adr);
}

Console.WriteLine($"Seeded {sampleAdrs.Count} ADR records into Qdran.\n");

// 6. Configure the TextSearchProvider options for RAG behavior

TextSearchProviderOptions textSearchOptions = new()
{
    SearchTime = TextSearchProviderOptions.TextSearchBehavior.BeforeAIInvoke
};

// 7 Create the Vector Search Adapter

async Task<IEnumerable<TextSearchProvider.TextSearchResult>> VectorSearchAdapter(string query, CancellationToken cancellationToken)
{
    // Generate embedding for the user's query

    var queryEmbedding = await embeddingGenerator.GenerateAsync(query, cancellationToken: cancellationToken);
    var queryVector = queryEmbedding.Vector;

    // Search the Qdrant vector store for semantically similar ADRs (top 3 results)

    var searchOptions = new VectorSearchOptions<ArchitectureDecision>();

    var searchResults = adrCollection.SearchAsync(queryVector, 3, searchOptions, cancellationToken);

    // COnvert Qdrant results to TextSearchProvider results

    var results = new List<TextSearchProvider.TextSearchResult>();

    await foreach (var result in searchResults)
    {
        results.Add(new TextSearchProvider.TextSearchResult
        {
            SourceName = $"ADR: {result.Record.Title}",
            SourceLink = $"adr://{result.Record.DocumentId}",
            Text = $"Title: {result.Record.Title}\nContent: {result.Record.Content}"
        });
    }
    return results;

}

// 8. Initialize the Agent with the Qdrant-backed RAG capability

AIAgent architectAgent = chatClient.AsAIAgent(new ChatClientAgentOptions()
{
    Name = "EnterpriseArchitect",
    ChatOptions = new()
    {
        Instructions = "You are a senior enterprise architects. Always reference the provided ADR context to answer questions about past architectral questions."
    },
    AIContextProviders = [new TextSearchProvider(VectorSearchAdapter, textSearchOptions)]
});

Console.WriteLine("-----Enterprise Archiotecture Swarm Online --\n");

// The active researcher loop begins

string query = "Why did we choose gRPS over REST for the internal microservices communication in 2024 ?";
// The Agent will autonomously:
//1. Invoke the VectorSearchAdapter with the user's query.
//2. The adapet will embed the query and search qdrant.
//3. Qdrant will return the top semantic matches.
//4. The agent will read the retrieved ADRS and synthesize the final answer.

AgentResponse response = await architectAgent.RunAsync(query);
Console.WriteLine($"\nAgent: {response.Text}");