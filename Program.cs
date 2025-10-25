using System;
using OpenAI;
using Azure.AI.OpenAI;
using Azure.Identity;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Safety;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;

// using Microsoft.ML.Tokenizers;

string endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("Environment variable 'AZURE_OPENAI_ENDPOINT' is not set.");

string deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("Environment variable 'AZURE_OPENAI_DEPLOYMENT_NAME' is not set.");

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

string chatSystemMessage = "You are a helpful assistant. Always respond in Japan.";
string userMessageContent = "Tell me a joke about a pirate.";

ChatMessage systemMessage = new(
    ChatRole.System,
    chatSystemMessage);
ChatMessage userMessage = new(ChatRole.User, userMessageContent);

var response = await agent.RunAsync([systemMessage, userMessage]);

Console.WriteLine(response);

// 評価を実行する
// HateAndUnfairnessEvaluatorはAzure AI Content Safetyサービスが必要なため、
// 通常のチャットモデルで動作する評価器を使用します
IEvaluator[] evaluators = [
    new CoherenceEvaluator(),
    new FluencyEvaluator(),
    new RelevanceEvaluator(),
];

AzureOpenAIClient chatClient =
    new(
        new Uri(endpoint),
        new DefaultAzureCredential(new DefaultAzureCredentialOptions()));

IChatClient client = chatClient.GetChatClient(deploymentName: deploymentName).AsIChatClient();

var chatConfig = new ChatConfiguration(client);

// ディスクにキャッシュを保存する ReportingConfiguration を作成
var reportingConfiguration = DiskBasedReportingConfiguration.Create(
    // キャッシュを保存するルートパス
    storageRootPath: "./reports",
    // 評価器の設定
    evaluators,
    chatConfig
    );

// ReportingConfiguration を使って ScenarioRun を作成 (ScenarioRun を使って実際の評価を行う）
await using var scenario = await reportingConfiguration.CreateScenarioRunAsync("QualityEvaluators");

// インプットに対して、結果がちゃんとしているかを評価
EvaluationResult evaluationResult = await scenario.EvaluateAsync(
    // チャットのインプット
    [
        new ChatMessage(ChatRole.System, chatSystemMessage),
        new ChatMessage(ChatRole.User, userMessageContent),
    ],
    // 結果
    new ChatResponse(new ChatMessage(ChatRole.Assistant, response.ToString()))
);

// evaluationResult からメッセージを取り出す
Console.WriteLine("\n=== 評価結果 ===");

// Metrics を確認
Console.WriteLine($"Metrics Count: {evaluationResult.Metrics?.Count}");
if (evaluationResult.Metrics != null)
{
    foreach (var metric in evaluationResult.Metrics)
    {
        Console.WriteLine($"\n【Metric Key】 {metric.Key}");
        
        // EvaluationMetric のプロパティを確認
        if (metric.Value is EvaluationMetric evalMetric)
        {
            Console.WriteLine($"  Name: {evalMetric.Name}");
            Console.WriteLine($"  Reason: {evalMetric.Reason}");
            Console.WriteLine($"  Interpretation: {evalMetric.Interpretation}");
            Console.WriteLine($"  Context: {evalMetric.Context}");
            
            // Diagnostics の詳細を確認
            if (evalMetric.Diagnostics != null)
            {
                Console.WriteLine($"\n  Diagnostics Count: {evalMetric.Diagnostics.Count}");
                foreach (var diagnostic in evalMetric.Diagnostics)
                {
                    Console.WriteLine($"    - Message: {diagnostic.Message}");
                    Console.WriteLine($"      Severity: {diagnostic.Severity}");
                }
            }
            
            // Metadata の確認
            if (evalMetric.Metadata != null)
            {
                Console.WriteLine($"\n  Metadata Count: {evalMetric.Metadata.Count}");
                foreach (var meta in evalMetric.Metadata)
                {
                    Console.WriteLine($"    {meta.Key}: {meta.Value}");
                }
            }
        }
    }
}
