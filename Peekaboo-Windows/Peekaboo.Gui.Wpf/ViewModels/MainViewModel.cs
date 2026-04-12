using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using Peekaboo.Core;
using Peekaboo.Gui.Wpf.Ai;
using Peekaboo.Gui.Wpf.Mvvm;
using Peekaboo.Gui.Wpf.Sessions;

namespace Peekaboo.Gui.Wpf.ViewModels;

/// <summary>Record representing a tool execution in the history panel.</summary>
public record ToolExecutionEntry(
    string ToolName,
    string? Arguments,
    string? Result,
    bool IsRunning,
    bool IsSuccess,
    TimeSpan? Duration
);

/// <summary>Record representing a chat message for display.</summary>
public record ChatMessageEntry(
    string Role,       // "user", "assistant", "system", "tool"
    string Content,
    DateTimeOffset Timestamp,
    List<ToolExecutionEntry>? ToolCalls = null
);

/// <summary>Main window view model.</summary>
public class MainViewModel : ObservableObject
{
    private readonly IAgentService _agent;
    private readonly SessionStore _sessionStore;
    private readonly IApplicationService _apps;
    private readonly IWindowManagementService _windows;
    private readonly IPermissionsService _permissions;
    private readonly AiSettings _settings;
    private CancellationTokenSource? _taskCts;

    // Sessions
    private ObservableCollection<SessionSummary> _sessions = new();
    public ObservableCollection<SessionSummary> Sessions
    {
        get => _sessions;
        set => SetProperty(ref _sessions, value);
    }

