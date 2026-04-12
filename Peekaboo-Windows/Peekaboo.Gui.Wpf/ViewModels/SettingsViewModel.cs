using System.Collections.ObjectModel;
using System.Windows;
using Peekaboo.Gui.Wpf.Ai;
using Peekaboo.Gui.Wpf.Mvvm;

namespace Peekaboo.Gui.Wpf.ViewModels;

/// <summary>Settings window view model.</summary>
public class SettingsViewModel : ObservableObject
{
    private readonly AiSettings _settings;

    public ObservableCollection<string> Providers { get; } = new() { "OpenAI", "Anthropic", "OpenRouter", "Ollama" };

    private string _selectedProvider;
    public string SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if (SetProperty(ref _selectedProvider, value))
            {
                Models.Clear();
                foreach (var m in AiProviderFactory.GetModelsForProvider(value.ToLowerInvariant()))
                    Models.Add(m);
                if (Models.Count > 0) SelectedModel = Models[0];
            }
        }
    }

    public ObservableCollection<string> Models { get; } = new();

    private string _selectedModel;
    public string SelectedModel
    {
        get => _selectedModel;
        set => SetProperty(ref _selectedModel, value);
    }

    private string _openAiApiKey = "";
    public string OpenAiApiKey
    {
        get => _openAiApiKey;
        set => SetProperty(ref _openAiApiKey, value);
    }

    private string _anthropicApiKey = "";
    public string AnthropicApiKey
    {
        get => _anthropicApiKey;
        set => SetProperty(ref _anthropicApiKey, value);
    }

    private string _openRouterApiKey = "";
    public string OpenRouterApiKey
    {
        get => _openRouterApiKey;
        set => SetProperty(ref _openRouterApiKey, value);
    }

    private string _openRouterModel = "anthropic/claude-3.5-sonnet";
    public string OpenRouterModel
    {
        get => _openRouterModel;
        set => SetProperty(ref _openRouterModel, value);
    }

    private string _ollamaBaseUrl = "http://localhost:11434";
    public string OllamaBaseUrl
    {
        get => _ollamaBaseUrl;
        set => SetProperty(ref _ollamaBaseUrl, value);
    }

    private string _ollamaModel = "llava";
    public string OllamaModel
    {
        get => _ollamaModel;
        set => SetProperty(ref _ollamaModel, value);
    }

    private double _temperature = 0.3;
    public double Temperature
    {
        get => _temperature;
        set => SetProperty(ref _temperature, value);
    }

    private int _maxTokens = 4096;
    public int MaxTokens
    {
        get => _maxTokens;
        set => SetProperty(ref _maxTokens, value);
    }

    private int _maxSteps = 25;
    public int MaxSteps
    {
        get => _maxSteps;
        set => SetProperty(ref _maxSteps, value);
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private bool _isTesting;
    public bool IsTesting
    {
        get => _isTesting;
        set => SetProperty(ref _isTesting, value);
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand TestConnectionCommand { get; }

    public SettingsViewModel(AiSettings settings)
    {
        _settings = settings;

        _selectedProvider = settings.SelectedProvider;
        _temperature = settings.Temperature;
        _maxTokens = settings.MaxTokens;
        _maxSteps = settings.MaxSteps;
        _openAiApiKey = settings.OpenAiApiKey ?? "";
        _anthropicApiKey = settings.AnthropicApiKey ?? "";
        _openRouterApiKey = settings.OpenRouterApiKey ?? "";
        _openRouterModel = settings.OpenRouterModel ?? "anthropic/claude-3.5-sonnet";
        _ollamaBaseUrl = settings.OllamaBaseUrl ?? "http://localhost:11434";
        _ollamaModel = settings.OllamaModel ?? "llava";

        // Load models for selected provider
        foreach (var m in AiProviderFactory.GetModelsForProvider(_selectedProvider.ToLowerInvariant()))
            Models.Add(m);

        // Set selected model
        _selectedModel = Models.Contains(settings.SelectedModel) ? settings.SelectedModel : (Models.Count > 0 ? Models[0] : "");

        SaveCommand = new RelayCommand(Save);
        TestConnectionCommand = new RelayCommand(TestConnection, () => !IsTesting);
    }

    public void Save()
    {
        _settings.SelectedProvider = SelectedProvider.ToLowerInvariant();
        _settings.SelectedModel = SelectedModel;
        _settings.OpenAiApiKey = string.IsNullOrWhiteSpace(OpenAiApiKey) ? null : OpenAiApiKey;
        _settings.AnthropicApiKey = string.IsNullOrWhiteSpace(AnthropicApiKey) ? null : AnthropicApiKey;
        _settings.OpenRouterApiKey = string.IsNullOrWhiteSpace(OpenRouterApiKey) ? null : OpenRouterApiKey;
        _settings.OpenRouterModel = string.IsNullOrWhiteSpace(OpenRouterModel) ? null : OpenRouterModel;
        _settings.OllamaBaseUrl = OllamaBaseUrl;
        _settings.OllamaModel = OllamaModel;
        _settings.Temperature = Temperature;
        _settings.MaxTokens = MaxTokens;
        _settings.MaxSteps = MaxSteps;

        _settings.Save();
        StatusMessage = "Settings saved successfully.";
        Application.Current.Dispatcher.BeginInvoke(new Action(() => StatusMessage = ""), TimeSpan.FromSeconds(3));
    }

    private async void TestConnection()
    {
        IsTesting = true;
        StatusMessage = "Testing connection...";

        try
        {
            var provider = AiProviderFactory.Create(_settings);
            var response = await provider.ChatAsync(new[]
            {
                new ChatMessage(ChatRole.System, "Reply with exactly: OK"),
                new ChatMessage(ChatRole.User, "Hello"),
            });

            StatusMessage = $"Connection OK. Model: {provider.ModelName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }
}
