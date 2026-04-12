using System.Runtime.InteropServices;
using Peekaboo.Core;
using Peekaboo.Platform.Windows.Native;

namespace Peekaboo.Platform.Windows.Services;

/// <summary>
/// Windows window management using User32 APIs.
/// </summary>
public sealed class WindowsWindowManagementService : IWindowManagementService
{
    private readonly IApplicationService _appService;

    public WindowsWindowManagementService(IApplicationService appService)
    {
        _appService = appService;
    }

    public async Task CloseWindowAsync(WindowTarget target, CancellationToken ct = default)
    {
        var hwnd = await ResolveWindowHandle(target, ct);
        NativeMethods.PostMessage(hwnd, NativeMethods.WM_CLOSE, nint.Zero, nint.Zero);
    }

    public async Task MinimizeWindowAsync(WindowTarget target, CancellationToken ct = default)
    {
        var hwnd = await ResolveWindowHandle(target, ct);
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_MINIMIZE);
    }

    public async Task MaximizeWindowAsync(WindowTarget target, CancellationToken ct = default)
    {
        var hwnd = await ResolveWindowHandle(target, ct);
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_MAXIMIZE);
    }

    public async Task MoveWindowAsync(WindowTarget target, Point position, CancellationToken ct = default)
    {
        var hwnd = await ResolveWindowHandle(target, ct);
        NativeMethods.GetWindowRect(hwnd, out var rect);
        NativeMethods.MoveWindow(hwnd, (int)position.X, (int)position.Y, rect.Width, rect.Height, true);
    }

    public async Task ResizeWindowAsync(WindowTarget target, Size size, CancellationToken ct = default)
    {
        var hwnd = await ResolveWindowHandle(target, ct);
        NativeMethods.GetWindowRect(hwnd, out var rect);
        NativeMethods.MoveWindow(hwnd, rect.Left, rect.Top, (int)size.Width, (int)size.Height, true);
    }

    public async Task SetWindowBoundsAsync(WindowTarget target, Rect bounds, CancellationToken ct = default)
    {
        var hwnd = await ResolveWindowHandle(target, ct);
        NativeMethods.MoveWindow(hwnd, (int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height, true);
    }

    public async Task FocusWindowAsync(WindowTarget target, CancellationToken ct = default)
    {
        var hwnd = await ResolveWindowHandle(target, ct);
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        NativeMethods.SetForegroundWindow(hwnd);
    }

    public async Task<IReadOnlyList<ServiceWindowInfo>> ListWindowsAsync(WindowTarget target, CancellationToken ct = default)
    {
        var windows = new List<ServiceWindowInfo>();

        switch (target)
        {
            case WindowTarget.Application app:
                return await _appService.ListWindowsAsync(app.AppName, ct);

            case WindowTarget.ApplicationAndTitle at:
                return (await _appService.ListWindowsAsync(at.AppName, ct))
                    .Where(w => w.Title.Contains(at.TitleText, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            case WindowTarget.Frontmost:
                var fg = NativeMethods.GetForegroundWindow();
                if (fg != nint.Zero)
                    return new[] { await GetWindowInfoAsync(fg, 0, ct) };
                return Array.Empty<ServiceWindowInfo>();

            case WindowTarget.WindowId wid:
                return new[] { await GetWindowInfoAsync((nint)wid.Id, 0, ct) };

            case WindowTarget.All:
                return await ListAllWindowsAsync(ct);

            default:
                throw new NotImplementedException($"WindowTarget type {target.GetType().Name} not supported for listing");
        }
    }

    private async Task<IReadOnlyList<ServiceWindowInfo>> ListAllWindowsAsync(CancellationToken ct)
    {
        var windows = new List<ServiceWindowInfo>();
        int index = 0;

        NativeMethods.EnumWindowsProc proc = (hWnd, _) =>
        {
            if (ct.IsCancellationRequested) return false;
            if (!NativeMethods.IsWindowVisible(hWnd)) return true;
            if (NativeMethods.GetWindowTextLength(hWnd) <= 0) return true;

            windows.Add(GetWindowInfoAsync(hWnd, index++, ct).GetAwaiter().GetResult());
            return true;
        };

        NativeMethods.EnumWindows(proc, nint.Zero);
        ct.ThrowIfCancellationRequested();
        return await Task.FromResult<IReadOnlyList<ServiceWindowInfo>>(windows);
    }

    public async Task<ServiceWindowInfo?> GetFocusedWindowAsync(CancellationToken ct = default)
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == nint.Zero) return null;
        return await GetWindowInfoAsync(hwnd, 0, ct);
    }

    private async Task<ServiceWindowInfo> GetWindowInfoAsync(nint hwnd, int index, CancellationToken ct)
    {
        NativeMethods.GetWindowRect(hwnd, out var rect);
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);

        string title = "";
        try
        {
            var len = NativeMethods.GetWindowTextLength(hwnd);
            if (len > 0)
            {
                var sb = new System.Text.StringBuilder(len + 1);
                NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
                title = sb.ToString();
            }
        }
        catch { }

        string? procName = null;
        try
        {
            var proc = System.Diagnostics.Process.GetProcessById((int)pid);
            procName = proc.ProcessName;
        }
        catch { }

        return new ServiceWindowInfo(
            WindowId: hwnd,
            Title: title,
            Bounds: new Core.Rect(rect.Left, rect.Top, rect.Width, rect.Height),
            IsMinimized: NativeMethods.IsIconic(hwnd),
            IsMainWindow: index == 0,
            WindowLevel: 0,
            Alpha: 1.0,
            Index: index,
            IsOnScreen: NativeMethods.IsWindowVisible(hwnd),
            ProcessId: (int)pid,
            ProcessName: procName
        );
    }

    private async Task<nint> ResolveWindowHandle(WindowTarget target, CancellationToken ct)
    {
        return target switch
        {
            WindowTarget.Frontmost => NativeMethods.GetForegroundWindow(),
            WindowTarget.WindowId wid => (nint)wid.Id,
            WindowTarget.Application app => await FindWindowForAppAsync(app.AppName, ct),
            WindowTarget.ApplicationAndTitle at => await FindWindowForAppAndTitleAsync(at.AppName, at.TitleText, ct),
            WindowTarget.Title title => await FindWindowByTitleAsync(title.Text, ct),
            WindowTarget.Index idx => await FindWindowByIndexAsync(idx.AppName, idx.WindowIndex, ct),
            WindowTarget.All => throw new PeekabooException("The 'all' window target is only valid for listing windows"),
            _ => throw new PeekabooException($"Unsupported window target: {target.GetType().Name}")
        };
    }

    private async Task<IReadOnlyList<ServiceWindowInfo>> ListAllWindowsAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var windows = new List<ServiceWindowInfo>();
        var foreground = NativeMethods.GetForegroundWindow();
        int index = 0;

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hWnd)) return true;

            var length = NativeMethods.GetWindowTextLength(hWnd);
            if (length == 0) return true;

            windows.Add(GetWindowInfoAsync(hWnd, index++, ct).GetAwaiter().GetResult() with
            {
                IsMainWindow = hWnd == foreground,
            });
            return true;
        }, nint.Zero);

        await Task.CompletedTask;
        return windows;
    }

    private async Task<nint> FindWindowForAppAsync(string appName, CancellationToken ct)
    {
        var windows = await _appService.ListWindowsAsync(appName, ct);
        return windows.FirstOrDefault()?.WindowId ?? throw new WindowNotFoundException($"No windows found for {appName}");
    }

    private async Task<nint> FindWindowForAppAndTitleAsync(string appName, string title, CancellationToken ct)
    {
        var windows = await _appService.ListWindowsAsync(appName, ct);
        var match = windows.FirstOrDefault(w => w.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
        return match?.WindowId ?? throw new WindowNotFoundException($"No window with title '{title}' found for {appName}");
    }

    private async Task<nint> FindWindowByTitleAsync(string title, CancellationToken ct)
    {
        var apps = await _appService.ListApplicationsAsync(ct);
        foreach (var app in apps)
        {
            try
            {
                var windows = await _appService.ListWindowsAsync(app.Name, ct);
                var match = windows.FirstOrDefault(w => w.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match.WindowId;
            }
            catch { }
        }
        throw new WindowNotFoundException($"No window with title '{title}' found");
    }

    private async Task<nint> FindWindowByIndexAsync(string appName, int index, CancellationToken ct)
    {
        var windows = await _appService.ListWindowsAsync(appName, ct);
        if (index < 0 || index >= windows.Count)
            throw new WindowNotFoundException($"Window index {index} out of range for {appName} ({windows.Count} windows)");
        return windows[index].WindowId;
    }
}
