using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Peekaboo.Gui.Wpf.Ai;

/// <summary>AI provider configuration settings.</summary>
public class AiSettings
{
    public string SelectedProvider { get; set; } = "openrouter";
    public string SelectedModel { get; set; } = "deepseek/deepseek-v3.2";
    public string? OpenAiApiKey { get; set; }
    public string? AnthropicApiKey { get; set; }
    public string? OpenRouterApiKey { get; set; }
    public string? OpenRouterModel { get; set; } = "deepseek/deepseek-v3.2";
    public string? OllamaBaseUrl { get; set; } = "http://localhost:11434";
    public string? OllamaModel { get; set; } = "llava";
    public double Temperature { get; set; } = 0.3;
    public int MaxTokens { get; set; } = 4096;
    public int MaxSteps { get; set; } = 25;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string SettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Peekaboo", "ai-settings.json");

    public static AiSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AiSettings>(json, JsonOpts) ?? new AiSettings();
            }
        }
        catch { /* Use defaults */ }
        return new AiSettings();
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(this, JsonOpts);
        File.WriteAllText(SettingsPath, json);
    }
}
