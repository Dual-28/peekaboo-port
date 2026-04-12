# Peekaboo for Windows

A Windows port of [steipete/Peekaboo](https://github.com/steipete/Peekaboo) - desktop automation tool with MCP server support.

## Architecture

```
Peekaboo-Windows/
├── Peekaboo.Core/              # Domain models + service interfaces
├── Peekaboo.Platform.Windows/  # Win32 P/Invoke + FlaUI implementations
├── Peekaboo.Cli/               # System.CommandLine CLI
└── Peekaboo.McpServer/         # MCP SDK server (stdio transport)
```

## Requirements

- .NET 10 SDK
- Windows 10/11

## Build

```bash
dotnet build Peekaboo.slnx
```

## CLI Usage

```bash
dotnet run --project Peekaboo.Cli -- --help

# Capture
dotnet run --project Peekaboo.Cli -- capture screen
dotnet run --project Peekaboo.Cli -- capture window --app notepad
dotnet run --project Peekaboo.Cli -- capture frontmost

# See (element detection)
dotnet run --project Peekaboo.Cli -- see
dotnet run --project Peekaboo.Cli -- see --app notepad

# Input
dotnet run --project Peekaboo.Cli -- click --x 100 --y 200
dotnet run --project Peekaboo.Cli -- type --text "Hello World"
dotnet run --project Peekaboo.Cli -- hotkey "ctrl,c"
dotnet run --project Peekaboo.Cli -- scroll --direction down --amount 5
dotnet run --project Peekaboo.Cli -- drag --from-x 0 --from-y 0 --to-x 100 --to-y 100

# Applications
dotnet run --project Peekaboo.Cli -- app list
dotnet run --project Peekaboo.Cli -- app find notepad
dotnet run --project Peekaboo.Cli -- app launch notepad
dotnet run --project Peekaboo.Cli -- app quit notepad
dotnet run --project Peekaboo.Cli -- app activate notepad

# Windows
dotnet run --project Peekaboo.Cli -- window list
dotnet run --project Peekaboo.Cli -- window close --app notepad
dotnet run --project Peekaboo.Cli -- window minimize --app notepad
dotnet run --project Peekaboo.Cli -- window maximize --app notepad
dotnet run --project Peekaboo.Cli -- window focus --app notepad
dotnet run --project Peekaboo.Cli -- window move --app notepad --x 0 --y 0
dotnet run --project Peekaboo.Cli -- window resize --app notepad --width 800 --height 600

# Clipboard
dotnet run --project Peekaboo.Cli -- clipboard get
dotnet run --project Peekaboo.Cli -- clipboard set --text "Hello"
dotnet run --project Peekaboo.Cli -- clipboard clear

# Permissions
dotnet run --project Peekaboo.Cli -- permissions

# Clean
dotnet run --project Peekaboo.Cli -- clean --all-snapshots

# JSON output
dotnet run --project Peekaboo.Cli -- capture screen --json
```

## MCP Server

The MCP server runs over stdio and exposes all automation tools to AI clients.

```bash
dotnet run --project Peekaboo.McpServer
```

### Claude Desktop Configuration

Add to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "peekaboo-windows": {
      "command": "dotnet",
      "args": ["run", "--project", "C:\\path\\to\\Peekaboo-Windows\\Peekaboo.McpServer"]
    }
  }
}
```

### Available MCP Tools

| Tool | Description |
|------|-------------|
| `capture_screen` | Capture entire screen |
| `capture_window` | Capture specific application window |
| `capture_area` | Capture rectangular area |
| `capture_frontmost` | Capture frontmost window |
| `see` | Detect UI elements on screen |
| `click` | Click at coordinates or element |
| `type_text` | Type text into focused element |
| `hotkey` | Press hotkey combination |
| `scroll` | Scroll in direction |
| `drag` | Drag between coordinates |
| `list_apps` | List running applications |
| `find_app` | Find application by name |
| `launch_app` | Launch application |
| `quit_app` | Quit application |
| `activate_app` | Activate application |
| `list_windows` | List windows |
| `close_window` | Close window |
| `minimize_window` | Minimize window |
| `maximize_window` | Maximize window |
| `focus_window` | Focus window |
| `move_window` | Move window to position |
| `resize_window` | Resize window |
| `clipboard_get` | Get clipboard text |
| `clipboard_set` | Set clipboard text |
| `clipboard_clear` | Clear clipboard |
| `permissions` | Check automation permissions |

## Platform Implementation

| Service | Implementation |
|---------|---------------|
| Screen Capture | GDI `BitBlt` / `CopyFromScreen` |
| Element Detection | FlaUI UIA3 tree walk |
| Input | Win32 `SendInput` (mouse + keyboard) |
| Applications | `Process.GetProcesses()` + `EnumWindows` |
| Window Management | User32 `ShowWindow`, `SetWindowPos`, `MoveWindow` |
| Clipboard | Win32 `OpenClipboard` / `SetClipboardData` |
| Permissions | Admin check + UIAccess manifest check |

## Permissions

For full automation capability:
- **Administrator**: Run as admin for access to elevated processes
- **UIAccess**: Sign the binary and place in `Program Files` or `System32` for UIAccess privileges (allows interaction with elevated windows)

## Differences from macOS Original

- Screen capture uses GDI instead of ScreenCaptureKit (no native HDR/10-bit)
- Element detection uses FlaUI/UIA3 instead of AX (Accessibility API)
- Input uses `SendInput` instead of CoreGraphics events
- No Dock, Spaces, Menu Bar, or Dialog APIs (Windows equivalents differ significantly)
- No daemon mode (Windows Service would be needed)

## License

Same as original Peekaboo project.
