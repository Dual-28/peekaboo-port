using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using ImGuiNET;
using Peekaboo.Core;
using Peekaboo.Gui.Wpf.Ai;
using Peekaboo.Gui.Wpf.Sessions;
using Peekaboo.Platform.Windows.Gui;
using IconHashes = Peekaboo.Platform.Windows.Gui.IconHashes;

namespace Peekaboo.Gui.Wpf.Rendering;

public record ChatMessageEntry(
    string Role,
    string Content,
    DateTimeOffset Timestamp,
    List<ToolExecutionEntry>? ToolCalls = null
);

public record ToolExecutionEntry(
    string ToolName,
    string? Arguments,
    string? Result,
    bool IsRunning,
    bool IsSuccess,
    TimeSpan? Duration
);

public class ImGuiMainViewModel
{
    private readonly IAgentService _agent;
    private readonly SessionStore _sessionStore;
    private readonly IApplicationService _apps;
    private readonly IWindowManagementService _windows;
    private readonly IPermissionsService _permissions;
    private readonly AiSettings _settings;
    private readonly object _stateLock = new();
    private CancellationTokenSource? _taskCts;

    private int _currentTab;
    private float _sidebarWidth = 70f;
    private float _targetSidebarWidth = 70f;
    private bool _isExpanded;
    private bool _showToolHistory = true;
    private string _userInput = "";
    private string _searchQuery = "";
    private bool _isProcessing;

    public ObservableCollection<SessionSummary> Sessions { get; } = new();
    public ObservableCollection<ChatMessageEntry> ChatMessages { get; } = new();
    public ObservableCollection<ToolExecutionEntry> ToolHistory { get; } = new();
    public ObservableCollection<ServiceApplicationInfo> RunningApps { get; } = new();
    public ObservableCollection<ServiceWindowInfo> OpenWindows { get; } = new();

    private string _permissionStatus = "";
    public string PermissionStatus => _permissionStatus;

    private bool _isFullyCapable;
    public bool IsFullyCapable => _isFullyCapable;

    public ImGuiMainViewModel(
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
    }

    public void Initialize()
    {
        LoadSessions();
        _ = Task.Run(LoadRunningApps);
        _ = Task.Run(LoadOpenWindows);
        _ = Task.Run(LoadPermissions);
    }

    public void Shutdown()
    {
        _taskCts?.Cancel();
        _taskCts?.Dispose();
    }

    private void LoadSessions()
    {
        Sessions.Clear();
        foreach (var s in _sessionStore.GetSummaries())
            Sessions.Add(s);

        if (Sessions.Count == 0)
            NewSession();
    }

    private void NewSession()
    {
        var session = _sessionStore.CreateSession(modelName: _settings.SelectedModel);
        lock (_stateLock)
        {
            Sessions.Insert(0, _sessionStore.GetSummaries().First(s => s.Id == session.Id));
            ChatMessages.Clear();
            ToolHistory.Clear();
        }
    }

    private void LoadCurrentSessionMessages()
    {
        lock (_stateLock)
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
    }

    public async System.Threading.Tasks.Task LoadRunningApps()
    {
        try
        {
            var apps = await _apps.ListApplicationsAsync();
            lock (_stateLock)
            {
                RunningApps.Clear();
                foreach (var a in apps) RunningApps.Add(a);
            }
        }
        catch { }
    }

    public async System.Threading.Tasks.Task LoadOpenWindows()
    {
        try
        {
            var wins = await _windows.ListWindowsAsync(new WindowTarget.All());
            lock (_stateLock)
            {
                OpenWindows.Clear();
                foreach (var w in wins) OpenWindows.Add(w);
            }
        }
        catch { }
    }

    public async System.Threading.Tasks.Task LoadPermissions()
    {
        try
        {
            var status = await _permissions.CheckAllAsync();
            _isFullyCapable = status.IsFullyCapable;
            _permissionStatus = $"Admin: {(status.IsAdministrator ? "Yes" : "No")} | UIA: {(status.HasUiAccess ? "Yes" : "No")}";
        }
        catch { }
    }

