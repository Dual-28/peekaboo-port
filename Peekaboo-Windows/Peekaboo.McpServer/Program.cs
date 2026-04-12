using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Peekaboo.Core;
using Peekaboo.Platform.Windows.Services;

namespace Peekaboo.McpServer;

[McpServerToolType]
public sealed class PeekabooTools
{
    private readonly IScreenCaptureService _capture;
    private readonly IElementDetectionService _detection;
    private readonly IInputService _input;
    private readonly IApplicationService _apps;
    private readonly IWindowManagementService _windows;
    private readonly IClipboardService _clipboard;
    private readonly IPermissionsService _permissions;

    public PeekabooTools(
        IScreenCaptureService capture,
        IElementDetectionService detection,
        IInputService input,
        IApplicationService apps,
        IWindowManagementService windows,
        IClipboardService clipboard,
        IPermissionsService permissions)
    {
        _capture = capture;
        _detection = detection;
        _input = input;
        _apps = apps;
        _windows = windows;
        _clipboard = clipboard;
        _permissions = permissions;
    }

    // ── SCREEN CAPTURE ──

    [McpServerTool(Name = "capture_screen"), Description("Capture the entire screen and return base64 image data.")]
    public async Task<string> CaptureScreen(
        [Description("Display index (0-based). Default: 0.")] int display = 0)
    {
        var result = await _capture.CaptureScreenAsync(display);
        return $"Captured screen ({result.Metadata.Size.Width}x{result.Metadata.Size.Height}) - {result.ImageData.Length} bytes (base64: {Convert.ToBase64String(result.ImageData).Substring(0, Math.Min(80, Convert.ToBase64String(result.ImageData).Length))}...)";
    }

    [McpServerTool(Name = "capture_window"), Description("Capture a specific application window.")]
    public async Task<string> CaptureWindow(
        [Description("Application name (required)")] string app,
        [Description("Window index (0-based) if multiple windows")] int windowIndex = 0)
    {
        if (string.IsNullOrWhiteSpace(app)) throw new McpException("app is required");
        var result = await _capture.CaptureWindowAsync(app, windowIndex);
        return $"Captured window '{app}' ({result.Metadata.Size.Width}x{result.Metadata.Size.Height}) - {result.ImageData.Length} bytes";
    }

    [McpServerTool(Name = "capture_area"), Description("Capture a rectangular area of the screen.")]
    public async Task<string> CaptureArea(
        [Description("X coordinate")] int x,
        [Description("Y coordinate")] int y,
        [Description("Width")] int width,
        [Description("Height")] int height)
    {
        var result = await _capture.CaptureAreaAsync(new Rect(x, y, width, height));
        return $"Captured area ({x},{y},{width}x{height}) - {result.ImageData.Length} bytes";
    }

    [McpServerTool(Name = "capture_frontmost"), Description("Capture the currently frontmost (active) window.")]
    public async Task<string> CaptureFrontmost()
    {
        var result = await _capture.CaptureFrontmostAsync();
        return $"Captured frontmost window ({result.Metadata.Size.Width}x{result.Metadata.Size.Height}) - {result.ImageData.Length} bytes";
    }

    // ── ELEMENT DETECTION ──

