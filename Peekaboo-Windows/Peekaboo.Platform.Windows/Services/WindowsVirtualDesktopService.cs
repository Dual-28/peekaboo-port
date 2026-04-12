using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Peekaboo.Core;

namespace Peekaboo.Platform.Windows.Services;

/// <summary>
/// Windows virtual desktop service using IVirtualDesktopManager COM interop.
/// </summary>
public sealed class WindowsVirtualDesktopService : IVirtualDesktopService
{
    private readonly ILogger<WindowsVirtualDesktopService> _logger;

    public WindowsVirtualDesktopService(ILogger<WindowsVirtualDesktopService> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<VirtualDesktopInfo>> ListDesktopsAsync(CancellationToken ct = default)
    {
        // Note: Full IVirtualDesktopManager implementation requires COM interop
        // This is a simplified placeholder that returns the current desktop
        _logger.LogInformation("Listing virtual desktops");
        
        var desktops = new List<VirtualDesktopInfo>
        {
            new("desktop-1", "Desktop 1", 0, true)
        };
        
        return Task.FromResult<IReadOnlyList<VirtualDesktopInfo>>(desktops);
    }

    public Task SwitchToDesktopAsync(string desktopId, CancellationToken ct = default)
    {
        _logger.LogInformation("Switching to desktop: {DesktopId}", desktopId);
        return Task.FromException(new NotSupportedException(
            "Switching virtual desktops is not implemented in this Windows build. space_list can report the current desktop placeholder only."));
    }

    public Task<string> CreateDesktopAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Creating new virtual desktop");
        return Task.FromException<string>(new NotSupportedException(
            "Creating virtual desktops is not implemented in this Windows build."));
    }

    public Task DeleteDesktopAsync(string desktopId, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting desktop: {DesktopId}", desktopId);
        return Task.FromException(new NotSupportedException(
            "Deleting virtual desktops is not implemented in this Windows build."));
    }

    public Task MoveWindowToDesktopAsync(string windowTitle, string desktopId, CancellationToken ct = default)
    {
        _logger.LogInformation("Moving window '{WindowTitle}' to desktop {DesktopId}", windowTitle, desktopId);
        return Task.FromException(new NotSupportedException(
            "Moving windows between virtual desktops is not implemented in this Windows build."));
    }
}
