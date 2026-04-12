namespace Peekaboo.Gui.Wpf.Ai;

/// <summary>Formats tool names with icons for display in the UI.</summary>
public static class ToolFormatter
{
    private static readonly Dictionary<string, (string Icon, string Label, string Description)> ToolInfo = new()
    {
        { "capture_screen", ("📸", "Screen Capture", "Capture entire screen") },
        { "capture_window", ("📸", "Window Capture", "Capture specific window") },
        { "capture_frontmost", ("📸", "Frontmost Capture", "Capture frontmost window") },
        { "see", ("👁️", "See Elements", "Detect UI elements on screen") },
        { "click", ("🖱️", "Click", "Click at coordinates or element") },
        { "type_text", ("⌨️", "Type Text", "Type text into focused field") },
        { "hotkey", ("🔑", "Hotkey", "Press keyboard shortcut") },
        { "scroll", ("📜", "Scroll", "Scroll in direction") },
        { "drag", ("↔️", "Drag", "Drag from point to point") },
        { "list_apps", ("📋", "List Apps", "List running applications") },
        { "find_app", ("🔍", "Find App", "Find app by name") },
        { "launch_app", ("🚀", "Launch App", "Launch application") },
        { "quit_app", ("⏹️", "Quit App", "Quit application") },
        { "activate_app", ("🎯", "Activate App", "Activate/bring to front") },
        { "list_windows", ("🪟", "List Windows", "List open windows") },
        { "focus_window", ("🔍", "Focus Window", "Focus window by title") },
        { "close_window", ("❌", "Close Window", "Close window") },
        { "minimize_window", ("➖", "Minimize", "Minimize window") },
        { "maximize_window", ("⬜", "Maximize", "Maximize window") },
        { "move_window", ("📐", "Move Window", "Move window to position") },
        { "resize_window", ("📏", "Resize Window", "Resize window dimensions") },
        { "clipboard_get", ("📋", "Get Clipboard", "Get clipboard content") },
        { "clipboard_set", ("📋", "Set Clipboard", "Set clipboard content") },
        { "clipboard_clear", ("🗑️", "Clear Clipboard", "Clear clipboard") },
        { "permissions", ("🔒", "Permissions", "Check accessibility permissions") },
        { "menu_list", ("📋", "Menu List", "List menu items") },
        { "menu_click", ("🖱️", "Menu Click", "Click menu item by path") },
        { "taskbar_list", ("📋", "Taskbar List", "List taskbar items") },
        { "taskbar_click", ("🖱️", "Taskbar Click", "Click taskbar item") },
        { "taskbar_hide", ("🔽", "Taskbar Hide", "Hide taskbar item") },
        { "taskbar_show", ("🔼", "Taskbar Show", "Show taskbar item") },
        { "dialog_set_path", ("📁", "Dialog Set Path", "Set file path in dialog") },
        { "dialog_set_filter", ("📁", "Dialog Set Filter", "Set file filter") },
        { "dialog_click_button", ("🖱️", "Dialog Click Button", "Click dialog button") },
        { "space_list", ("🖥️", "Space List", "List virtual desktops") },
        { "space_switch", ("↔️", "Space Switch", "Switch virtual desktop") },
        { "space_create", ("➕", "Space Create", "Create virtual desktop") },
        { "space_move_window", ("📦", "Space Move Window", "Move window to desktop") },
    };

    public static string GetIcon(string toolName) =>
        ToolInfo.TryGetValue(toolName, out var info) ? info.Icon : "🔧";

    public static string GetLabel(string toolName) =>
        ToolInfo.TryGetValue(toolName, out var info) ? info.Label : toolName;

    public static string GetDescription(string toolName) =>
        ToolInfo.TryGetValue(toolName, out var info) ? info.Description : toolName;

    public static string FormatCall(string toolName, string? args)
    {
        var icon = GetIcon(toolName);
        var label = GetLabel(toolName);
        var argsText = string.IsNullOrEmpty(args) ? "" : $" — {args}";
        return $"{icon} {label}{argsText}";
    }
}
