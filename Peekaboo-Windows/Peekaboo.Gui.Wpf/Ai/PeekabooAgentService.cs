using System.Text;
using System.Text.Json;
using Peekaboo.Core;
using Peekaboo.Gui.Wpf.Sessions;

namespace Peekaboo.Gui.Wpf.Ai;

/// <summary>
/// The Peekaboo agent: receives natural language tasks, uses AI to plan and execute
/// tool calls, and feeds results back until the task is complete.
/// </summary>
public class PeekabooAgentService : IAgentService
{
    private readonly AiSettings _settings;
    private readonly IScreenCaptureService _capture;
    private readonly IElementDetectionService _detection;
    private readonly IInputService _input;
    private readonly IApplicationService _apps;
    private readonly IWindowManagementService _windows;
    private readonly IClipboardService _clipboard;
    private readonly IPermissionsService _permissions;
    private readonly IMenuDiscoveryService _menu;
    private readonly ITaskbarService _taskbar;
    private readonly IDialogService _dialog;
    private readonly IVirtualDesktopService _virtualDesktop;
    private CancellationTokenSource? _cts;

    public bool IsProcessing => _cts != null && !_cts.IsCancellationRequested;

    public async Task<string> ExecuteTaskAsync(
        string task,
        IReadOnlyList<ConversationMessage> history,
        Action<AgentEvent> onEvent,
        CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var linkedCt = _cts.Token;

        try
        {
            var provider = AiProviderFactory.Create(_settings);
            var conversation = new List<ChatMessage>();

            // System prompt
            conversation.Add(new ChatMessage(ChatRole.System, BuildSystemPrompt()));
            conversation.Add(new ChatMessage(ChatRole.System, BuildToolDefinitions()));

            // Add conversation history (skip system messages, limit to last 20 messages)
            var recentHistory = history
                .Where(m => m.Role != "system")
                .TakeLast(20)
                .ToList();

            foreach (var msg in recentHistory)
            {
                var role = msg.Role switch
                {
                    "user" => ChatRole.User,
                    "assistant" => ChatRole.Assistant,
                    "system" => ChatRole.System,
                    _ => ChatRole.User
                };
                conversation.Add(new ChatMessage(role, msg.Content));
            }

            // Add current user task
            conversation.Add(new ChatMessage(ChatRole.User, task));

            string lastAssistantResponse = string.Empty;
            int step = 0;
            var maxSteps = _settings.MaxSteps;

            while (step < maxSteps)
            {
                linkedCt.ThrowIfCancellationRequested();

                onEvent(new AgentEvent(AgentEventKind.Thinking, Content: $"Step {step + 1}/{maxSteps}..."));

                var response = await provider.ChatAsync(conversation.ToArray(), linkedCt);
                lastAssistantResponse = response.Content;
                conversation.Add(new ChatMessage(ChatRole.Assistant, response.Content));

                // Parse tool calls from response
                var toolCall = ParseToolCall(response.Content);
                if (toolCall == null)
                {
                    // No tool call - task complete
                    onEvent(new AgentEvent(AgentEventKind.Completed, Content: response.Content));
                    return response.Content;
                }

                // Execute tool
                var startTime = DateTime.Now;
                onEvent(new AgentEvent(AgentEventKind.ToolCallStarted, toolCall.Name, toolCall.Arguments));

                string result;
                try
                {
                    result = await ExecuteToolAsync(toolCall.Name, toolCall.Arguments, linkedCt);
                }
                catch (Exception ex)
                {
                    result = $"Error: {ex.Message}";
                    onEvent(new AgentEvent(AgentEventKind.Error, toolCall.Name, toolCall.Arguments, result));
                    conversation.Add(new ChatMessage(ChatRole.Assistant, $"[TOOL_ERROR:{toolCall.Name}]\n{ex.Message}"));
                    step++;
                    continue;
                }

                var duration = DateTime.Now - startTime;
                onEvent(new AgentEvent(AgentEventKind.ToolCallCompleted, toolCall.Name, toolCall.Arguments, result, duration.ToString()));

                // Add tool result to conversation
                conversation.Add(new ChatMessage(ChatRole.Assistant, $"[TOOL_RESULT:{toolCall.Name}]\n{result}"));
                step++;
            }

            onEvent(new AgentEvent(AgentEventKind.Error, Content: "Max steps reached"));
            return lastAssistantResponse;
        }
        catch (OperationCanceledException)
        {
            onEvent(new AgentEvent(AgentEventKind.Cancelled));
            throw;
        }
        catch (Exception ex)
        {
            onEvent(new AgentEvent(AgentEventKind.Error, Content: ex.Message));
            throw;
        }
    }

