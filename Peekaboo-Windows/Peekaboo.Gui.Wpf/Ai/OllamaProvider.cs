using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Peekaboo.Gui.Wpf.Ai;

/// <summary>Ollama provider using the local Ollama REST API.</summary>
public class OllamaProvider : IAiProvider
{
    private readonly HttpClient _http;
    private readonly string _modelName;
    private readonly string _baseUrl;

    public string ModelName => _modelName;
    public bool SupportsVision => _modelName.Contains("llava") || _modelName.Contains("moondream");

    public OllamaProvider(string baseUrl = "http://localhost:11434", string model = "llava")
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _modelName = model;
        _http = new HttpClient { BaseAddress = new Uri(_baseUrl) };
    }

    public async Task<ChatResponse> ChatAsync(ChatMessage[] messages, CancellationToken ct = default)
    {
        var payload = new
        {
            model = _modelName,
            messages = messages.Select(m => new
            {
                role = m.Role switch
                {
                    ChatRole.System => "system",
                    ChatRole.User => "user",
                    ChatRole.Assistant => "assistant",
                    _ => "user",
                },
                content = m.Content,
                images = m.ImageData != null ? new[] { Convert.ToBase64String(m.ImageData) } : null,
            }).Where(m => m.content != null || m.images != null).ToArray(),
            stream = false,
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("/api/chat", content, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        var message = root.GetProperty("message");
        var text = message.GetProperty("content").GetString() ?? string.Empty;

        int inputTokens = 0, outputTokens = 0;
        if (root.TryGetProperty("prompt_eval_count", out var pec)) inputTokens = pec.GetInt32();
        if (root.TryGetProperty("eval_count", out var ec)) outputTokens = ec.GetInt32();

        return new ChatResponse(text, inputTokens, outputTokens);
    }

    public async IAsyncEnumerable<ChatResponseChunk> StreamChatAsync(ChatMessage[] messages, CancellationToken ct = default)
    {
        var payload = new
        {
            model = _modelName,
            messages = messages.Select(m => new
            {
                role = m.Role switch
                {
                    ChatRole.System => "system",
                    ChatRole.User => "user",
                    ChatRole.Assistant => "assistant",
                    _ => "user",
                },
                content = m.Content,
                images = m.ImageData != null ? new[] { Convert.ToBase64String(m.ImageData) } : null,
            }).Where(m => m.content != null || m.images != null).ToArray(),
            stream = true,
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("/api/chat", content, ct);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var message = root.GetProperty("message");
            var delta = message.GetProperty("content").GetString() ?? string.Empty;
            var isDone = root.TryGetProperty("done", out var done) && done.GetBoolean();

            yield return new ChatResponseChunk(delta, isDone);
            if (isDone) break;
        }
    }
}
