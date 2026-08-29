using System;
using Azure.Core;
using Azure.Identity;
using Azure.AI.OpenAI;

using OpenAI.Chat;

namespace CustomContextProvider.Services;

public class ChatClientFactory : IChatClientFactory
{
    public ChatClient CreateFromEnvironment(string? model = null, TokenCredential? credential = null)
    {
        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT_NEW")
                       ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT environment variable is not set. Set it to your Azure OpenAI resource endpoint, for example https://<resource-name>.openai.azure.com/");
        }

        model ??= Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? "gpt-5-mini";
        credential ??= new AzureCliCredential();

        return new AzureOpenAIClient(new Uri(endpoint), credential)
                    .GetChatClient(model);
                    //.AsIChatClient();
    }

    //public IChatClient Create(string endpoint, string model, TokenCredential credential)
    //{
    //    if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("endpoint must be provided", nameof(endpoint));
    //    if (string.IsNullOrWhiteSpace(model)) throw new ArgumentException("model must be provided", nameof(model));
    //    if (credential is null) throw new ArgumentNullException(nameof(credential));

    //    return new AzureOpenAIClient(new Uri(endpoint), credential)
    //                .GetChatClient(model)
    //                .AsIChatClient();
    //}
}