    public PeekabooAgentService(
        AiSettings settings,
        IScreenCaptureService capture,
        IElementDetectionService detection,
        IInputService input,
        IApplicationService apps,
        IWindowManagementService windows,
        IClipboardService clipboard,
        IPermissionsService permissions,
        IMenuDiscoveryService menu,
        ITaskbarService taskbar,
        IDialogService dialog,
        IVirtualDesktopService virtualDesktop)
    {
        _settings = settings;
        _capture = capture;
        _detection = detection;
        _input = input;
        _apps = apps;
        _windows = windows;
        _clipboard = clipboard;
        _permissions = permissions;
        _menu = menu;
        _taskbar = taskbar;
        _dialog = dialog;
        _virtualDesktop = virtualDesktop;
    }

    public void Cancel() => _cts?.Cancel();

    public async Task<string> ExecuteTaskAsync(string task, Action<AgentEvent> onEvent, CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var linkedCt = _cts.Token;

        try
        {
            var provider = AiProviderFactory.Create(_settings);
            var conversation = new List<ChatMessage>();

            // System prompt
            conversation.Add(new ChatMessage(ChatRole.System, BuildSystemPrompt()));

            // Tool reference (as text, since we're not using function calling)
            conversation.Add(new ChatMessage(ChatRole.System, BuildToolDefinitions()));

            // User task
            conversation.Add(new ChatMessage(ChatRole.User, task));

            string lastAssistantResponse = string.Empty;
            int step = 0;
            var maxSteps = _settings.MaxSteps;

            while (step < maxSteps)
            {
                linkedCt.ThrowIfCancellationRequested();
                step++;

                // Call AI
                onEvent(new AgentEvent(AgentEventKind.Thinking, Content: "Thinking..."));
                ChatResponse response;
                try
                {
                    response = await provider.ChatAsync(conversation.ToArray(), linkedCt);
                }
                catch (OperationCanceledException)
                {
                    onEvent(new AgentEvent(AgentEventKind.Cancelled));
                    return "Task was cancelled.";
                }
                catch (Exception ex)
                {
                    onEvent(new AgentEvent(AgentEventKind.Error, Content: ex.Message));
                    return $"AI Error: {ex.Message}";
                }

                lastAssistantResponse = response.Content;
                conversation.Add(new ChatMessage(ChatRole.Assistant, response.Content));

                // Check for tool call in response
                var toolCall = ParseToolCall(response.Content);
                if (toolCall == null)
                {
                    // No tool call = task complete
                    onEvent(new AgentEvent(AgentEventKind.Completed,
                        Content: response.Content,
                        InputTokens: response.InputTokens,
                        OutputTokens: response.OutputTokens));
                    return response.Content;
                }

                // Execute tool
                onEvent(new AgentEvent(AgentEventKind.ToolCallStarted,
                    ToolName: toolCall.Name, ToolArgs: toolCall.Arguments));

                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var result = await ExecuteToolAsync(toolCall.Name, toolCall.Arguments, linkedCt);
                    sw.Stop();
                    conversation.Add(new ChatMessage(ChatRole.Assistant, $"[TOOL_RESULT:{toolCall.Name}]\n{result}"));
                    onEvent(new AgentEvent(AgentEventKind.ToolCallCompleted,
                        ToolName: toolCall.Name, ToolResult: result, Duration: sw.Elapsed));
                }
                catch (OperationCanceledException)
                {
                    onEvent(new AgentEvent(AgentEventKind.Cancelled));
                    return "Task was cancelled.";
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    var errorMsg = $"Error: {ex.Message}";
                    conversation.Add(new ChatMessage(ChatRole.Assistant, $"[TOOL_ERROR:{toolCall.Name}]\n{errorMsg}"));
                    onEvent(new AgentEvent(AgentEventKind.ToolCallCompleted,
                        ToolName: toolCall.Name, ToolResult: errorMsg, Duration: sw.Elapsed));
                }
            }

            onEvent(new AgentEvent(AgentEventKind.Error, Content: $"Max steps ({maxSteps}) reached. Task may be incomplete."));
            return lastAssistantResponse + $"\n\n[Note: Maximum steps ({maxSteps}) reached.]";
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    private static string BuildSystemPrompt() => """
You are Peekaboo, a powerful Windows desktop automation assistant. You can see the screen, detect UI elements, click, type, use hotkeys, scroll, drag, manage applications and windows, and use the clipboard.

AVAILABLE CAPABILITIES:
- Screen capture and UI element detection
- Mouse clicks, typing, hotkeys, scrolling, dragging
- Application launching, quitting, and management
- Window management (list, focus, close, minimize, maximize, move, resize)
- Clipboard operations (get, set, clear)
- Menu interaction (list and click menu items)
- Taskbar interaction (list, click, hide, show taskbar items)
- Dialog handling (set path, set filter, click buttons)

CORE PRINCIPLES:
1. Think step-by-step - break complex tasks into manageable steps
2. Verify each action - check the result before proceeding to the next step
3. Use the right tool - prefer element-based actions over coordinates when possible
4. Be efficient - minimize unnecessary screen captures and actions
5. Handle errors gracefully - if one approach fails, try an alternative

TROUBLESHOOTING:
- If clicking fails, try using the element ID first, then coordinates
- If typing fails, try clicking the text field first to focus it
- If an app doesn't respond, try activating it first with activate_app
- For file dialogs, set the path before clicking Open/Save

OUTPUT FORMAT:
To call a tool, output EXACTLY this format (nothing else on that line):
[TOOL_CALL:tool_name]
{"arg1": "value1", "arg2": "value2"}
[END_TOOL_CALL]

When you complete a task successfully, respond with a brief summary of what was accomplished.
If you cannot complete a task, explain why and suggest what the user could try instead.
""";

    private static string BuildToolDefinitions() => """
AVAILABLE TOOLS:

1. capture_screen - Capture the entire screen
   Args: none
   Returns: Screenshot saved, dimensions

2. capture_window - Capture a specific application window
   Args: {"app": "application name"}
   Returns: Screenshot of the window

3. capture_frontmost - Capture the frontmost window
   Args: none
   Returns: Screenshot of frontmost window

4. see - Detect UI elements on the current screen
   Args: {"app": "optional app name", "window": "optional window title"}
   Returns: List of detected elements with IDs (B1, T2, etc.), types, labels, and bounds

5. click - Click at coordinates or on an element
   Args: {"x": 100, "y": 200} OR {"element_id": "B1"}
   Optional: {"type": "single"|"double"|"right"}
   Returns: Click confirmation

6. type_text - Type text
   Args: {"text": "Hello World"}
   Optional: {"clear": true}
   Returns: Characters typed

7. hotkey - Press hotkey combination
   Args: {"keys": "ctrl,c"}
   Returns: Hotkey pressed

8. scroll - Scroll in a direction
   Args: {"direction": "up"|"down"|"left"|"right"}
   Optional: {"amount": 3}
   Returns: Scroll confirmation

9. drag - Drag from one point to another
   Args: {"from_x": 0, "from_y": 0, "to_x": 100, "to_y": 100}
   Returns: Drag confirmation

10. list_apps - List running applications
    Args: none
    Returns: List of running apps with names and PIDs

11. find_app - Find an application
    Args: {"name": "notepad"}
    Returns: App info

12. launch_app - Launch an application
    Args: {"name": "notepad"}
    Returns: Launched app info

13. quit_app - Quit an application
    Args: {"name": "notepad"}
    Optional: {"force": true}
    Returns: Quit confirmation

14. activate_app - Activate (focus) an application
    Args: {"name": "notepad"}
    Returns: Activation confirmation

15. list_windows - List windows
    Args: none (or {"app": "notepad"} to filter)
    Returns: List of windows with titles and bounds

16. focus_window - Focus a window
    Args: {"app": "notepad"} or {"title": "window title"}
    Returns: Focus confirmation

17. close_window - Close a window
    Args: {"app": "notepad"} or {"title": "window title"}
    Returns: Close confirmation

18. minimize_window - Minimize a window
    Args: {"app": "notepad"}
    Returns: Minimize confirmation

19. maximize_window - Maximize a window
    Args: {"app": "notepad"}
    Returns: Maximize confirmation

20. move_window - Move a window
    Args: {"app": "notepad", "x": 0, "y": 0}
    Returns: Move confirmation

21. resize_window - Resize a window
    Args: {"app": "notepad", "width": 800, "height": 600}
    Returns: Resize confirmation

22. clipboard_get - Get clipboard text
    Args: none
    Returns: Clipboard text

23. clipboard_set - Set clipboard text
    Args: {"text": "Hello"}
    Returns: Confirmation

24. clipboard_clear - Clear clipboard
    Args: none
    Returns: Confirmation

25. permissions - Check automation permissions
    Args: none
    Returns: Permission status

26. menu_list - List menu items for an application
    Args: {"app": "optional app name", "system_menu": false}
    Returns: List of menu items

27. menu_click - Click a menu item by path
    Args: {"path": "File > Save As", "app": "optional app name"}
    Returns: Click confirmation

28. taskbar_list - List taskbar items (running windows)
    Args: none
    Returns: List of taskbar items with IDs

29. taskbar_click - Click a taskbar item
    Args: {"item": "taskbar_0" or "window title"}
    Returns: Click confirmation

30. taskbar_hide - Hide a taskbar item (minimize to tray)
    Args: {"item": "taskbar_0"}
    Returns: Hide confirmation

31. taskbar_show - Show a taskbar item (restore)
    Args: {"item": "taskbar_0"}
    Returns: Show confirmation

32. dialog_set_path - Set file path in dialog
    Args: {"path": "C:\\Users\\file.txt"}
    Returns: Path set confirmation

33. dialog_set_filter - Set file type filter
    Args: {"filter": "Text Files|*.txt|All Files|*.*"}
    Returns: Filter set confirmation

34. dialog_click_button - Click dialog button
    Args: {"button": "Open"|"Save"|"Cancel"}
    Returns: Button click confirmation

35. space_list - List virtual desktops
    Args: none
    Returns: List of desktops with names and IDs

36. space_switch - Switch to a virtual desktop
    Args: {"desktop": "desktop-1"}
    Returns: Switch confirmation

37. space_create - Create a new virtual desktop
    Args: none
    Returns: New desktop ID

38. space_move_window - Move a window to a virtual desktop
    Args: {"window": "window title", "desktop": "desktop-1"}
    Returns: Move confirmation
""";

    private static ToolCallRequest? ParseToolCall(string content)
    {
        var startIdx = content.IndexOf("[TOOL_CALL:", StringComparison.Ordinal);
        if (startIdx < 0) return null;

        var endIdx = content.IndexOf("]", startIdx + 11, StringComparison.Ordinal);
        if (endIdx < 0) return null;

        var toolName = content.Substring(startIdx + 11, endIdx - startIdx - 11);

        // Find JSON between [TOOL_CALL:...] and [END_TOOL_CALL]
        var jsonStart = content.IndexOf('\n', endIdx) + 1;
        var jsonEndMarker = content.IndexOf("[END_TOOL_CALL]", jsonStart, StringComparison.Ordinal);
        if (jsonEndMarker < 0) jsonEndMarker = content.Length;

        var jsonStr = content.Substring(jsonStart, jsonEndMarker - jsonStart).Trim();

        return new ToolCallRequest(toolName, jsonStr);
    }

    private async Task<string> ExecuteToolAsync(string toolName, string arguments, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(arguments);
        var args = doc.RootElement;

        return toolName switch
        {
            "capture_screen" => await CaptureScreenAsync(args, ct),
            "capture_window" => await CaptureWindowAsync(args, ct),
            "capture_frontmost" => await CaptureFrontmostAsync(ct),
            "see" => await SeeAsync(args, ct),
            "click" => await ClickAsync(args, ct),
            "type_text" => await TypeTextAsync(args, ct),
            "hotkey" => await HotkeyAsync(args, ct),
            "scroll" => await ScrollAsync(args, ct),
            "drag" => await DragAsync(args, ct),
            "list_apps" => await ListAppsAsync(ct),
            "find_app" => await FindAppAsync(args, ct),
            "launch_app" => await LaunchAppAsync(args, ct),
            "quit_app" => await QuitAppAsync(args, ct),
            "activate_app" => await ActivateAppAsync(args, ct),
            "list_windows" => await ListWindowsAsync(args, ct),
            "focus_window" => await FocusWindowAsync(args, ct),
            "close_window" => await CloseWindowAsync(args, ct),
            "minimize_window" => await MinimizeWindowAsync(args, ct),
            "maximize_window" => await MaximizeWindowAsync(args, ct),
            "move_window" => await MoveWindowAsync(args, ct),
            "resize_window" => await ResizeWindowAsync(args, ct),
            "clipboard_get" => await ClipboardGetAsync(ct),
            "clipboard_set" => await ClipboardSetAsync(args, ct),
            "clipboard_clear" => await ClipboardClearAsync(ct),
            "permissions" => await PermissionsAsync(ct),
            "menu_list" => await MenuListAsync(args, ct),
            "menu_click" => await MenuClickAsync(args, ct),
            "taskbar_list" => await TaskbarListAsync(ct),
            "taskbar_click" => await TaskbarClickAsync(args, ct),
            "taskbar_hide" => await TaskbarHideAsync(args, ct),
            "taskbar_show" => await TaskbarShowAsync(args, ct),
            "dialog_set_path" => await DialogSetPathAsync(args, ct),
            "dialog_set_filter" => await DialogSetFilterAsync(args, ct),
            "dialog_click_button" => await DialogClickButtonAsync(args, ct),
            "space_list" => await SpaceListAsync(ct),
            "space_switch" => await SpaceSwitchAsync(args, ct),
            "space_create" => await SpaceCreateAsync(ct),
            "space_move_window" => await SpaceMoveWindowAsync(args, ct),
            _ => $"Unknown tool: {toolName}",
        };
    }

    private async Task<string> CaptureScreenAsync(JsonElement args, CancellationToken ct)
    {
        int? display = args.TryGetProperty("display", out var d) ? d.GetInt32() : null;
        var result = await _capture.CaptureScreenAsync(display, ct);
        return $"Screen captured: {result.Metadata.Size.Width}x{result.Metadata.Size.Height}, {result.ImageData.Length} bytes";
    }

    private async Task<string> CaptureWindowAsync(JsonElement args, CancellationToken ct)
    {
        var app = args.GetProperty("app").GetString()!;
        var result = await _capture.CaptureWindowAsync(app, null, ct);
        return $"Window '{app}' captured: {result.Metadata.Size.Width}x{result.Metadata.Size.Height}";
    }

    private async Task<string> CaptureFrontmostAsync(CancellationToken ct)
    {
        var result = await _capture.CaptureFrontmostAsync(ct);
        return $"Frontmost window captured: {result.Metadata.Size.Width}x{result.Metadata.Size.Height}";
    }

    private async Task<string> SeeAsync(JsonElement args, CancellationToken ct)
    {
        var shot = await _capture.CaptureFrontmostAsync(ct);
        string? app = args.TryGetProperty("app", out var a) ? a.GetString() : null;
        string? window = args.TryGetProperty("window", out var w) ? w.GetString() : null;
        var wCtx = (app != null || window != null) ? new WindowContext(ApplicationName: app, WindowTitle: window) : null;
        var result = await _detection.DetectElementsAsync(shot.ImageData, wCtx, ct);

        var sb = new StringBuilder();
        sb.AppendLine($"Detected {result.Metadata.ElementCount} elements:");
        foreach (var el in result.Elements.Buttons)
            sb.AppendLine($"  [{el.Id}] Button: \"{el.Label}\" at ({el.Bounds.X},{el.Bounds.Y},{el.Bounds.Width}x{el.Bounds.Height})");
        foreach (var el in result.Elements.TextFields)
            sb.AppendLine($"  [{el.Id}] TextField: \"{el.Label}\" at ({el.Bounds.X},{el.Bounds.Y},{el.Bounds.Width}x{el.Bounds.Height})");
        foreach (var el in result.Elements.Links)
            sb.AppendLine($"  [{el.Id}] Link: \"{el.Label}\" at ({el.Bounds.X},{el.Bounds.Y},{el.Bounds.Width}x{el.Bounds.Height})");
        foreach (var el in result.Elements.Images)
            sb.AppendLine($"  [{el.Id}] Image at ({el.Bounds.X},{el.Bounds.Y},{el.Bounds.Width}x{el.Bounds.Height})");
        foreach (var el in result.Elements.Groups)
            sb.AppendLine($"  [{el.Id}] Group: \"{el.Label}\" at ({el.Bounds.X},{el.Bounds.Y},{el.Bounds.Width}x{el.Bounds.Height})");
        foreach (var el in result.Elements.Other)
            sb.AppendLine($"  [{el.Id}] Other: \"{el.Label}\" at ({el.Bounds.X},{el.Bounds.Y},{el.Bounds.Width}x{el.Bounds.Height})");
        return sb.ToString();
    }

    private async Task<string> ClickAsync(JsonElement args, CancellationToken ct)
    {
        ClickTarget target;
        if (args.TryGetProperty("element_id", out var eid))
            target = new ClickTarget.ElementId(eid.GetString()!);
        else if (args.TryGetProperty("x", out var x) && args.TryGetProperty("y", out var y))
            target = new ClickTarget.Coordinates(new Point(x.GetDouble(), y.GetDouble()));
        else
            return "Error: provide element_id or x/y coordinates";

        var clickType = args.TryGetProperty("type", out var t) && t.GetString() == "double" ? ClickType.Double :
                        args.TryGetProperty("type", out var t2) && t2.GetString() == "right" ? ClickType.Right : ClickType.Single;

        await _input.ClickAsync(target, clickType, ct);
        return $"Clicked ({clickType})";
    }

    private async Task<string> TypeTextAsync(JsonElement args, CancellationToken ct)
    {
        var text = args.GetProperty("text").GetString()!;
        var clear = args.TryGetProperty("clear", out var c) && c.GetBoolean();
        await _input.TypeAsync(text, clearExisting: clear, ct: ct);
        return $"Typed {text.Length} characters";
    }

    private async Task<string> HotkeyAsync(JsonElement args, CancellationToken ct)
    {
        var keys = args.GetProperty("keys").GetString()!;
        await _input.HotkeyAsync(keys, ct: ct);
        return $"Pressed {keys}";
    }

    private async Task<string> ScrollAsync(JsonElement args, CancellationToken ct)
    {
        var dirStr = args.GetProperty("direction").GetString()!.ToLowerInvariant();
        var dir = dirStr switch
        {
            "up" => ScrollDirection.Up,
            "down" => ScrollDirection.Down,
            "left" => ScrollDirection.Left,
            "right" => ScrollDirection.Right,
            _ => ScrollDirection.Down,
        };
        var amount = args.TryGetProperty("amount", out var a) ? a.GetInt32() : 3;
        await _input.ScrollAsync(dir, amount, ct: ct);
        return $"Scrolled {dir} ({amount} notches)";
    }

    private async Task<string> DragAsync(JsonElement args, CancellationToken ct)
    {
        var fx = args.GetProperty("from_x").GetDouble();
        var fy = args.GetProperty("from_y").GetDouble();
        var tx = args.GetProperty("to_x").GetDouble();
        var ty = args.GetProperty("to_y").GetDouble();
        await _input.DragAsync(new Point(fx, fy), new Point(tx, ty), ct: ct);
        return $"Dragged ({fx},{fy}) -> ({tx},{ty})";
    }

    private async Task<string> ListAppsAsync(CancellationToken ct)
    {
        var apps = await _apps.ListApplicationsAsync(ct);
        var sb = new StringBuilder($"Running applications ({apps.Count}):\n");
        foreach (var a in apps)
            sb.AppendLine($"  [{a.ProcessId}] {a.Name}");
        return sb.ToString();
    }

    private async Task<string> FindAppAsync(JsonElement args, CancellationToken ct)
    {
        var name = args.GetProperty("name").GetString()!;
        var app = await _apps.FindApplicationAsync(name, ct);
        return $"Found: {app.Name} (PID {app.ProcessId})";
    }

    private async Task<string> LaunchAppAsync(JsonElement args, CancellationToken ct)
    {
        var name = args.GetProperty("name").GetString()!;
        var app = await _apps.LaunchApplicationAsync(name, ct);
        return $"Launched {app.Name} (PID {app.ProcessId})";
    }

    private async Task<string> QuitAppAsync(JsonElement args, CancellationToken ct)
    {
        var name = args.GetProperty("name").GetString()!;
        var force = args.TryGetProperty("force", out var f) && f.GetBoolean();
        var ok = await _apps.QuitApplicationAsync(name, force, ct);
        return ok ? $"Quit {name}" : $"Failed to quit {name}";
    }

    private async Task<string> ActivateAppAsync(JsonElement args, CancellationToken ct)
    {
        var name = args.GetProperty("name").GetString()!;
        await _apps.ActivateApplicationAsync(name, ct);
        return $"Activated {name}";
    }

    private async Task<string> ListWindowsAsync(JsonElement args, CancellationToken ct)
    {
        WindowTarget target = args.TryGetProperty("app", out var a)
            ? new WindowTarget.Application(a.GetString()!)
            : new WindowTarget.Frontmost();
        var windows = await _windows.ListWindowsAsync(target, ct);
        var sb = new StringBuilder($"Windows ({windows.Count}):\n");
        foreach (var w in windows)
            sb.AppendLine($"  [{w.ProcessId}] {w.Title} ({w.Bounds.Width}x{w.Bounds.Height})");
        return sb.ToString();
    }

    private async Task<string> FocusWindowAsync(JsonElement args, CancellationToken ct)
    {
        var target = ParseWindowTarget(args);
        await _windows.FocusWindowAsync(target, ct);
        return "Window focused";
    }

    private async Task<string> CloseWindowAsync(JsonElement args, CancellationToken ct)
    {
        var target = ParseWindowTarget(args);
        await _windows.CloseWindowAsync(target, ct);
        return "Window closed";
    }

    private async Task<string> MinimizeWindowAsync(JsonElement args, CancellationToken ct)
    {
        var target = ParseWindowTarget(args);
        await _windows.MinimizeWindowAsync(target, ct);
        return "Window minimized";
    }

    private async Task<string> MaximizeWindowAsync(JsonElement args, CancellationToken ct)
    {
        var target = ParseWindowTarget(args);
        await _windows.MaximizeWindowAsync(target, ct);
        return "Window maximized";
    }

    private async Task<string> MoveWindowAsync(JsonElement args, CancellationToken ct)
    {
        var target = ParseWindowTarget(args);
        var x = args.GetProperty("x").GetDouble();
        var y = args.GetProperty("y").GetDouble();
        await _windows.MoveWindowAsync(target, new Point(x, y), ct);
        return $"Window moved to ({x},{y})";
    }

    private async Task<string> ResizeWindowAsync(JsonElement args, CancellationToken ct)
    {
        var target = ParseWindowTarget(args);
        var w = args.GetProperty("width").GetDouble();
        var h = args.GetProperty("height").GetDouble();
        await _windows.ResizeWindowAsync(target, new Size(w, h), ct);
        return $"Window resized to {w}x{h}";
    }

    private async Task<string> ClipboardGetAsync(CancellationToken ct)
    {
        var text = await _clipboard.GetTextAsync(ct);
        return string.IsNullOrEmpty(text) ? "Clipboard is empty" : text;
    }

    private async Task<string> ClipboardSetAsync(JsonElement args, CancellationToken ct)
    {
        var text = args.GetProperty("text").GetString()!;
        await _clipboard.SetTextAsync(text, ct);
        return $"Set clipboard ({text.Length} chars)";
    }

    private async Task<string> ClipboardClearAsync(CancellationToken ct)
    {
        await _clipboard.SetTextAsync("", ct);
        return "Clipboard cleared";
    }

    private async Task<string> PermissionsAsync(CancellationToken ct)
    {
        var status = await _permissions.CheckAllAsync(ct);
        var sb = new StringBuilder();
        sb.AppendLine($"Administrator: {(status.IsAdministrator ? "Yes" : "No")}");
        sb.AppendLine($"UIAccess: {(status.HasUiAccess ? "Yes" : "No")}");
        sb.AppendLine($"Fully capable: {(status.IsFullyCapable ? "Yes" : "No")}");
        if (status.Warnings.Count > 0)
            foreach (var w in status.Warnings) sb.AppendLine($"  Warning: {w}");
        return sb.ToString();
    }

    private async Task<string> MenuListAsync(JsonElement args, CancellationToken ct)
    {
        string? app = args.TryGetProperty("app", out var a) ? a.GetString() : null;
        bool systemMenu = args.TryGetProperty("system_menu", out var s) && s.GetBoolean();

        var target = new MenuTarget(ApplicationName: app, UseSystemMenu: systemMenu);
        var items = await _menu.ListMenuItemsAsync(target, ct);

        if (items.Count == 0)
            return "No menu items found";

        var sb = new StringBuilder($"Menu items ({items.Count}):\n");
        foreach (var item in items)
        {
            var check = item.IsChecked ? "[x]" : "[ ]";
            var sub = item.HasSubmenu ? " ->" : "";
            sb.AppendLine($"  {check} {item.Label}{sub}");
        }
        return sb.ToString();
    }

    private async Task<string> TaskbarListAsync(CancellationToken ct)
    {
        var items = await _taskbar.ListTaskbarItemsAsync(ct);
        var sb = new StringBuilder($"Taskbar items ({items.Count}):\n");
        foreach (var item in items)
        {
            var active = item.IsActive ? "[ACTIVE] " : "";
            sb.AppendLine($"  [{item.Id}] {active}{item.Name} ({item.ProcessName})");
        }
        return sb.ToString();
    }

    private async Task<string> TaskbarClickAsync(JsonElement args, CancellationToken ct)
    {
        var itemId = args.GetProperty("item").GetString()!;
        await _taskbar.ClickTaskbarItemAsync(itemId, ct);
        return $"Clicked taskbar item: {itemId}";
    }

    private async Task<string> TaskbarHideAsync(JsonElement args, CancellationToken ct)
    {
        var itemId = args.GetProperty("item").GetString()!;
        await _taskbar.HideTaskbarItemAsync(itemId, ct);
        return $"Hidden taskbar item: {itemId}";
    }

    private async Task<string> TaskbarShowAsync(JsonElement args, CancellationToken ct)
    {
        var itemId = args.GetProperty("item").GetString()!;
        await _taskbar.ShowTaskbarItemAsync(itemId, ct);
        return $"Showed taskbar item: {itemId}";
    }

    private async Task<string> DialogSetPathAsync(JsonElement args, CancellationToken ct)
    {
        var path = args.GetProperty("path").GetString()!;
        await _dialog.SetPathAsync(path, ct);
        return $"Set dialog path: {path}";
    }

    private async Task<string> DialogSetFilterAsync(JsonElement args, CancellationToken ct)
    {
        var filter = args.GetProperty("filter").GetString()!;
        await _dialog.SetFilterAsync(filter, ct);
        return $"Set dialog filter: {filter}";
    }

    private async Task<string> DialogClickButtonAsync(JsonElement args, CancellationToken ct)
    {
        var buttonStr = args.GetProperty("button").GetString()!;
        var button = Enum.Parse<DialogButton>(buttonStr, true);
        await _dialog.ClickButtonAsync(button, ct);
        return $"Clicked dialog button: {buttonStr}";
    }

    private async Task<string> MenuClickAsync(JsonElement args, CancellationToken ct)
    {
        var path = args.GetProperty("path").GetString()!;
        string? app = args.TryGetProperty("app", out var a) ? a.GetString() : null;
        bool systemMenu = args.TryGetProperty("system_menu", out var s) && s.GetBoolean();

        var target = new MenuTarget(ApplicationName: app, UseSystemMenu: systemMenu);
        await _menu.ClickMenuItemAsync(target, path, ct);
        return $"Clicked menu path: {path}";
    }

    private async Task<string> SpaceListAsync(CancellationToken ct)
    {
        var desktops = await _virtualDesktop.ListDesktopsAsync(ct);
        var lines = desktops.Select((d, i) => $"  {i + 1}. {d.Name} ({(d.IsCurrent ? "current" : "not current")})");
        return "Virtual Desktops:\n" + string.Join("\n", lines);
    }

    private async Task<string> SpaceSwitchAsync(JsonElement args, CancellationToken ct)
    {
        var desktopId = args.GetProperty("desktop").GetString()!;
        await _virtualDesktop.SwitchToDesktopAsync(desktopId, ct);
        return $"Switched to desktop: {desktopId}";
    }

    private async Task<string> SpaceCreateAsync(CancellationToken ct)
    {
        var desktopId = await _virtualDesktop.CreateDesktopAsync(ct);
        return $"Created desktop: {desktopId}";
    }

    private async Task<string> SpaceMoveWindowAsync(JsonElement args, CancellationToken ct)
    {
        var window = args.GetProperty("window").GetString()!;
        var desktopId = args.GetProperty("desktop").GetString()!;
        await _virtualDesktop.MoveWindowToDesktopAsync(window, desktopId, ct);
        return $"Moved window '{window}' to desktop: {desktopId}";
    }

    private static WindowTarget ParseWindowTarget(JsonElement args)
    {
        if (args.TryGetProperty("app", out var a) && a.ValueKind == JsonValueKind.String)
            return new WindowTarget.Application(a.GetString()!);
        if (args.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String)
            return new WindowTarget.Title(t.GetString()!);
        return new WindowTarget.Frontmost();
    }
}
