using Azure.Core;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace CustomContextProvider.Services;

public interface IChatClientFactory
{
    // Creates an IChatClient using environment variables. If model or credential are provided they override environment values.
    ChatClient CreateFromEnvironment(string? model = null, TokenCredential? credential = null);

    // Creates an IChatClient with explicit parameters (useful for tests or when injecting credentials).
    //IChatClient Create(string endpoint, string model, TokenCredential credential);
}