    [McpServerTool(Name = "see"), Description("Detect UI elements on the current screen. Returns a structured list of buttons, text fields, links, images, and other interactive elements with their positions.")]
    public async Task<string> See(
        [Description("Scope detection to this application")] string? app = null,
        [Description("Scope detection to this window title")] string? window = null)
    {
        var shot = await _capture.CaptureFrontmostAsync();
        var wCtx = string.IsNullOrEmpty(app) && string.IsNullOrEmpty(window)
            ? null
            : new WindowContext(ApplicationName: app, WindowTitle: window);
        var result = await _detection.DetectElementsAsync(shot.ImageData, wCtx);

        var lines = new List<string>
        {
            $"Detected {result.Metadata.ElementCount} elements in {result.Metadata.DetectionTime.TotalMilliseconds:F0}ms:",
            $"  Buttons: {result.Elements.Buttons.Count}",
            $"  Text Fields: {result.Elements.TextFields.Count}",
            $"  Links: {result.Elements.Links.Count}",
            $"  Images: {result.Elements.Images.Count}",
            $"  Groups: {result.Elements.Groups.Count}",
            $"  Other: {result.Elements.Other.Count}",
            "",
            "Elements (first 30):"
        };

        foreach (var el in result.Elements.All.Take(30))
        {
            var label = string.IsNullOrEmpty(el.Label) ? "(no label)" : el.Label;
            var valuePart = el.Value != null ? $" = {el.Value}" : "";
            lines.Add($"  [{el.Id}] {el.Type} \"{label}\"" +
                      $" @ ({el.Bounds.X},{el.Bounds.Y},{el.Bounds.Width}x{el.Bounds.Height})" +
                      $" {(el.IsEnabled ? "" : "[disabled]")}{valuePart}");
        }

        if (result.Elements.All.Count > 30)
            lines.Add($"  ... and {result.Elements.All.Count - 30} more");

        return string.Join("\n", lines);
    }

    // ── INPUT ──

    [McpServerTool(Name = "click"), Description("Click at screen coordinates or on a detected element.")]
    public async Task<string> Click(
        [Description("X coordinate")] int? x = null,
        [Description("Y coordinate")] int? y = null,
        [Description("Element ID from 'see' command")] string? elementId = null,
        [Description("Click type: single, double, right")] ClickType type = ClickType.Single)
    {
        ClickTarget target;
        if (!string.IsNullOrEmpty(elementId))
            target = new ClickTarget.ElementId(elementId!);
        else if (x.HasValue && y.HasValue)
            target = new ClickTarget.Coordinates(new Point(x.Value, y.Value));
        else
            throw new McpException("Provide either x+y coordinates or elementId");

        await _input.ClickAsync(target, type);
        return $"Clicked ({type})";
    }

    [McpServerTool(Name = "type_text"), Description("Type text into the focused element.")]
    public async Task<string> TypeText(
        [Description("Text to type")] string text,
        [Description("Clear existing text first")] bool clear = false)
    {
        if (string.IsNullOrEmpty(text)) throw new McpException("text is required");
        await _input.TypeAsync(text, clearExisting: clear);
        return $"Typed {text.Length} characters";
    }

    [McpServerTool(Name = "hotkey"), Description("Press a hotkey combination. Use comma-separated keys like 'ctrl,c' or 'alt,tab'.")]
    public async Task<string> Hotkey(
        [Description("Keys (e.g. ctrl,c)")] string keys)
    {
        if (string.IsNullOrWhiteSpace(keys)) throw new McpException("keys is required");
        await _input.HotkeyAsync(keys);
        return $"Pressed {keys}";
    }

    [McpServerTool(Name = "scroll"), Description("Scroll in a direction.")]
    public async Task<string> Scroll(
        [Description("Scroll direction: up, down, left, right")] ScrollDirection direction,
        [Description("Scroll amount (notches)")] int amount = 3)
    {
        await _input.ScrollAsync(direction, amount);
        return $"Scrolled {direction} ({amount} notches)";
    }

    [McpServerTool(Name = "drag"), Description("Drag from one screen coordinate to another.")]
    public async Task<string> Drag(
        [Description("Start X")] int fromX,
        [Description("Start Y")] int fromY,
        [Description("End X")] int toX,
        [Description("End Y")] int toY)
    {
        await _input.DragAsync(new Point(fromX, fromY), new Point(toX, toY));
        return $"Dragged ({fromX},{fromY}) -> ({toX},{toY})";
    }

    // ── APPLICATIONS ──

