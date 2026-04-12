using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Peekaboo.Core;
using Peekaboo.Platform.Windows.Native;

namespace Peekaboo.Platform.Windows.Services;

/// <summary>
/// Windows application management using Process and User32 APIs.
/// </summary>
public sealed class WindowsApplicationService : IApplicationService
{
    private readonly ILogger<WindowsApplicationService> _logger;

    public WindowsApplicationService(ILogger<WindowsApplicationService> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<ServiceApplicationInfo>> ListApplicationsAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var processes = Process.GetProcesses();
        var apps = new List<ServiceApplicationInfo>();

        foreach (var proc in processes)
        {
            try
            {
                if (string.IsNullOrEmpty(proc.ProcessName)) continue;
                if (proc.Id == 0 || proc.Id == 4) continue; // System/Idle

                var path = GetProcessPath(proc);
                var isActive = proc.Id == GetForegroundProcessId();

                apps.Add(new ServiceApplicationInfo(
                    ProcessId: proc.Id,
                    BundleIdentifier: null, // Windows doesn't have bundle IDs
                    Name: proc.ProcessName,
                    BundlePath: path,
                    IsActive: isActive,
                    IsHidden: false,
                    WindowCount: CountWindowsForProcess((uint)proc.Id)
                ));
            }
            catch
            {
                // Access denied or process exited — skip silently
            }
        }

        return Task.FromResult<IReadOnlyList<ServiceApplicationInfo>>(apps);
    }

    public async Task<ServiceApplicationInfo> FindApplicationAsync(string identifier, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var apps = await ListApplicationsAsync(ct);

        // Try exact match first, then case-insensitive, then substring
        var app = apps.FirstOrDefault(a =>
            string.Equals(a.Name, identifier, StringComparison.OrdinalIgnoreCase) ||
            (a.BundlePath != null && a.BundlePath.Contains(identifier, StringComparison.OrdinalIgnoreCase)));

        if (app == null)
            throw new ApplicationNotFoundException(identifier);

        return app;
    }

    public async Task<IReadOnlyList<ServiceWindowInfo>> ListWindowsAsync(string appIdentifier, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var app = await FindApplicationAsync(appIdentifier, ct);
        var windows = new List<ServiceWindowInfo>();
        var foregroundPid = GetForegroundProcessId();
        int index = 0;

        NativeMethods.EnumWindowsProc proc = (hWnd, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
            if (pid != app.ProcessId) return true;
            if (!NativeMethods.IsWindowVisible(hWnd)) return true;

            var title = GetWindowText(hWnd);
            if (string.IsNullOrEmpty(title)) return true;

            NativeMethods.GetWindowRect(hWnd, out var rect);
            var isMain = pid == foregroundPid && index == 0;

            windows.Add(new ServiceWindowInfo(
                WindowId: hWnd,
                Title: title,
                Bounds: new Core.Rect(rect.Left, rect.Top, rect.Width, rect.Height),
                IsMinimized: NativeMethods.IsIconic(hWnd),
                IsMainWindow: isMain,
                WindowLevel: 0,
                Alpha: 1.0,
                Index: index++,
                IsOnScreen: true,
                ProcessId: (int)pid,
                ProcessName: app.Name
            ));

            return true;
        };

        NativeMethods.EnumWindows(proc, nint.Zero);
        return windows;
    }

    public Task<ServiceApplicationInfo> GetForegroundApplicationAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var pid = GetForegroundProcessId();

