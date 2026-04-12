namespace Peekaboo.Core;

/// <summary>
/// Menu item information returned from menu discovery.
/// </summary>
public record MenuItemInfo(
    string Id,
    string Label,
    string? Shortcut,
    bool IsEnabled,
    bool IsChecked,
    bool HasSubmenu,
    int Position);

/// <summary>
/// Target for menu operations - can target app menu bar or system menu.
/// </summary>
public record MenuTarget(
    string? ApplicationName = null,  // App to target (null = frontmost)
    bool UseSystemMenu = false);     // Use system menu instead of app menu

/// <summary>
/// Menu discovery service - lists and clicks menu items for applications.
/// This provides Windows equivalent of macOS menu bar access.
/// </summary>
public interface IMenuDiscoveryService
{
    /// <summary>
    /// List all menu items in the menu bar for an application.
    /// Returns top-level menus (File, Edit, View, etc.) with their items.
    /// </summary>
    Task<IReadOnlyList<MenuItemInfo>> ListMenuItemsAsync(MenuTarget target, CancellationToken ct = default);

    /// <summary>
    /// Click a menu item by path (e.g., "File > Save As" or "Edit > Undo").
    /// </summary>
    Task ClickMenuItemAsync(MenuTarget target, string menuPath, CancellationToken ct = default);

    /// <summary>
    /// Check if an application has a menu bar (most GUI apps do).
    /// </summary>
    Task<bool> HasMenuBarAsync(string applicationName, CancellationToken ct = default);
}