    [McpServerTool(Name = "list_apps"), Description("List all running applications.")]
    public async Task<string> ListApps()
    {
        var apps = await _apps.ListApplicationsAsync();
        if (apps.Count == 0) return "No applications found";
        return string.Join("\n", apps.Select(a =>
            $"[{a.ProcessId}] {a.Name}" + (!string.IsNullOrEmpty(a.BundleIdentifier) ? $" ({a.BundleIdentifier})" : "")));
    }

    [McpServerTool(Name = "find_app"), Description("Find a running application by name or PID.")]
    public async Task<string> FindApp(
        [Description("Application name or PID")] string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new McpException("name is required");
        var app = await _apps.FindApplicationAsync(name);
        return $"Found: {app.Name} (PID {app.ProcessId})";
    }

    [McpServerTool(Name = "launch_app"), Description("Launch an application.")]
    public async Task<string> LaunchApp(
        [Description("Application name")] string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new McpException("name is required");
        var app = await _apps.LaunchApplicationAsync(name);
        return $"Launched {app.Name} (PID {app.ProcessId})";
    }

    [McpServerTool(Name = "quit_app"), Description("Quit an application.")]
    public async Task<string> QuitApp(
        [Description("Application name")] string name,
        [Description("Force quit")] bool force = false)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new McpException("name is required");
        var ok = await _apps.QuitApplicationAsync(name, force);
        return ok ? $"Quit {name}" : $"Failed to quit {name}";
    }

    [McpServerTool(Name = "activate_app"), Description("Activate (bring to front) an application.")]
    public async Task<string> ActivateApp(
        [Description("Application name")] string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new McpException("name is required");
        await _apps.ActivateApplicationAsync(name);
        return $"Activated {name}";
    }

    // ── WINDOWS ──

    [McpServerTool(Name = "list_windows"), Description("List windows for an application or the frontmost window.")]
    public async Task<string> ListWindows(
        [Description("Application name (optional, defaults to frontmost)")] string? app = null)
    {
        WindowTarget target = string.IsNullOrEmpty(app)
            ? new WindowTarget.Frontmost()
            : new WindowTarget.Application(app!);
        var windows = await _windows.ListWindowsAsync(target);
        if (windows.Count == 0) return "No windows found";
        return string.Join("\n", windows.Select(w =>
            $"[{w.ProcessId}] {w.Title} ({w.Bounds.Width}x{w.Bounds.Height})"));
    }

    [McpServerTool(Name = "close_window"), Description("Close a window.")]
    public async Task<string> CloseWindow(
        [Description("Application name")] string? app = null,
        [Description("Window title")] string? title = null)
    {
        await _windows.CloseWindowAsync(ParseWinTarget(app, title));
        return "Window closed";
    }

    [McpServerTool(Name = "minimize_window"), Description("Minimize a window.")]
    public async Task<string> MinimizeWindow(
        [Description("Application name")] string? app = null,
        [Description("Window title")] string? title = null)
    {
        await _windows.MinimizeWindowAsync(ParseWinTarget(app, title));
        return "Window minimized";
    }

    [McpServerTool(Name = "maximize_window"), Description("Maximize a window.")]
    public async Task<string> MaximizeWindow(
        [Description("Application name")] string? app = null,
        [Description("Window title")] string? title = null)
    {
        await _windows.MaximizeWindowAsync(ParseWinTarget(app, title));
        return "Window maximized";
    }

    [McpServerTool(Name = "focus_window"), Description("Focus (activate) a window.")]
    public async Task<string> FocusWindow(
        [Description("Application name")] string? app = null,
        [Description("Window title")] string? title = null)
    {
        await _windows.FocusWindowAsync(ParseWinTarget(app, title));
        return "Window focused";
    }

    [McpServerTool(Name = "move_window"), Description("Move a window to a position.")]
    public async Task<string> MoveWindow(
        [Description("X coordinate")] int x,
        [Description("Y coordinate")] int y,
        [Description("Application name")] string? app = null,
        [Description("Window title")] string? title = null)
    {
        await _windows.MoveWindowAsync(ParseWinTarget(app, title), new Point(x, y));
        return $"Window moved to ({x},{y})";
    }

    [McpServerTool(Name = "resize_window"), Description("Resize a window.")]
    public async Task<string> ResizeWindow(
        [Description("Width")] int width,
        [Description("Height")] int height,
        [Description("Application name")] string? app = null,
        [Description("Window title")] string? title = null)
    {
        await _windows.ResizeWindowAsync(ParseWinTarget(app, title), new Size(width, height));
        return $"Window resized to {width}x{height}";
    }

    // ── CLIPBOARD ──

    [McpServerTool(Name = "clipboard_get"), Description("Get the current clipboard text content.")]
    public async Task<string> ClipboardGet()
    {
        var text = await _clipboard.GetTextAsync();
        return string.IsNullOrEmpty(text) ? "Clipboard is empty" : text;
    }

    [McpServerTool(Name = "clipboard_set"), Description("Set the clipboard text content.")]
    public async Task<string> ClipboardSet(
        [Description("Text to set on clipboard")] string text)
    {
        if (string.IsNullOrEmpty(text)) throw new McpException("text is required");
        await _clipboard.SetTextAsync(text);
        return $"Set clipboard ({text.Length} chars)";
    }

    [McpServerTool(Name = "clipboard_clear"), Description("Clear the clipboard.")]
    public async Task<string> ClipboardClear()
    {
        await _clipboard.SetTextAsync("");
        return "Clipboard cleared";
    }

    // ── PERMISSIONS ──

    [McpServerTool(Name = "permissions"), Description("Check automation permissions (admin, UIAccess, etc).")]
    public async Task<string> Permissions()
    {
        var status = await _permissions.CheckAllAsync();
        var lines = new List<string>
        {
            $"Administrator: {(status.IsAdministrator ? "Yes" : "No")}",
            $"UIAccess: {(status.HasUiAccess ? "Yes" : "No")}",
            $"Fully capable: {(status.IsFullyCapable ? "Yes" : "No")}",
        };
        if (status.Warnings.Count > 0)
        {
            lines.Add("Warnings:");
            lines.AddRange(status.Warnings.Select(w => $"  - {w}"));
        }
        return string.Join("\n", lines);
    }

    private static WindowTarget ParseWinTarget(string? app, string? title)
    {
        if (!string.IsNullOrEmpty(app)) return new WindowTarget.Application(app!);
        if (!string.IsNullOrEmpty(title)) return new WindowTarget.Title(title!);
        return new WindowTarget.Frontmost();
    }
}

public static class Program
{
    public static async Task Main(string[] args)
    {
        var services = new ServiceCollection();

        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // Register Peekaboo services
        services.AddSingleton<IScreenCaptureService, WindowsScreenCaptureService>();
        services.AddSingleton<IElementDetectionService, WindowsElementDetectionService>();
        services.AddSingleton<IInputService, WindowsInputService>();
        services.AddSingleton<IApplicationService, WindowsApplicationService>();
        services.AddSingleton<IWindowManagementService, WindowsWindowManagementService>();
        services.AddSingleton<IClipboardService, WindowsClipboardService>();
        services.AddSingleton<IPermissionsService, WindowsPermissionsService>();

        // Register MCP server with stdio transport
        services.AddMcpServer(options =>
        {
            options.ServerInfo = new ModelContextProtocol.Protocol.Implementation
            {
                Name = "Peekaboo-Windows",
                Version = "0.1.0"
            };
        })
        .WithStdioServerTransport()
        .WithTools<PeekabooTools>();

        var serviceProvider = services.BuildServiceProvider();
        var server = serviceProvider.GetRequiredService<ModelContextProtocol.Server.McpServer>();

        Console.Error.WriteLine("Peekaboo MCP Server starting on stdio...");
        await server.RunAsync();
    }
}
