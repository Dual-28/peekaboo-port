namespace Peekaboo.Core;

/// <summary>
/// Virtual desktop (Windows Spaces) service.
/// </summary>
public interface IVirtualDesktopService
{
    /// <summary>List all virtual desktops.</summary>
    Task<IReadOnlyList<VirtualDesktopInfo>> ListDesktopsAsync(CancellationToken ct = default);

    /// <summary>Switch to a specific desktop.</summary>
    Task SwitchToDesktopAsync(string desktopId, CancellationToken ct = default);

    /// <summary>Create a new virtual desktop.</summary>
    Task<string> CreateDesktopAsync(CancellationToken ct = default);

    /// <summary>Delete a virtual desktop.</summary>
    Task DeleteDesktopAsync(string desktopId, CancellationToken ct = default);

    /// <summary>Move a window to a different desktop.</summary>
    Task MoveWindowToDesktopAsync(string windowTitle, string desktopId, CancellationToken ct = default);
}

/// <summary>Information about a virtual desktop.</summary>
public record VirtualDesktopInfo(
    string Id,
    string Name,
    int Index,
    bool IsCurrent
);
