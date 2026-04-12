using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Peekaboo.Core;

namespace Peekaboo.Platform.Windows.Services;

/// <summary>
/// Windows taskbar service using Shell and Win32 APIs.
/// Lists taskbar items and supports clicking/hiding/showing.
/// </summary>
public sealed class WindowsTaskbarService : ITaskbarService
{
    private readonly ILogger<WindowsTaskbarService> _logger;

    public WindowsTaskbarService(ILogger<WindowsTaskbarService> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<TaskbarItemInfo>> ListTaskbarItemsAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var items = new List<TaskbarItemInfo>();

        try
        {
            // Get all visible windows that appear in taskbar
            var windows = GetVisibleWindows();
            var hwnd = Shell32.FindWindow("Shell_TrayWnd", null);
            
            if (hwnd != 0)
            {
                // Enum child windows to find taskbar buttons
                var trayNotify = Shell32.FindWindowEx(hwnd, 0, "TrayNotifyWnd", null);
                if (trayNotify != 0)
                {
                    // Get system tray items
                }
            }

            // For simplicity, return running apps that have taskbar windows
            var processes = System.Diagnostics.Process.GetProcesses();
            int index = 0;

            foreach (var proc in processes)
            {
                try
                {
                    if (string.IsNullOrEmpty(proc.ProcessName)) continue;
                    if (proc.Id == 4) continue; // Skip System

                    // Check if process has a main window
                    if (proc.MainWindowHandle != 0)
                        {
                            var title = proc.MainWindowTitle;
                            if (!string.IsNullOrEmpty(title))
                            {
                                // Use stable ID based on process ID and window handle
                                items.Add(new TaskbarItemInfo(
                                    Id: $"taskbar_{proc.Id}_{proc.MainWindowHandle:X}",
                                    Name: title,
                                    ProcessName: proc.ProcessName,
                                    ProcessId: proc.Id,
                                    IsActive: proc.MainWindowHandle == User32.GetForegroundWindow(),
                                    IsPinned: false,
                                    WindowHandle: proc.MainWindowHandle
                                ));
                            }
                        }
                }
                catch
                {
                    // Skip inaccessible processes
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing taskbar items");
        }

        return Task.FromResult<IReadOnlyList<TaskbarItemInfo>>(items);
    }

    public Task ClickTaskbarItemAsync(string itemId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            // Handle new stable ID format: taskbar_{ProcessId}_{WindowHandle:X}
            if (itemId.StartsWith("taskbar_"))
            {
                var parts = itemId.Replace("taskbar_", "").Split('_');
                if (parts.Length == 2 && int.TryParse(parts[0], out int pid))
                {
                    // Try to find the process and its main window
                    var proc = Process.GetProcesses().FirstOrDefault(p => p.Id == pid && p.MainWindowHandle != 0);
                    if (proc != null)
                    {
                        User32.ShowWindow(proc.MainWindowHandle, 9);
                        User32.SetForegroundWindow(proc.MainWindowHandle);
                        return Task.CompletedTask;
                    }
                }
                else
                {
                    // Legacy index-based format for backward compatibility
                    var indexStr = itemId.Replace("taskbar_", "");
                    if (int.TryParse(indexStr, out int index))
                    {
                        var items = ListTaskbarItemsAsync(ct).Result;
                        if (index < items.Count)
                        {
                            var item = items[index];
                            User32.ShowWindow(item.WindowHandle, 9);
                            User32.SetForegroundWindow(item.WindowHandle);
                            return Task.CompletedTask;
                        }
                    }
                }
            }

            // Try to find by window title
            var hwnd = FindWindowByTitle(itemId);
            if (hwnd != 0)
            {
                User32.ShowWindow(hwnd, 9);
                User32.SetForegroundWindow(hwnd);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clicking taskbar item: {ItemId}", itemId);
            throw;
        }

        return Task.CompletedTask;
    }

    public Task HideTaskbarItemAsync(string itemId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var hwnd = GetWindowHandleFromId(itemId);
            if (hwnd != 0)
            {
                User32.ShowWindow(hwnd, 0); // SW_HIDE
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error hiding taskbar item: {ItemId}", itemId);
            throw;
        }

        return Task.CompletedTask;
    }

    public Task ShowTaskbarItemAsync(string itemId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var hwnd = GetWindowHandleFromId(itemId);
            if (hwnd != 0)
            {
                User32.ShowWindow(hwnd, 5); // SW_SHOW
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing taskbar item: {ItemId}", itemId);
            throw;
        }

        return Task.CompletedTask;
    }

    private static nint FindWindowByTitle(string title)
    {
        nint result = 0;

        User32.EnumWindows((hWnd, _) =>
        {
            int length = User32.GetWindowTextLength(hWnd);
            if (length == 0) return true;

            var sb = new StringBuilder(length + 1);
            User32.GetWindowText(hWnd, sb, sb.Capacity);
            var windowName = sb.ToString();

            if (windowName.Contains(title, StringComparison.OrdinalIgnoreCase))
            {
                result = hWnd;
                return false;
            }
            return true;
        }, 0);

        return result;
    }

    private static nint GetWindowHandleFromId(string itemId)
    {
        if (!itemId.StartsWith("taskbar_")) return 0;
        if (!int.TryParse(itemId.Replace("taskbar_", ""), out int index)) return 0;

        var items = GetVisibleWindows();
        if (index < items.Count) return items[index].WindowHandle;
        return 0;
    }

    private static List<TaskbarItemInfo> GetVisibleWindows()
    {
        var items = new List<TaskbarItemInfo>();
        var processes = System.Diagnostics.Process.GetProcesses();
        int index = 0;

        foreach (var proc in processes)
        {
            try
            {
                if (proc.MainWindowHandle != 0 && !string.IsNullOrEmpty(proc.MainWindowTitle))
                {
                    items.Add(new TaskbarItemInfo(
                        Id: $"taskbar_{index}",
                        Name: proc.MainWindowTitle,
                        ProcessName: proc.ProcessName,
                        ProcessId: proc.Id,
                        IsActive: false,
                        IsPinned: false,
                        WindowHandle: proc.MainWindowHandle
                    ));
                    index++;
                }
            }
            catch { }
        }

        return items;
    }
}

// Win32 P/Invoke
file static partial class Shell32
{
    [DllImport("shell32.dll")]
    public static extern nint FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("shell32.dll")]
    public static extern nint FindWindowEx(nint hwndParent, nint hwndChildAfter, string? lpszClass, string? lpszWindow);
}

file static partial class User32
{
    public delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    public static extern int GetWindowTextLength(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(nint hWnd, int nCmdShow);
}
