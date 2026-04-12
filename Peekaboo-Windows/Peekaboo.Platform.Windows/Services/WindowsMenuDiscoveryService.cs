using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Peekaboo.Core;

namespace Peekaboo.Platform.Windows.Services;

/// <summary>
/// Windows menu discovery using Win32 APIs (GetMenu, GetSubMenu).
/// Lists menu items and supports clicking via menu path (e.g., "File > Save As").
/// </summary>
public sealed class WindowsMenuDiscoveryService : IMenuDiscoveryService
{
    private readonly ILogger<WindowsMenuDiscoveryService> _logger;

    public WindowsMenuDiscoveryService(ILogger<WindowsMenuDiscoveryService> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<MenuItemInfo>> ListMenuItemsAsync(MenuTarget target, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var items = new List<MenuItemInfo>();

        try
        {
            var hwnd = target.ApplicationName != null
                ? FindWindowByName(target.ApplicationName)
                : Win32.GetForegroundWindow();

            if (hwnd == 0)
            {
                _logger.LogWarning("Could not find window for target: {Target}", target.ApplicationName ?? "frontmost");
                return Task.FromResult<IReadOnlyList<MenuItemInfo>>(items);
            }

            // Use system menu if requested
            IntPtr hMenu = target.UseSystemMenu 
                ? Win32.GetSystemMenu(hwnd, false) 
                : Win32.GetMenu(hwnd);
            
            if (hMenu == IntPtr.Zero)
            {
                _logger.LogDebug("No menu found for window (UseSystemMenu: {UseSystemMenu})", target.UseSystemMenu);
                return Task.FromResult<IReadOnlyList<MenuItemInfo>>(items);
            }

            // Recursively collect menu items including submenus
            CollectMenuItemsRecursive(hMenu, items, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing menu items");
        }

        return Task.FromResult<IReadOnlyList<MenuItemInfo>>(items);
    }

    public Task ClickMenuItemAsync(MenuTarget target, string menuPath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var hwnd = target.ApplicationName != null
                ? FindWindowByName(target.ApplicationName)
                : Win32.GetForegroundWindow();

            if (hwnd == 0)
            {
                throw new InvalidOperationException($"Could not find window for {target.ApplicationName ?? "frontmost"}");
            }

            var parts = menuPath.Split(" > ").Select(p => p.Trim()).ToArray();
            if (parts.Length < 1)
            {
                throw new ArgumentException("Menu path must have at least one item");
            }

            var hMenu = Win32.GetMenu(hwnd);
            if (hMenu == 0)
            {
                throw new InvalidOperationException("No menu found for window");
            }

            nint currentMenu = hMenu;
            for (int i = 0; i < parts.Length; i++)
            {
                var partName = parts[i];
                int itemCount = Win32.GetMenuItemCount(currentMenu);
                int targetIndex = -1;

                for (int j = 0; j < itemCount; j++)
                {
                    var info = GetMenuItemInfo(currentMenu, j, (uint)i);
                    if (info != null && info.Label.Equals(partName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetIndex = j;
                        break;
                    }
                }

                if (targetIndex == -1)
                {
                    throw new InvalidOperationException($"Menu item '{partName}' not found at path {menuPath}");
                }

                if (i < parts.Length - 1)
                {
                    var subMenu = Win32.GetSubMenu(currentMenu, targetIndex);
                    if (subMenu == 0)
                    {
                        throw new InvalidOperationException($"Menu item '{partName}' has no submenu");
                    }
                    currentMenu = subMenu;
                }
                else
                {
                    Win32.SetForegroundWindow(hwnd);
                    Thread.Sleep(50);

                    var mii = new MENUITEMINFO
                    {
                        cbSize = (uint)Marshal.SizeOf<MENUITEMINFO>(),
                        fMask = 0x00000001
                    };
                    if (Win32.GetMenuItemInfo(currentMenu, (uint)targetIndex, true, ref mii))
                    {
                        Win32.SendMessage(hwnd, 0x0111, (nint)mii.wID, 0);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clicking menu item: {Path}", menuPath);
            throw;
        }

        return Task.CompletedTask;
    }

    public Task<bool> HasMenuBarAsync(string applicationName, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var hwnd = FindWindowByName(applicationName);
            if (hwnd == 0) return Task.FromResult(false);

            var hMenu = Win32.GetMenu(hwnd);
            return Task.FromResult(hMenu != 0);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private MenuItemInfo? GetMenuItemInfo(nint hMenu, int index, uint pos)
    {
        var mii = new MENUITEMINFO
        {
            cbSize = (uint)Marshal.SizeOf<MENUITEMINFO>(),
            fMask = 0x00000001 | 0x00000002 | 0x00000004 | 0x00000010,
            dwTypeData = new string('\0', 256),
            cch = 256
        };

        if (!Win32.GetMenuItemInfo(hMenu, (uint)index, true, ref mii))
        {
            return null;
        }

        var label = new string(mii.dwTypeData.TakeWhile(c => c != '\0').ToArray());
        if (string.IsNullOrEmpty(label)) return null;

        return new MenuItemInfo(
            Id: $"menu_{pos}_{index}",
            Label: label.Replace("&", ""),
            Shortcut: null,
            IsEnabled: (mii.fState & 0x00000002) == 0,
            IsChecked: (mii.fState & 0x00000008) != 0,
            HasSubmenu: mii.hSubMenu != IntPtr.Zero,
            Position: (int)pos + index);
    }

    private void CollectMenuItemsRecursive(nint hMenu, List<MenuItemInfo> items, uint pos)
    {
        try
        {
            int itemCount = Win32.GetMenuItemCount(hMenu);
            for (int i = 0; i < itemCount; i++)
            {
                var itemInfo = GetMenuItemInfo(hMenu, i, pos);
                if (itemInfo != null)
                {
                    items.Add(itemInfo);
                    // Recursively collect submenu items
                    var subMenu = Win32.GetSubMenu(hMenu, i);
                    if (subMenu != IntPtr.Zero)
                    {
                        CollectMenuItemsRecursive(subMenu, items, (uint)items.Count);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recursively collecting menu items");
        }
    }
            IsEnabled: (mii.fState & 0x0001) == 0,
            IsChecked: (mii.fState & 0x0002) != 0,
            HasSubmenu: mii.hSubMenu != 0,
            Position: index
        );
    }

    private static nint FindWindowByName(string name)
    {
        nint result = 0;

        Win32.EnumWindows((hWnd, _) =>
        {
            int length = Win32.GetWindowTextLength(hWnd);
            if (length == 0) return true;

            var sb = new StringBuilder(length + 1);
            Win32.GetWindowText(hWnd, sb, sb.Capacity);
            var windowName = sb.ToString();

            if (windowName.Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                result = hWnd;
                return false;
            }
            return true;
        }, 0);

        return result;
    }
}

// Win32 menu P/Invoke
internal static class Win32
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
    public static extern nint SendMessage(nint hWnd, uint Msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    public static extern nint GetMenu(nint hWnd);

    [DllImport("user32.dll")]
    public static extern nint GetSystemMenu(nint hWnd, [MarshalAs(UnmanagedType.Bool)] bool bRevert);

    [DllImport("user32.dll")]
    public static extern int GetMenuItemCount(nint hMenu);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMenuItemInfo(nint hMenu, uint uItem, [MarshalAs(UnmanagedType.Bool)] bool fByPosition, ref MENUITEMINFO lpmii);

    [DllImport("user32.dll")]
    public static extern nint GetSubMenu(nint hMenu, int nPos);
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct MENUITEMINFO
{
    public uint cbSize;
    public uint fMask;
    public uint fType;
    public uint fState;
    public uint wID;
    public nint hSubMenu;
    public nint hbmpChecked;
    public nint hbmpUnchecked;
    public nint dwItemData;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string dwTypeData;
    public uint cch;
    public nint hbmpItem;
}