    public void Render()
    {
        UpdateSidebarAnimation();
        RenderSidebar();
        RenderMainContent();
    }

    private void UpdateSidebarAnimation()
    {
        if (_isExpanded && _sidebarWidth < _targetSidebarWidth)
            _sidebarWidth = Math.Min(_sidebarWidth + 15, _targetSidebarWidth);
        else if (!_isExpanded && _sidebarWidth > _targetSidebarWidth)
            _sidebarWidth = Math.Max(_sidebarWidth - 15, _targetSidebarWidth);
    }

    private void RenderSidebar()
    {
        var io = ImGui.GetIO();
        ImDrawListPtr drawList = default;
        var sidebarPos = Vector2.Zero;

        float windowHeight = io.DisplaySize.Y;
        
        PushStyleVars();
        
        ImGui.SetNextWindowSize(new Vector2(_sidebarWidth + 20, windowHeight));
        ImGui.SetNextWindowPos(new Vector2(0, 0));
        
        ImGui.Begin("##sidebar", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar);
        {
            sidebarPos = ImGui.GetWindowPos();
            drawList = ImGui.GetWindowDrawList();
            
            drawList.AddRectFilled(
                sidebarPos,
                new Vector2(sidebarPos.X + _sidebarWidth, sidebarPos.Y + windowHeight),
                ImGui.GetColorU32(new Vector4(0.078f, 0.078f, 0.098f, 1f)),
                8, ImDrawFlags.RoundCornersLeft);
            
            DrawLogo(sidebarPos);
            
            if (_isExpanded)
            {
                ImGui.SetCursorPos(new Vector2(15, 50));
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "NAVIGATION");
            }
            
            ImGui.SetCursorPos(new Vector2(15, 70));
            ImGui.BeginGroup();
            {
                if (Tab("Chat", IconHashes.ICON_FA_COMMENTS, _currentTab == 0)) _currentTab = 0;
                if (Tab("Apps", IconHashes.ICON_FA_DESKTOP, _currentTab == 1)) _currentTab = 1;
                if (Tab("Windows", IconHashes.ICON_FA_WINDOW_RESTORE, _currentTab == 2)) _currentTab = 2;
                if (Tab("History", IconHashes.ICON_FA_HISTORY, _currentTab == 3)) _currentTab = 3;
                if (Tab("Settings", IconHashes.ICON_FA_COG, _currentTab == 4)) _currentTab = 4;
            }
            ImGui.EndGroup();
            
            if (_isExpanded)
            {
                ImGui.SetCursorPos(new Vector2(15, 450));
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "SESSIONS");
                
                ImGui.SetCursorPos(new Vector2(15, 470));
                if (ImGui.Button("+ New", new Vector2(_sidebarWidth - 30, 25)))
                {
                    NewSession();
                }
                
                ImGui.SetCursorPos(new Vector2(15, 500));
                ImGui.BeginChild("##sessions", new Vector2(_sidebarWidth - 20, 120), ImGuiChildFlags.Borders);
                {
                    foreach (var session in Sessions)
                    {
                        if (ImGui.Selectable(session.Title, false, ImGuiSelectableFlags.SpanAllColumns))
                        {
                            _sessionStore.SelectSession(session.Id);
                            LoadCurrentSessionMessages();
                        }
                    }
                }
                ImGui.EndChild();
            }
            
            ImGui.SetCursorPos(new Vector2(15, windowHeight - 50));
            ImGui.TextColored(new Vector4(0.4f, 0.4f, 0.4f, 1f), PermissionStatus);
            
            ImGui.SetCursorPos(new Vector2(_sidebarWidth - 20, 10));
            if (ImGui.InvisibleButton("##toggle", new Vector2(30, windowHeight - 20)))
            {
                _isExpanded = !_isExpanded;
            }
            
