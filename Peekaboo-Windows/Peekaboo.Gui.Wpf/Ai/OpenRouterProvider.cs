using System.ClientModel;
using System.Text.Json;
using OpenAI;
using OpenAI.Chat;

namespace Peekaboo.Gui.Wpf.Ai;

/// <summary>OpenRouter provider using OpenAI-compatible API.</summary>
public class OpenRouterProvider : IAiProvider
{
    private readonly ChatClient _client;
    private readonly string _modelName;

    public string ModelName => _modelName;
    public bool SupportsVision => true; // Most OpenRouter models support vision

    public OpenRouterProvider(string apiKey, string model = "anthropic/claude-3.5-sonnet")
    {
        _modelName = model;
        _client = new ChatClient(
            model,
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions { Endpoint = new Uri("https://openrouter.ai/api/v1") });
    }

    public async Task<ChatResponse> ChatAsync(ChatMessage[] messages, CancellationToken ct = default)
    {
        var openMessages = ConvertMessages(messages);
        var options = new ChatCompletionOptions
        {
            Temperature = 0.3f,
            MaxOutputTokenCount = 4096,
        };

        var result = await _client.CompleteChatAsync(openMessages, options, ct);
        var value = result.Value;

        int inputTokens = value.Usage?.InputTokenCount ?? 0;
        int outputTokens = value.Usage?.OutputTokenCount ?? 0;

        return new ChatResponse(
            Content: value.Content[0]?.Text ?? string.Empty,
            InputTokens: inputTokens,
            OutputTokens: outputTokens);
    }

    public async IAsyncEnumerable<ChatResponseChunk> StreamChatAsync(ChatMessage[] messages, CancellationToken ct = default)
    {
        var openMessages = ConvertMessages(messages);
        var options = new ChatCompletionOptions
        {
            Temperature = 0.3f,
            MaxOutputTokenCount = 4096,
        };

        var updateCollection = _client.CompleteChatStreamingAsync(openMessages, options, ct);
        await foreach (var update in updateCollection.WithCancellation(ct))
        {
            var delta = update.ContentUpdate.Count > 0 ? update.ContentUpdate[0]?.Text ?? string.Empty : string.Empty;
            var isFinished = update.FinishReason == ChatFinishReason.Stop;
            yield return new ChatResponseChunk(delta, isFinished);
            if (isFinished) break;
        }
    }

    private static IReadOnlyList<OpenAI.Chat.ChatMessage> ConvertMessages(ChatMessage[] messages)
    {
        var result = new List<OpenAI.Chat.ChatMessage>();
        foreach (var msg in messages)
        {
            if (msg.ImageData != null && msg.Role == ChatRole.User)
            {
                var imagePart = OpenAI.Chat.ChatMessageContentPart.CreateImagePart(
                    BinaryData.FromBytes(msg.ImageData),
                    "image/png");
                var textPart = OpenAI.Chat.ChatMessageContentPart.CreateTextPart(msg.Content);
                result.Add(new OpenAI.Chat.UserChatMessage(textPart, imagePart));
            }
            else
            {
                result.Add(msg.Role switch
                {
                    ChatRole.System => new OpenAI.Chat.SystemChatMessage(msg.Content),
                    ChatRole.User => new OpenAI.Chat.UserChatMessage(msg.Content),
                    ChatRole.Assistant => new OpenAI.Chat.AssistantChatMessage(msg.Content),
                    _ => new OpenAI.Chat.UserChatMessage(msg.Content),
                });
            }
        }
        return result;
    }
}