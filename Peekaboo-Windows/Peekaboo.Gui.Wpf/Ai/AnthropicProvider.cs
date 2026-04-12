using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Peekaboo.Gui.Wpf.Ai;

/// <summary>Anthropic Claude provider using the REST API.</summary>
public class AnthropicProvider : IAiProvider
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _modelName;

    public string ModelName => _modelName;
    public bool SupportsVision => _modelName.Contains("sonnet") || _modelName.Contains("opus") || _modelName.Contains("haiku");

    public AnthropicProvider(string apiKey, string model = "claude-sonnet-4-20250514")
    {
        _apiKey = apiKey;
        _modelName = model;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public async Task<ChatResponse> ChatAsync(ChatMessage[] messages, CancellationToken ct = default)
    {
        var systemMsg = messages.FirstOrDefault(m => m.Role == ChatRole.System);
        var userMessages = messages.Where(m => m.Role != ChatRole.System).ToArray();

        var payload = new
        {
            model = _modelName,
            max_tokens = 4096,
            system = systemMsg?.Content,
            messages = userMessages.Select(m => new
            {
                role = m.Role == ChatRole.Assistant ? "assistant" : "user",
                content = BuildContent(m),
            }).ToArray(),
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("https://api.anthropic.com/v1/messages", content, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        var textContent = root.GetProperty("content")
            .EnumerateArray()
            .Where(c => c.GetProperty("type").GetString() == "text")
            .Select(c => c.GetProperty("text").GetString())
            .FirstOrDefault() ?? string.Empty;

        var usage = root.GetProperty("usage");
        int inputTokens = usage.TryGetProperty("input_tokens", out var it) ? it.GetInt32() : 0;
        int outputTokens = usage.TryGetProperty("output_tokens", out var ot) ? ot.GetInt32() : 0;

        return new ChatResponse(textContent, inputTokens, outputTokens);
    }

    public async IAsyncEnumerable<ChatResponseChunk> StreamChatAsync(ChatMessage[] messages, CancellationToken ct = default)
    {
        // Non-streaming fallback for simplicity
        var result = await ChatAsync(messages, ct);
        yield return new ChatResponseChunk(result.Content, true);
    }

    private static object BuildContent(ChatMessage msg)
    {
        if (msg.ImageData != null)
        {
            var base64 = Convert.ToBase64String(msg.ImageData);
            return new object[]
            {
                new { type = "text", text = msg.Content },
                new
                {
                    type = "image",
                    source = new
                    {
                        type = "base64",
                        media_type = "image/png",
                        data = base64,
                    }
                }
            };
        }
        return msg.Content;
    }
}
