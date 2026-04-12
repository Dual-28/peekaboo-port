namespace Peekaboo.Core;

/// <summary>
/// Information about a taskbar item.
/// </summary>
public record TaskbarItemInfo(
    string Id,
    string Name,
    string? ProcessName,
    int ProcessId,
    bool IsActive,
    bool IsPinned,
    IntPtr WindowHandle);

/// <summary>
/// Taskbar service for interacting with Windows taskbar items.
/// </summary>
public interface ITaskbarService
{
    /// <summary>
    /// List all items in the taskbar.
    /// </summary>
    Task<IReadOnlyList<TaskbarItemInfo>> ListTaskbarItemsAsync(CancellationToken ct = default);

    /// <summary>
    /// Click a taskbar item by index or name.
    /// </summary>
    Task ClickTaskbarItemAsync(string itemId, CancellationToken ct = default);

    /// <summary>
    /// Minimize a taskbar item (hide from taskbar).
    /// </summary>
    Task HideTaskbarItemAsync(string itemId, CancellationToken ct = default);

    /// <summary>
    /// Restore a taskbar item (show in taskbar).
    /// </summary>
    Task ShowTaskbarItemAsync(string itemId, CancellationToken ct = default);
}