    private SessionSummary? _selectedSession;
    public SessionSummary? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (SetProperty(ref _selectedSession, value) && value != null)
            {
                _sessionStore.SelectSession(value.Id);
                LoadCurrentSessionMessages();
            }
        }
    }

    // Chat messages
    private ObservableCollection<ChatMessageEntry> _chatMessages = new();
    public ObservableCollection<ChatMessageEntry> ChatMessages
    {
        get => _chatMessages;
        set => SetProperty(ref _chatMessages, value);
    }

    // User input
    private string _userInput = "";
    public string UserInput
    {
        get => _userInput;
        set => SetProperty(ref _userInput, value);
    }

    // Search
    private string _searchQuery = "";
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
                FilterMessages();
        }
    }

    private bool _showSearchResults;
    public bool ShowSearchResults
    {
        get => _showSearchResults;
        set => SetProperty(ref _showSearchResults, value);
    }

    private ObservableCollection<ChatMessageEntry> _filteredMessages = new();
    public ObservableCollection<ChatMessageEntry> FilteredMessages
    {
        get => _filteredMessages;
        set => SetProperty(ref _filteredMessages, value);
    }

    // Processing state
    private bool _isProcessing;
    public bool IsProcessing
    {
        get => _isProcessing;
        set => SetProperty(ref _isProcessing, value);
    }

    private bool _isThinking;
    public bool IsThinking
    {
        get => _isThinking;
        set => SetProperty(ref _isThinking, value);
    }

    private string _currentTool = "";
    public string CurrentTool
    {
        get => _currentTool;
        set => SetProperty(ref _currentTool, value);
    }

    private string _currentToolArgs = "";
    public string CurrentToolArgs
    {
        get => _currentToolArgs;
        set => SetProperty(ref _currentToolArgs, value);
    }

    private string _thinkingContent = "";
    public string ThinkingContent
    {
        get => _thinkingContent;
        set => SetProperty(ref _thinkingContent, value);
    }

    // Tool execution history
    private ObservableCollection<ToolExecutionEntry> _toolHistory = new();
    public ObservableCollection<ToolExecutionEntry> ToolHistory
    {
        get => _toolHistory;
        set => SetProperty(ref _toolHistory, value);
    }

    // Running apps
    private ObservableCollection<ServiceApplicationInfo> _runningApps = new();
    public ObservableCollection<ServiceApplicationInfo> RunningApps
    {
        get => _runningApps;
        set => SetProperty(ref _runningApps, value);
    }

    // Windows
    private ObservableCollection<ServiceWindowInfo> _openWindows = new();
    public ObservableCollection<ServiceWindowInfo> OpenWindows
    {
        get => _openWindows;
        set => SetProperty(ref _openWindows, value);
    }

    // Permissions
    private string _permissionStatus = "";
    public string PermissionStatus
    {
        get => _permissionStatus;
        set => SetProperty(ref _permissionStatus, value);
    }

    private bool _isFullyCapable;
    public bool IsFullyCapable
    {
        get => _isFullyCapable;
        set => SetProperty(ref _isFullyCapable, value);
    }

    // Tool history panel visibility
    private bool _showToolHistory = true;
    public bool ShowToolHistory
    {
        get => _showToolHistory;
        set => SetProperty(ref _showToolHistory, value);
    }

    // Error handling
    private string _lastError = "";
    public string LastError
    {
        get => _lastError;
        set => SetProperty(ref _lastError, value);
    }

    private string _lastErrorType = "";
    public string LastErrorType
    {
        get => _lastErrorType;
        set => SetProperty(ref _lastErrorType, value);
    }

    // Commands
    public RelayCommand SendCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand NewSessionCommand { get; }
    public RelayCommand DeleteSessionCommand { get; }
    public RelayCommand ClearChatCommand { get; }
    public AsyncRelayCommand RefreshAppsCommand { get; }
    public AsyncRelayCommand RefreshWindowsCommand { get; }
    public AsyncRelayCommand RefreshPermissionsCommand { get; }
    public RelayCommand ToggleToolHistoryCommand { get; }
    public AsyncRelayCommand<object> LaunchAppCommand { get; }
    public AsyncRelayCommand<object> QuitAppCommand { get; }
    public AsyncRelayCommand<object> FocusWindowCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }

    public MainViewModel(
        IAgentService agent,
        SessionStore sessionStore,
        IApplicationService apps,
        IWindowManagementService windows,
        IClipboardService clipboard,
        IPermissionsService permissions,
        AiSettings settings)
    {
        _agent = agent;
        _sessionStore = sessionStore;
        _apps = apps;
        _windows = windows;
        _permissions = permissions;
        _settings = settings;

        SendCommand = new RelayCommand(SendMessage, () => !IsProcessing && !string.IsNullOrWhiteSpace(UserInput));
        CancelCommand = new RelayCommand(CancelTask, () => IsProcessing);
        NewSessionCommand = new RelayCommand(NewSession);
        DeleteSessionCommand = new RelayCommand(DeleteSelectedSession, () => SelectedSession != null);
        RefreshAppsCommand = new AsyncRelayCommand(LoadRunningApps);
        RefreshWindowsCommand = new AsyncRelayCommand(LoadOpenWindows);
        RefreshPermissionsCommand = new AsyncRelayCommand(LoadPermissions);
        ToggleToolHistoryCommand = new RelayCommand(() => ShowToolHistory = !ShowToolHistory);
        LaunchAppCommand = new AsyncRelayCommand<object>(async name => await LaunchApp(name?.ToString()));
        QuitAppCommand = new AsyncRelayCommand<object>(async name => await QuitApp(name?.ToString()));
        FocusWindowCommand = new AsyncRelayCommand<object>(async title => await FocusWindow(title?.ToString()));
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        ClearChatCommand = new RelayCommand(ClearChat);

        LoadSessions();
        _ = LoadRunningApps();
        _ = LoadOpenWindows();
        _ = LoadPermissions();
    }

    private void LoadSessions()
    {
        Sessions.Clear();
        foreach (var s in _sessionStore.GetSummaries())
            Sessions.Add(s);

        if (_sessionStore.CurrentSession != null)
            SelectedSession = Sessions.FirstOrDefault(s => s.Id == _sessionStore.CurrentSession.Id);

        if (SelectedSession == null && Sessions.Count > 0)
            SelectedSession = Sessions[0];
        else if (SelectedSession == null)
            NewSession();
    }

    private void LoadCurrentSessionMessages()
    {
        ChatMessages.Clear();
        if (_sessionStore.CurrentSession == null) return;

        foreach (var msg in _sessionStore.CurrentSession.Messages)
        {
            ChatMessages.Add(new ChatMessageEntry(
                msg.Role, msg.Content, msg.Timestamp,
                msg.ToolCalls?.Select(tc => new ToolExecutionEntry(
                    tc.Name, tc.Arguments, tc.Result, false, tc.Status == "completed", null)).ToList()));
        }
    }

    private void NewSession()
    {
        var session = _sessionStore.CreateSession(modelName: _settings.SelectedModel);
        Sessions.Insert(0, _sessionStore.GetSummaries().First(s => s.Id == session.Id));
        SelectedSession = Sessions[0];
        ChatMessages.Clear();
        ToolHistory.Clear();
    }

    private void DeleteSelectedSession()
    {
        if (SelectedSession == null || _sessionStore == null) return;
        _sessionStore.DeleteSession(SelectedSession.Id);
        Sessions.Remove(SelectedSession);
        SelectedSession = Sessions.FirstOrDefault();
        if (SelectedSession != null)
            LoadCurrentSessionMessages();
        else
            NewSession();
    }

    private void SendMessage()
    {
        if (IsProcessing || string.IsNullOrWhiteSpace(UserInput)) return;

        var text = UserInput.Trim();
        UserInput = "";

        // Add user message
        var userMsg = new ChatMessageEntry("user", text, DateTimeOffset.Now);
        ChatMessages.Add(userMsg);
        _sessionStore.AddMessage(new ConversationMessage("user", text));

        // Generate session title from first message
        if (_sessionStore.CurrentSession?.Title == "New Session")
        {
            var title = text.Length > 50 ? text.Substring(0, 47) + "..." : text;
            _sessionStore.UpdateTitle(title);
            var idx = Sessions.IndexOf(SelectedSession!);
            if (idx >= 0)
                Sessions[idx] = _sessionStore.GetSummaries().First(s => s.Id == _sessionStore.CurrentSession!.Id);
        }

        // Execute task
        _taskCts = new CancellationTokenSource();
        IsProcessing = true;
        SendCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();

        // Add thinking placeholder
        ChatMessages.Add(new ChatMessageEntry("system", "Thinking...", DateTimeOffset.Now));

        _ = ExecuteTaskAsync(text, _taskCts.Token);
    }

    private async Task ExecuteTaskAsync(string task, CancellationToken ct)
    {
        try
        {
            // Get conversation history from session
            var history = _sessionStore.CurrentSession?.Messages ?? new List<ConversationMessage>();
            
            var result = await _agent.ExecuteTaskAsync(task, history, OnAgentEvent, ct);

            // Remove thinking placeholder and add response
            var thinkingIdx = ChatMessages.ToList().FindIndex(m => m.Role == "system" && m.Content == "Thinking...");
            if (thinkingIdx >= 0) ChatMessages.RemoveAt(thinkingIdx);

            ChatMessages.Add(new ChatMessageEntry("assistant", result, DateTimeOffset.Now));
            _sessionStore.AddMessage(new ConversationMessage("assistant", result));
        }
        catch (OperationCanceledException)
        {
            var thinkingIdx = ChatMessages.ToList().FindIndex(m => m.Role == "system" && m.Content == "Thinking...");
            if (thinkingIdx >= 0) ChatMessages.RemoveAt(thinkingIdx);
            ChatMessages.Add(new ChatMessageEntry("system", "Task was cancelled.", DateTimeOffset.Now));
            LastError = "Task was cancelled by user";
            LastErrorType = "Cancelled";
        }
        catch (HttpRequestException ex)
        {
            var thinkingIdx = ChatMessages.ToList().FindIndex(m => m.Role == "system" && m.Content == "Thinking...");
            if (thinkingIdx >= 0) ChatMessages.RemoveAt(thinkingIdx);
            var errorMsg = $"API Error: {ex.Message}. Check your API key and internet connection.";
            ChatMessages.Add(new ChatMessageEntry("system", errorMsg, DateTimeOffset.Now));
            LastError = ex.Message;
            LastErrorType = "API Error";
        }
        catch (UnauthorizedAccessException ex)
        {
            var thinkingIdx = ChatMessages.ToList().FindIndex(m => m.Role == "system" && m.Content == "Thinking...");
            if (thinkingIdx >= 0) ChatMessages.RemoveAt(thinkingIdx);
            var errorMsg = $"Authentication failed. Please check your API key in Settings.";
            ChatMessages.Add(new ChatMessageEntry("system", errorMsg, DateTimeOffset.Now));
            LastError = ex.Message;
            LastErrorType = "Auth Error";
        }
        catch (Exception ex)
        {
            var thinkingIdx = ChatMessages.ToList().FindIndex(m => m.Role == "system" && m.Content == "Thinking...");
            if (thinkingIdx >= 0) ChatMessages.RemoveAt(thinkingIdx);
            
            // Format error message based on type
            var (shortType, friendlyMsg) = GetFriendlyError(ex);
            ChatMessages.Add(new ChatMessageEntry("system", friendlyMsg, DateTimeOffset.Now));
            LastError = ex.Message;
            LastErrorType = shortType;
        }
        finally
        {
            IsProcessing = false;
            IsThinking = false;
            CurrentTool = "";
            CurrentToolArgs = "";
            ThinkingContent = "";
            SendCommand.RaiseCanExecuteChanged();
            CancelCommand.RaiseCanExecuteChanged();
            _taskCts?.Dispose();
            _taskCts = null;
        }
    }

    private static (string ShortType, string FriendlyMessage) GetFriendlyError(Exception ex)
    {
        var typeName = ex.GetType().Name;
        var msg = ex.Message;

        return typeName switch
        {
            "HttpRequestException" => ("Network Error", $"Network error: {msg}. Check your internet connection."),
            "UnauthorizedAccessException" => ("Auth Error", "Authentication failed. Please check your API key in Settings."),
            "TaskCanceledException" => ("Cancelled", "The operation was cancelled."),
            "ArgumentException" => ("Invalid Input", $"Invalid input: {msg}"),
            "InvalidOperationException" => ("Invalid Operation", msg),
            "TimeoutException" => ("Timeout", "The operation timed out. The AI may be taking too long to respond."),
            "JsonException" => ("Parse Error", "Failed to parse AI response. The model may be unavailable."),
            "KeyNotFoundException" => ("Not Found", msg),
            _ => ("Error", $"Error: {msg}")
        };
    }

    private void OnAgentEvent(AgentEvent evt)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            switch (evt.Kind)
            {
                case AgentEventKind.Thinking:
                    IsThinking = true;
                    ThinkingContent = evt.Content ?? "";
                    // Update thinking message
                    var thinkingIdx = ChatMessages.ToList().FindIndex(m => m.Role == "system" && m.Content == "Thinking...");
                    if (thinkingIdx >= 0)
                        ChatMessages[thinkingIdx] = new ChatMessageEntry("system", $"Thinking: {evt.Content}", DateTimeOffset.Now);
                    break;

                case AgentEventKind.ToolCallStarted:
                    IsThinking = false;
                    CurrentTool = evt.ToolName ?? "";
                    CurrentToolArgs = evt.ToolArgs ?? "";
                    ToolHistory.Add(new ToolExecutionEntry(
                        evt.ToolName ?? "unknown", evt.ToolArgs, null, true, false, null));
                    ChatMessages.Add(new ChatMessageEntry("tool",
                        ToolFormatter.FormatCall(evt.ToolName ?? "unknown", evt.ToolArgs),
                        DateTimeOffset.Now));
                    break;

                case AgentEventKind.ToolCallCompleted:
                    CurrentTool = "";
                    CurrentToolArgs = "";
                    // Update last tool entry
                    if (ToolHistory.Count > 0)
                    {
                        var last = ToolHistory[^1];
                        var idx = ToolHistory.IndexOf(last);
                        ToolHistory[idx] = last with
                        {
                            IsRunning = false,
                            IsSuccess = !string.IsNullOrEmpty(evt.ToolResult) && !evt.ToolResult.StartsWith("Error:"),
                            Result = evt.ToolResult,
                            Duration = evt.Duration,
                        };
                    }
                    break;

                case AgentEventKind.AssistantMessage:
                    IsThinking = false;
                    break;

                case AgentEventKind.Error:
                    IsThinking = false;
                    LastError = evt.Content ?? "Unknown error";
                    LastErrorType = "Tool Error";
                    // Check if it's an API/network error
                    var content = evt.Content ?? "";
                    if (content.Contains("401") || content.Contains("unauthorized"))
                    {
                        LastErrorType = "Auth Error";
                        ChatMessages.Add(new ChatMessageEntry("system", "Authentication failed. Check your API key in Settings.", DateTimeOffset.Now));
                    }
                    else if (content.Contains("429") || content.Contains("rate limit"))
                    {
                        LastErrorType = "Rate Limit";
                        ChatMessages.Add(new ChatMessageEntry("system", "Rate limit exceeded. Wait a moment and try again.", DateTimeOffset.Now));
                    }
                    else if (content.Contains("timeout") || content.Contains("timed out"))
                    {
                        LastErrorType = "Timeout";
                        ChatMessages.Add(new ChatMessageEntry("system", "Operation timed out.", DateTimeOffset.Now));
                    }
                    else
                    {
                        ChatMessages.Add(new ChatMessageEntry("system", $"Error: {content}", DateTimeOffset.Now));
                    }
                    break;

                case AgentEventKind.Completed:
                    IsThinking = false;
                    break;

                case AgentEventKind.Cancelled:
                    IsThinking = false;
                    break;
            }
        });
    }

    private void CancelTask()
    {
        _taskCts?.Cancel();
        _agent.Cancel();
    }

    private async Task LoadRunningApps()
    {
        try
        {
            var apps = await _apps.ListApplicationsAsync();
            Application.Current.Dispatcher.Invoke(() =>
            {
                RunningApps.Clear();
                foreach (var a in apps) RunningApps.Add(a);
            });
        }
        catch { /* Ignore */ }
    }

    private async Task LoadOpenWindows()
    {
        try
        {
            var wins = await _windows.ListWindowsAsync(new WindowTarget.Frontmost());
            Application.Current.Dispatcher.Invoke(() =>
            {
                OpenWindows.Clear();
                foreach (var w in wins) OpenWindows.Add(w);
            });
        }
        catch { /* Ignore */ }
    }

    private async Task LoadPermissions()
    {
        try
        {
            var status = await _permissions.CheckAllAsync();
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsFullyCapable = status.IsFullyCapable;
                PermissionStatus = $"Admin: {(status.IsAdministrator ? "Yes" : "No")} | UIAccess: {(status.HasUiAccess ? "Yes" : "No")}";
            });
        }
        catch { /* Ignore */ }
    }

    private async Task LaunchApp(string? name)
    {
        if (string.IsNullOrEmpty(name)) return;
        try
        {
            await _apps.LaunchApplicationAsync(name);
            await LoadRunningApps();
        }
        catch { /* Ignore */ }
    }

    private async Task QuitApp(string? name)
    {
        if (string.IsNullOrEmpty(name)) return;
        try
        {
            await _apps.QuitApplicationAsync(name);
            await LoadRunningApps();
        }
        catch { /* Ignore */ }
    }

    private async Task FocusWindow(string? title)
    {
        if (string.IsNullOrEmpty(title)) return;
        try
        {
            await _windows.FocusWindowAsync(new WindowTarget.Title(title));
            await LoadOpenWindows();
        }
        catch { /* Ignore */ }
    }

    private void FilterMessages()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            ShowSearchResults = false;
            return;
        }

        ShowSearchResults = true;
        FilteredMessages.Clear();
        var query = SearchQuery.ToLowerInvariant();
        
        foreach (var msg in ChatMessages)
        {
            if (msg.Content.ToLowerInvariant().Contains(query))
                FilteredMessages.Add(msg);
        }
    }

    private void ClearChat()
    {
        if (IsProcessing) return;
        ChatMessages.Clear();
        ToolHistory.Clear();
    }

    private void OpenSettings()
    {
        var settingsWindow = new Peekaboo.Gui.Wpf.Views.SettingsWindow(_settings)
        {
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        settingsWindow.ShowDialog();
    }
}
