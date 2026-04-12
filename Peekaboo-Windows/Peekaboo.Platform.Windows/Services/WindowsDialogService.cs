using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Peekaboo.Core;

namespace Peekaboo.Platform.Windows.Services;

/// <summary>
/// Windows dialog service using Win32 APIs to drive file open/save dialogs.
/// </summary>
public sealed class WindowsDialogService : IDialogService
{
    private readonly ILogger<WindowsDialogService> _logger;

    public WindowsDialogService(ILogger<WindowsDialogService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> WaitForDialogAsync(int timeoutMs = 5000, CancellationToken ct = default)
    {
        var start = DateTime.Now;
        while (DateTime.Now - start < TimeSpan.FromMilliseconds(timeoutMs))
        {
            ct.ThrowIfCancellationRequested();

            // Find common dialog window classes
            var dialogHWnd = User32.FindWindow("#32770", null); // Common dialog class
            if (dialogHWnd != 0)
            {
                return true;
            }
            await Task.Delay(100, ct);
        }
        return false;
    }

    public async Task SetPathAsync(string path, CancellationToken ct = default)
    {
        // Find the dialog
        var hwnd = FindDialog();
        if (hwnd == 0)
        {
            throw new InvalidOperationException("No dialog found");
        }

        // Find the filename edit control (RichEdit or Edit)
        var editHwnd = User32.FindWindowEx(hwnd, 0, "Edit", null);
        if (editHwnd == 0)
        {
            // Try ComboBox (some dialogs use this)
            editHwnd = User32.FindWindowEx(hwnd, 0, "ComboBox", null);
        }

        if (editHwnd == 0)
        {
            throw new InvalidOperationException("Could not find file path control");
        }

        // Set the text
        User32.SetWindowText(editHwnd, path);
        await Task.Delay(50, ct); // Allow dialog to process
    }

    public async Task SetFilterAsync(string filter, CancellationToken ct = default)
    {
        var hwnd = FindDialog();
        if (hwnd == 0)
        {
            throw new InvalidOperationException("No dialog found");
        }

        // Find the file type combo box
        var comboHwnd = User32.FindWindowEx(hwnd, 0, "ComboBox", null);
        if (comboHwnd == 0)
        {
            // Try finding all combo boxes
            User32.EnumChildWindows(hwnd, (child, _) =>
            {
                var className = new StringBuilder(256);
                User32.GetClassName(child, className, 256);
                if (className.ToString() == "ComboBox")
                {
                    comboHwnd = child;
                    return false;
                }
                return true;
            }, 0);
        }

        if (comboHwnd == 0)
        {
            _logger.LogWarning("Could not find filter combo box");
            return;
        }

        // Select the filter - typically index corresponds to filter string
        // For simplicity, we'll just note that filters are usually pre-defined
        _logger.LogInformation("Filter setting not fully implemented - filters typically pre-defined");
        await Task.Delay(50, ct);
    }

    public async Task ClickButtonAsync(DialogButton button, CancellationToken ct = default)
    {
        var hwnd = FindDialog();
        if (hwnd == 0)
        {
            throw new InvalidOperationException("No dialog found");
        }

        // Button IDs for common dialog
        uint buttonId = button switch
        {
            DialogButton.Ok => 1,
            DialogButton.Cancel => 2,
            DialogButton.Open => 1,
            DialogButton.Save => 1,
            DialogButton.Yes => 6,
            DialogButton.No => 7,
            _ => 1
        };

        // Find and click the button
        var buttonHwnd = GetDlgItem(hwnd, (int)buttonId);
        if (buttonHwnd != 0)
        {
            User32.SendMessage(buttonHwnd, 0x00F4, 0, 0); // BM_CLICK
        }
        else
        {
            // Fallback: find by text
            User32.EnumChildWindows(hwnd, (child, _) =>
            {
                var text = new StringBuilder(256);
                User32.GetWindowText(child, text, 256);
                var btnText = text.ToString().ToLower();

                if ((button == DialogButton.Ok && (btnText == "ok" || btnText == "open")) ||
                    (button == DialogButton.Cancel && btnText == "cancel") ||
                    (button == DialogButton.Save && btnText == "save") ||
                    (button == DialogButton.Yes && btnText == "yes") ||
                    (button == DialogButton.No && btnText == "no"))
                {
                    User32.SendMessage(child, 0x00F4, 0, 0);
                    return false;
                }
                return true;
            }, 0);
        }

        await Task.Delay(50, ct);
    }

    private static nint FindDialog()
    {
        // Look for common dialog class
        return User32.FindWindow("#32770", null);
    }

    private static nint GetDlgItem(nint hwnd, int itemId)
    {
        return User32.GetDlgItem(hwnd, itemId);
    }
}

// Win32 P/Invoke
file static partial class User32
{
    [DllImport("user32.dll")]
    public static extern nint FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    public static extern nint FindWindowEx(nint hwndParent, nint hwndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    public static extern nint GetDlgItem(nint hDlg, int nIDDlgItem);

    [DllImport("user32.dll")]
    public static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int SetWindowText(nint hWnd, string lpString);

    [DllImport("user32.dll")]
    public static extern nint SendMessage(nint hWnd, uint Msg, nint wParam, nint lParam);

    public delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumChildWindows(nint hWnd, EnumWindowsProc lpEnumFunc, nint lParam);
}