        try
        {
            var proc = Process.GetProcessById((int)pid);
            return Task.FromResult(new ServiceApplicationInfo(
                ProcessId: proc.Id,
                BundleIdentifier: null,
                Name: proc.ProcessName,
                BundlePath: GetProcessPath(proc),
                IsActive: true,
                IsHidden: false,
                WindowCount: CountWindowsForProcess(pid)
            ));
        }
        catch
        {
            throw new ApplicationNotFoundException($"Process ID {pid}");
        }
    }

    public async Task<bool> IsApplicationRunningAsync(string identifier, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var apps = await ListApplicationsAsync(ct);
        return apps.Any(a => string.Equals(a.Name, identifier, StringComparison.OrdinalIgnoreCase));
    }

    public Task<ServiceApplicationInfo> LaunchApplicationAsync(string identifier, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var psi = new ProcessStartInfo
        {
            FileName = identifier,
            UseShellExecute = true
        };

        var proc = Process.Start(psi)
            ?? throw new PeekabooException($"Failed to launch application: {identifier}");

        proc.WaitForInputIdle(5000);

        return Task.FromResult(new ServiceApplicationInfo(
            ProcessId: proc.Id,
            BundleIdentifier: null,
            Name: proc.ProcessName,
            BundlePath: GetProcessPath(proc),
            IsActive: true,
            IsHidden: false,
            WindowCount: 0
        ));
    }

    public async Task ActivateApplicationAsync(string identifier, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var app = await FindApplicationAsync(identifier, ct);

        var hwnd = GetMainWindowForProcess((uint)app.ProcessId);
        if (hwnd == nint.Zero)
            throw new WindowNotFoundException($"No visible window found for {identifier}");

        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        NativeMethods.SetForegroundWindow(hwnd);

        _logger.LogInformation("Activated application: {Name} (PID {Pid})", app.Name, app.ProcessId);
    }

    public async Task<bool> QuitApplicationAsync(string identifier, bool force = false, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var app = await FindApplicationAsync(identifier, ct);

        try
        {
            var proc = Process.GetProcessById(app.ProcessId);
            if (force)
            {
                proc.Kill();
            }
            else
            {
                // Try graceful close via WM_CLOSE on main window
                var hwnd = GetMainWindowForProcess((uint)app.ProcessId);
                if (hwnd != nint.Zero)
                {
                    NativeMethods.PostMessage(hwnd, NativeMethods.WM_CLOSE, nint.Zero, nint.Zero);
                    proc.WaitForExit(5000);
                    if (!proc.HasExited) proc.Kill();
                }
                else
                {
                    proc.Kill();
                }
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task HideApplicationAsync(string identifier, CancellationToken ct = default)
    {
        // On Windows, "hiding" an app means minimizing all its windows
        ct.ThrowIfCancellationRequested();
        _ = FindApplicationAsync(identifier, ct).Result; // Validate app exists

        NativeMethods.EnumWindowsProc proc = (hWnd, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
            if (!NativeMethods.IsWindowVisible(hWnd)) return true;
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MINIMIZE);
            return true;
        };

        // We need the PID — do it synchronously for the enum callback
        var app = FindApplicationAsync(identifier, ct).Result;
        var targetPid = (uint)app.ProcessId;

        NativeMethods.EnumWindowsProc filteredProc = (hWnd, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
            if (pid == targetPid && NativeMethods.IsWindowVisible(hWnd))
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MINIMIZE);
            }
            return true;
        };

        NativeMethods.EnumWindows(filteredProc, nint.Zero);
        return Task.CompletedTask;
    }

    public Task UnhideApplicationAsync(string identifier, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var app = FindApplicationAsync(identifier, ct).Result;
        var targetPid = (uint)app.ProcessId;

        NativeMethods.EnumWindowsProc proc = (hWnd, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
            if (pid == targetPid && NativeMethods.IsIconic(hWnd))
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
            }
            return true;
        };

        NativeMethods.EnumWindows(proc, nint.Zero);
        return Task.CompletedTask;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static uint GetForegroundProcessId()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == nint.Zero) return 0;
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        return pid;
    }

    private static string? GetProcessPath(Process proc)
    {
        try { return proc.MainModule?.FileName; }
        catch { return null; } // Access denied for some processes
    }

    private static int CountWindowsForProcess(uint pid)
    {
        int count = 0;
        NativeMethods.EnumWindowsProc proc = (hWnd, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(hWnd, out var wpid);
            if (wpid == pid && NativeMethods.IsWindowVisible(hWnd) && !string.IsNullOrEmpty(GetWindowText(hWnd)))
                count++;
            return true;
        };
        NativeMethods.EnumWindows(proc, nint.Zero);
        return count;
    }

    private static nint GetMainWindowForProcess(uint pid)
    {
        nint found = nint.Zero;
        NativeMethods.EnumWindowsProc proc = (hWnd, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(hWnd, out var wpid);
            if (wpid == pid && NativeMethods.IsWindowVisible(hWnd) && !string.IsNullOrEmpty(GetWindowText(hWnd)))
            {
                found = hWnd;
                return false; // Stop enumeration
            }
            return true;
        };
        NativeMethods.EnumWindows(proc, nint.Zero);
        return found;
    }

    private static string? GetWindowText(nint hWnd)
    {
        var length = NativeMethods.GetWindowTextLength(hWnd);
        if (length == 0) return null;
        var sb = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
