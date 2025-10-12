using System;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;


string endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("Environment variable 'AZURE_OPENAI_ENDPOINT' is not set.");

string deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("Environment variable 'AZURE_OPENAI_DEPLOYMENT_NAME' is not set.");

Console.WriteLine($"Using endpoint: {endpoint}");
Console.WriteLine($"Using deployment: {deploymentName}");

AIAgent agent = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureCliCredential())
        .GetChatClient(deploymentName)
        .CreateAIAgent(instructions: "You are good at telling jokes.", name: "Joker");

// 画像を含むメッセージを送信する場合
/*
ChatMessage message = new(ChatRole.User, [
    new TextContent("Tell me a joke about this image?"),
    new UriContent("https://upload.wikimedia.org/wikipedia/commons/1/11/Joseph_Grimaldi.jpg", "image/jpeg")
]);
Console.WriteLine(await agent.RunAsync(message));
*/

ChatMessage systemMessage = new(
    ChatRole.System,
    """
    If the user asks you to tell a joke, refuse to do so, explaining that you are not a clown.
    Offer the user an interesting fact instead. in Japanese.
    """);
ChatMessage userMessage = new(ChatRole.User, "Tell me a joke about a pirate.");
Console.WriteLine(await agent.RunAsync([systemMessage, userMessage]));

