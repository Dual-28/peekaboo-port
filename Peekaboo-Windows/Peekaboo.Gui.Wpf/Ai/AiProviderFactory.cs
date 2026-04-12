namespace Peekaboo.Gui.Wpf.Ai;

/// <summary>Factory for creating AI providers based on configuration.</summary>
public static class AiProviderFactory
{
    public static IAiProvider Create(AiSettings settings)
    {
        return settings.SelectedProvider.ToLowerInvariant() switch
        {
            "openai" => CreateOpenAi(settings),
            "anthropic" => CreateAnthropic(settings),
            "ollama" => CreateOllama(settings),
            "openrouter" => CreateOpenRouter(settings),
            _ => throw new ArgumentException($"Unknown provider: {settings.SelectedProvider}"),
        };
    }

    private static IAiProvider CreateOpenAi(AiSettings settings)
    {
        var apiKey = settings.OpenAiApiKey
            ?? throw new InvalidOperationException("OpenAI API key is not configured. Go to Settings to add it.");
        return new OpenAiProvider(apiKey, settings.SelectedModel);
    }

    private static IAiProvider CreateAnthropic(AiSettings settings)
    {
        var apiKey = settings.AnthropicApiKey
            ?? throw new InvalidOperationException("Anthropic API key is not configured. Go to Settings to add it.");
        return new AnthropicProvider(apiKey, settings.SelectedModel);
    }

    private static IAiProvider CreateOpenRouter(AiSettings settings)
    {
        var apiKey = settings.OpenRouterApiKey
            ?? throw new InvalidOperationException("OpenRouter API key is not configured. Go to Settings to add it.");
        var model = settings.OpenRouterModel ?? "anthropic/claude-3.5-sonnet";
        return new OpenRouterProvider(apiKey, model);
    }

    private static IAiProvider CreateOllama(AiSettings settings)
    {
        var model = settings.OllamaModel ?? "llava";
        var baseUrl = settings.OllamaBaseUrl ?? "http://localhost:11434";
        return new OllamaProvider(baseUrl, model);
    }

    public static string[] GetModelsForProvider(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "openai" => new[] { "gpt-4o", "gpt-4.1", "gpt-4.1-mini", "gpt-4o-mini", "o1", "o3-mini", "gpt-5.1" },
            "anthropic" => new[] { "claude-sonnet-4-20250514", "claude-opus-4-20250514", "claude-haiku-4-20250514", "claude-3-5-sonnet-20241022" },
            "openrouter" => new[] { "deepseek/deepseek-v3.2", "deepseek/deepseek-chat", "anthropic/claude-3.5-sonnet", "anthropic/claude-3-opus", "google/gemini-2.0-flash", "meta-llama/llama-3.3-70b-instruct" },
            "ollama" => new[] { "llava", "llama3.2-vision", "moondream", "llama3.1", "llama3.2", "mistral" },
            _ => Array.Empty<string>(),
        };
    }
}
