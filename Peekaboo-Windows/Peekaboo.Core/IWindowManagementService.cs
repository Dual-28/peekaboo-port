namespace Peekaboo.Core;

/// <summary>
/// Window management service — move, resize, focus, close, minimize, maximize windows.
/// Maps to macOS WindowManagementServiceProtocol.
/// </summary>
public interface IWindowManagementService
{
    /// <summary>Close a window.</summary>
    Task CloseWindowAsync(WindowTarget target, CancellationToken ct = default);

    /// <summary>Minimize a window to the taskbar.</summary>
    Task MinimizeWindowAsync(WindowTarget target, CancellationToken ct = default);

    /// <summary>Maximize a window to fill the screen.</summary>
    Task MaximizeWindowAsync(WindowTarget target, CancellationToken ct = default);

    /// <summary>Move a window to specific screen coordinates.</summary>
    Task MoveWindowAsync(WindowTarget target, Point position, CancellationToken ct = default);

    /// <summary>Resize a window to specific dimensions.</summary>
    Task ResizeWindowAsync(WindowTarget target, Size size, CancellationToken ct = default);

    /// <summary>Set both position and size of a window.</summary>
    Task SetWindowBoundsAsync(WindowTarget target, Rect bounds, CancellationToken ct = default);

    /// <summary>Bring a window to the foreground and give it focus.</summary>
    Task FocusWindowAsync(WindowTarget target, CancellationToken ct = default);

    /// <summary>List all windows matching the given target criteria.</summary>
    Task<IReadOnlyList<ServiceWindowInfo>> ListWindowsAsync(WindowTarget target, CancellationToken ct = default);

    /// <summary>Get the currently focused window.</summary>
    Task<ServiceWindowInfo?> GetFocusedWindowAsync(CancellationToken ct = default);
}
