# Peekaboo Windows Port - Next Priorities Implementation

## Current State
- UI works, multi-monitor screen capture implemented (ListDisplaysAsync added)
- Need to add: menu discovery, taskbar interaction, dialog handling, virtual desktops

## Priority 1: Menu Discovery
Implement Windows menu discovery - equivalent to macOS menu/menubar commands.

### Implementation approach:
1. Add `IMenuDiscoveryService` interface to Peekaboo.Core
2. Implement via Windows API (GetMenu, GetSystemMenu, EnumWindows for tray)
3. Expose as `menu_list`, `menu_click` tools in agent

### Tools to add:
- `menu_list` - List menu bar items for an app (File, Edit, View, etc.)
- `menu_click` - Click a specific menu item by path (File > Save As)

## Priority 2: Taskbar/Dock Interaction
Windows equivalent of macOS dock - the taskbar.

### Implementation approach:
1. Use Windows Shell APIs (ITaskbarList3, Shell.Windows)
2. List taskbar items, get their positions
3. Simulate clicks via SendMessage/PostMessage

### Tools to add:
- `taskbar_list` - List pinned/running apps in taskbar
- `taskbar_click` - Click a taskbar item by index or name
- `taskbar_hide` / `taskbar_show` - Toggle visibility

## Priority 3: Dialog Handling
Drive system file open/save dialogs.

### Implementation approach:
1. Use Windows Accessibility (UIA) to find dialog controls
2. Navigate tree: dialog > combobox (Save As type) > button (Save)

### Tools to add:
- `dialog_set_path` - Set file path in open/save dialog
- `dialog_set_filter` - Set file type filter
- `dialog_click_button` - Click OK/Cancel/Open

## Priority 4: Virtual Desktops (Spaces)
Windows Virtual Desktop API via virtual-desktop-windows COM API.

### Implementation approach:
1. Use IVirtualDesktopManager interface
2. List desktops, switch, move windows between desktops

### Tools to add:
- `space_list` - List virtual desktops
- `space_switch` - Switch to a desktop
- `space_move_window` - Move window to a desktop

## Files to modify:
- Peekaboo.Core/ (add interfaces)
- Peekaboo.Platform.Windows/ (add implementations)
- Peekaboo.Gui.Wpf/Ai/PeekabooAgentService.cs (add tool handlers)
- Peekaboo.Gui.Wpf/Ai/ToolFormatter.cs (add tool icons)

## Test approach:
- Run WPF app, verify each tool via agent
- Check %APPDATA%/Peekaboo/peekaboo.log for errors