            if (io.MousePos.X > sidebarPos.X && io.MousePos.X < sidebarPos.X + _sidebarWidth + 10 &&
                io.MousePos.Y > 5 && io.MousePos.Y < windowHeight - 10)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
            }
        }
        ImGui.End();
        
        PopStyleVars();
        
        float lineAlpha = _isExpanded ? 255 : 0;
        drawList.AddLine(
            new Vector2(sidebarPos.X + _sidebarWidth, sidebarPos.Y),
            new Vector2(sidebarPos.X + _sidebarWidth, sidebarPos.Y + windowHeight),
            ImGui.GetColorU32(new Vector4(0.25f, 0.55f, 1f, lineAlpha / 255f)),
            1);
        
        drawList.AddCircleFilled(
            new Vector2(sidebarPos.X + _sidebarWidth, sidebarPos.Y + 75),
            9,
            ImGui.GetColorU32(new Vector4(0.25f, 0.55f, 1f, lineAlpha / 255f)),
            32);
        
        string chevron = _isExpanded ? IconHashes.ICON_FA_CHEVRON_LEFT : IconHashes.ICON_FA_CHEVRON_RIGHT;
        drawList.AddText(
            new Vector2(sidebarPos.X + _sidebarWidth - 8, sidebarPos.Y + 65),
            ImGui.GetColorU32(new Vector4(1, 1, 1, lineAlpha / 255f)),
            chevron);
    }

    private void DrawLogo(Vector2 pos)
    {
        var drawList = ImGui.GetWindowDrawList();
        float centerX = pos.X + _sidebarWidth / 2;
        float centerY = pos.Y + 30;
        
        drawList.AddCircleFilled(new Vector2(centerX, centerY), 12, ImGui.GetColorU32(new Vector4(0.25f, 0.55f, 1f, 1f)), 32);
        drawList.AddCircleFilled(new Vector2(centerX, centerY), 8, ImGui.GetColorU32(new Vector4(0.08f, 0.08f, 0.1f, 1f)), 32);
        drawList.AddCircleFilled(new Vector2(centerX - 2, centerY - 2), 3, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.8f)), 32);
    }

    private bool Tab(string label, string icon, bool selected)
    {
        float tabWidth = _isExpanded ? _sidebarWidth - 30 : 50;
        float tabHeight = _isExpanded ? 40 : 35;

        var drawList = ImGui.GetWindowDrawList();
        var itemMin = ImGui.GetCursorScreenPos();
        var itemMax = new Vector2(itemMin.X + tabWidth, itemMin.Y + tabHeight);
        bool clicked = ImGui.InvisibleButton("##" + label, new Vector2(tabWidth, tabHeight));
        bool hovered = ImGui.IsItemHovered();

        Vector4 bgColor, textColor;
        
        if (selected)
        {
            bgColor = new Vector4(0.15f, 0.15f, 0.2f, 1f);
            textColor = new Vector4(1f, 1f, 1f, 1f);
        }
        else if (hovered)
        {
            bgColor = new Vector4(0.1f, 0.1f, 0.12f, 1f);
            textColor = new Vector4(0.7f, 0.7f, 0.7f, 1f);
        }
        else
        {
            bgColor = new Vector4(0, 0, 0, 0);
            textColor = new Vector4(0.5f, 0.5f, 0.5f, 1f);
        }

        drawList.AddRectFilled(
            itemMin,
            itemMax,
            ImGui.GetColorU32(bgColor),
            4);

        uint textColorU32 = ImGui.GetColorU32(textColor);
        if (_isExpanded)
        {
            drawList.AddText(new Vector2(itemMin.X + 10, itemMin.Y + 10), textColorU32, icon);
            drawList.AddText(new Vector2(itemMin.X + 35, itemMin.Y + 12), textColorU32, label);
        }
        else
        {
            drawList.AddText(new Vector2(itemMin.X + 15, itemMin.Y + 10), textColorU32, icon);
        }

        return clicked;
    }

    private void RenderMainContent()
    {
        var io = ImGui.GetIO();
        float mainX = _sidebarWidth + 20;
        float mainWidth = Math.Max(320, io.DisplaySize.X - mainX - (_showToolHistory ? 300 : 0));
        float mainHeight = io.DisplaySize.Y;
        
        ImGui.SetNextWindowSize(new Vector2(mainWidth, mainHeight));
        ImGui.SetNextWindowPos(new Vector2(mainX, 0));
        
        ImGui.Begin("##main", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse);
        {
            var pos = ImGui.GetWindowPos();
            var drawList = ImGui.GetWindowDrawList();
            
            drawList.AddRectFilled(
                pos,
                new Vector2(pos.X + mainWidth, pos.Y + 60),
                ImGui.GetColorU32(new Vector4(0.137f, 0.141f, 0.161f, 1f)));
            
            ImGui.SetCursorPos(new Vector2(20, 20));
            ImGui.Text(GetCurrentTabTitle());
            
            ImGui.SameLine(mainWidth - 150);
            ImGui.SetCursorPos(new Vector2(mainWidth - 130, 20));
            
            ImGui.PushItemWidth(120);
            ImGui.InputText("##search", ref _searchQuery, 100);
            ImGui.PopItemWidth();
            
            ImGui.SetCursorPos(new Vector2(mainWidth - 30, 20));
            if (ImGui.Button("X", new Vector2(20, 20)))
            {
                _searchQuery = "";
            }
            
            RenderCurrentTab(mainWidth, mainHeight);
        }
        ImGui.End();
        
        if (_showToolHistory)
        {
            RenderToolHistoryPanel(mainX + mainWidth + 10, 0, 290, (int)mainHeight);
        }
    }

    private string GetCurrentTabTitle() => _currentTab switch
    {
        1 => "Running Apps",
        2 => "Open Windows",
        3 => "Tool History",
        4 => "Settings",
        _ => $"Session: {_sessionStore.CurrentSession?.Title ?? "New Session"}",
    };

    private void RenderCurrentTab(float mainWidth, float mainHeight)
    {
        switch (_currentTab)
        {
            case 1:
                RenderAppsTab(mainWidth, mainHeight);
                break;
            case 2:
                RenderWindowsTab(mainWidth, mainHeight);
                break;
            case 3:
                RenderHistoryTab(mainWidth, mainHeight);
                break;
            case 4:
                RenderSettingsTab(mainWidth, mainHeight);
                break;
            default:
                RenderChatTab(mainWidth, mainHeight);
                break;
        }
    }

    private void RenderChatTab(float mainWidth, float mainHeight)
    {
        ImGui.SetCursorPos(new Vector2(20, 80));
        float chatHeight = mainHeight - 180;
        ImGui.BeginChild("##chat", new Vector2(Math.Max(1, mainWidth - 40), Math.Max(1, chatHeight)), ImGuiChildFlags.Borders);
        {
            foreach (var msg in Snapshot(ChatMessages))
            {
                RenderChatMessage(msg);
            }
        }
        ImGui.EndChild();

        ImGui.SetCursorPos(new Vector2(20, mainHeight - 90));
        ImGui.BeginChild("##inputarea", new Vector2(Math.Max(1, mainWidth - 120), 60), ImGuiChildFlags.Borders);
        {
            ImGui.InputTextMultiline("##input", ref _userInput, 500, new Vector2(Math.Max(1, mainWidth - 140), 50));
        }
        ImGui.EndChild();

        ImGui.SetCursorPos(new Vector2(mainWidth - 90, mainHeight - 85));
        if (ImGui.Button("Send", new Vector2(60, 25)) && !_isProcessing && !string.IsNullOrWhiteSpace(_userInput))
        {
            SendMessage();
        }

        if (_isProcessing)
        {
            ImGui.SetCursorPos(new Vector2(mainWidth - 90, mainHeight - 55));
            if (ImGui.Button("Cancel", new Vector2(60, 20)))
            {
                CancelTask();
            }
        }
    }

    private void RenderAppsTab(float mainWidth, float mainHeight)
    {
        ImGui.SetCursorPos(new Vector2(20, 80));
        if (ImGui.Button("Refresh Apps", new Vector2(120, 28)))
        {
            _ = Task.Run(LoadRunningApps);
        }

        ImGui.SetCursorPos(new Vector2(20, 120));
        ImGui.BeginChild("##apps", new Vector2(Math.Max(1, mainWidth - 40), Math.Max(1, mainHeight - 140)), ImGuiChildFlags.Borders);
        {
            foreach (var app in Snapshot(RunningApps))
            {
                var active = app.IsActive ? " *" : "";
                if (ImGui.Selectable($"{app.Name}{active}  pid:{app.ProcessId}  windows:{app.WindowCount}"))
                {
                    _ = Task.Run(() => _apps.ActivateApplicationAsync(app.Name));
                }
            }
        }
        ImGui.EndChild();
    }

    private void RenderWindowsTab(float mainWidth, float mainHeight)
    {
        ImGui.SetCursorPos(new Vector2(20, 80));
        if (ImGui.Button("Refresh Windows", new Vector2(140, 28)))
        {
            _ = Task.Run(LoadOpenWindows);
        }

        ImGui.SetCursorPos(new Vector2(20, 120));
        ImGui.BeginChild("##windows", new Vector2(Math.Max(1, mainWidth - 40), Math.Max(1, mainHeight - 140)), ImGuiChildFlags.Borders);
        {
            foreach (var window in Snapshot(OpenWindows))
            {
                var appName = window.ProcessName ?? "unknown";
                var marker = window.IsMainWindow ? " *" : "";
                if (ImGui.Selectable($"{window.Title}{marker}  [{appName}]"))
                {
                    _ = Task.Run(() => _windows.FocusWindowAsync(new WindowTarget.Title(window.Title)));
                }
            }
        }
        ImGui.EndChild();
    }

    private void RenderHistoryTab(float mainWidth, float mainHeight)
    {
        ImGui.SetCursorPos(new Vector2(20, 80));
        ImGui.BeginChild("##historytab", new Vector2(Math.Max(1, mainWidth - 40), Math.Max(1, mainHeight - 100)), ImGuiChildFlags.Borders);
        {
            foreach (var tool in Snapshot(ToolHistory))
            {
                RenderToolEntry(tool);
            }
        }
        ImGui.EndChild();
    }

    private void RenderSettingsTab(float mainWidth, float mainHeight)
    {
        ImGui.SetCursorPos(new Vector2(20, 80));
        ImGui.BeginChild("##settingstab", new Vector2(Math.Max(1, mainWidth - 40), Math.Max(1, mainHeight - 100)), ImGuiChildFlags.Borders);
        {
            ImGui.Text($"Provider: {_settings.SelectedProvider}");
            ImGui.Text($"Model: {_settings.SelectedModel}");
            ImGui.Text(PermissionStatus);
            ImGui.Separator();
            ImGui.TextWrapped("Edit provider settings from the WPF settings window for now.");
        }
        ImGui.EndChild();
    }

    private List<T> Snapshot<T>(IEnumerable<T> items)
    {
        lock (_stateLock)
        {
            return items.ToList();
        }
    }

    private void RenderChatMessage(ChatMessageEntry msg)
    {
        Vector4 bgColor, textColor;
        
        switch (msg.Role)
        {
            case "user":
                bgColor = new Vector4(0f, 0.35f, 0.62f, 1f);
                textColor = new Vector4(1f, 1f, 1f, 1f);
                break;
            case "assistant":
                bgColor = new Vector4(0.176f, 0.176f, 0.188f, 1f);
                textColor = new Vector4(0.83f, 0.83f, 0.83f, 1f);
                break;
            case "tool":
                bgColor = new Vector4(0.08f, 0.08f, 0.1f, 1f);
                textColor = new Vector4(0.31f, 0.78f, 0.69f, 1f);
                break;
            default:
                bgColor = new Vector4(0.24f, 0.24f, 0.26f, 1f);
                textColor = new Vector4(0.83f, 0.65f, 0.17f, 1f);
                break;
        }
        
        bool isUser = msg.Role == "user";
        float maxWidth = Math.Min(600, Math.Max(160, ImGui.GetWindowWidth() - 40));
        float startX = isUser ? Math.Max(20, ImGui.GetWindowWidth() - maxWidth - 20) : 20;
        int estimatedLines = Math.Max(1, (msg.Content?.Length ?? 0) / 72 + 1);
        float height = Math.Max(44, estimatedLines * ImGui.GetTextLineHeightWithSpacing() + 20);

        ImGui.SetCursorPosX(startX);
        ImGui.PushID($"{msg.Role}-{msg.Timestamp.ToUnixTimeMilliseconds()}-{(msg.Content?.GetHashCode() ?? 0)}");
        ImGui.PushStyleColor(ImGuiCol.ChildBg, bgColor);
        ImGui.PushStyleColor(ImGuiCol.Text, textColor);
        ImGui.BeginChild("##message", new Vector2(maxWidth, height), ImGuiChildFlags.None);
        {
            ImGui.SetCursorPos(new Vector2(10, 8));
            ImGui.PushTextWrapPos(maxWidth - 20);
            ImGui.TextUnformatted(msg.Content);
            ImGui.PopTextWrapPos();
        }
        ImGui.EndChild();
        ImGui.PopStyleColor(2);
        ImGui.PopID();
    }

    private void RenderToolHistoryPanel(float x, float y, float width, int height)
    {
        ImGui.SetNextWindowSize(new Vector2(width, height));
        ImGui.SetNextWindowPos(new Vector2(x, y));
        
        ImGui.Begin("##toolhistory", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse);
        {
            var drawList = ImGui.GetWindowDrawList();
            var pos = ImGui.GetWindowPos();
            
            drawList.AddRectFilled(
                pos,
                new Vector2(pos.X + width, pos.Y + 50),
                ImGui.GetColorU32(new Vector4(0.137f, 0.141f, 0.161f, 1f)));
            
            ImGui.SetCursorPos(new Vector2(10, 15));
            ImGui.Text("Tool History");
            
            ImGui.SetCursorPos(new Vector2(width - 30, 15));
            if (ImGui.Button("X", new Vector2(20, 20)))
            {
                _showToolHistory = false;
            }
            
            ImGui.SetCursorPos(new Vector2(10, 60));
            ImGui.BeginChild("##toollist", new Vector2(width - 20, height - 70), ImGuiChildFlags.Borders);
            {
                foreach (var tool in Snapshot(ToolHistory))
                {
                    RenderToolEntry(tool);
                }
            }
            ImGui.EndChild();
        }
        ImGui.End();
    }

    private void RenderToolEntry(ToolExecutionEntry tool)
    {
        Vector4 statusColor = tool.IsRunning 
            ? new Vector4(0.83f, 0.65f, 0.17f, 1f)
            : tool.IsSuccess 
                ? new Vector4(0.31f, 0.78f, 0.69f, 1f)
                : new Vector4(0.96f, 0.28f, 0.28f, 1f);
        
        string statusIcon = tool.IsRunning ? "..." : (tool.IsSuccess ? "OK" : "X");
        
        RenderText(statusIcon, statusColor);
        ImGui.SameLine();
        ImGui.TextUnformatted(tool.ToolName);
        
        if (!string.IsNullOrEmpty(tool.Arguments))
        {
            ImGui.SetCursorPos(new Vector2(ImGui.GetCursorPos().X + 10, ImGui.GetCursorPos().Y));
            RenderText(tool.Arguments, new Vector4(0.5f, 0.5f, 0.5f, 1f));
        }

        if (!string.IsNullOrEmpty(tool.Result))
        {
            ImGui.SetCursorPos(new Vector2(ImGui.GetCursorPos().X + 10, ImGui.GetCursorPos().Y));
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + Math.Max(120, ImGui.GetContentRegionAvail().X - 10));
            RenderText(tool.Result, new Vector4(0.68f, 0.72f, 0.76f, 1f));
            ImGui.PopTextWrapPos();
        }
        
        ImGui.Separator();
    }

    private static void RenderText(string text, Vector4 color)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    private void SendMessage()
    {
        if (_isProcessing || string.IsNullOrWhiteSpace(_userInput)) return;

        var text = _userInput.Trim();
        _userInput = "";

        AddChatMessage(new ChatMessageEntry("user", text, DateTimeOffset.Now));
        _sessionStore.AddMessage(new ConversationMessage("user", text));

        if (_sessionStore.CurrentSession?.Title == "New Session")
        {
            var title = text.Length > 30 ? text.Substring(0, 27) + "..." : text;
            _sessionStore.UpdateTitle(title);
        }

        _taskCts = new CancellationTokenSource();
        _isProcessing = true;

        AddChatMessage(new ChatMessageEntry("system", "Thinking...", DateTimeOffset.Now));

        _ = ExecuteTaskAsync(text, _taskCts.Token);
    }

    private async Task ExecuteTaskAsync(string task, CancellationToken ct)
    {
        try
        {
            var history = _sessionStore.CurrentSession?.Messages ?? new List<ConversationMessage>();
            var result = await _agent.ExecuteTaskAsync(task, history, OnAgentEvent, ct);

            RemoveThinkingMessage();

            AddChatMessage(new ChatMessageEntry("assistant", result, DateTimeOffset.Now));
            _sessionStore.AddMessage(new ConversationMessage("assistant", result));
        }
        catch (OperationCanceledException)
        {
            RemoveThinkingMessage();
            AddChatMessage(new ChatMessageEntry("system", "Task was cancelled.", DateTimeOffset.Now));
        }
        catch (Exception ex)
        {
            RemoveThinkingMessage();
            AddChatMessage(new ChatMessageEntry("system", $"Error: {ex.Message}", DateTimeOffset.Now));
        }
        finally
        {
            _isProcessing = false;
            _taskCts?.Dispose();
            _taskCts = null;
        }
    }

    private void OnAgentEvent(AgentEvent evt)
    {
        switch (evt.Kind)
        {
            case AgentEventKind.Thinking:
                break;

            case AgentEventKind.ToolCallStarted:
                AddToolEntry(new ToolExecutionEntry(
                    evt.ToolName ?? "unknown", evt.ToolArgs, null, true, false, null));
                AddChatMessage(new ChatMessageEntry("tool",
                    $"{evt.ToolName}: {evt.ToolArgs}",
                    DateTimeOffset.Now));
                break;

            case AgentEventKind.ToolCallCompleted:
                CompleteLastToolEntry(evt);
                if (!string.IsNullOrWhiteSpace(evt.ToolResult))
                {
                    AddChatMessage(new ChatMessageEntry("tool", evt.ToolResult, DateTimeOffset.Now));
                }
                break;

            case AgentEventKind.Error:
                AddChatMessage(new ChatMessageEntry("system", $"Error: {evt.Content}", DateTimeOffset.Now));
                break;
        }
    }

    private void AddChatMessage(ChatMessageEntry message)
    {
        lock (_stateLock)
        {
            ChatMessages.Add(message);
        }
    }

    private void AddToolEntry(ToolExecutionEntry tool)
    {
        lock (_stateLock)
        {
            ToolHistory.Add(tool);
        }
    }

    private void RemoveThinkingMessage()
    {
        lock (_stateLock)
        {
            var thinkingIdx = ChatMessages.ToList().FindIndex(m => m.Role == "system" && m.Content == "Thinking...");
            if (thinkingIdx >= 0) ChatMessages.RemoveAt(thinkingIdx);
        }
    }

    private void CompleteLastToolEntry(AgentEvent evt)
    {
        lock (_stateLock)
        {
            if (ToolHistory.Count == 0) return;

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
    }

    private void CancelTask()
    {
        _taskCts?.Cancel();
        _agent.Cancel();
    }

    private void PushStyleVars()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 1);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 0);
        
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0, 0, 0, 0));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 1, 1, 1));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, new Vector4(0.137f, 0.137f, 0.137f, 0));
        ImGui.PushStyleColor(ImGuiCol.PopupBg, new Vector4(0.137f, 0.137f, 0.137f, 0));
    }

    private void PopStyleVars()
    {
        ImGui.PopStyleVar(6);
        ImGui.PopStyleColor(4);
    }
}
