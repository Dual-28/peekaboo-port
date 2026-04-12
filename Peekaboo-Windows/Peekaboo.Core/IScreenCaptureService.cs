using Peekaboo.Core;

namespace Peekaboo.Core;

/// <summary>
/// Screen capture service — takes screenshots of the full display, specific windows, or areas.
/// Maps to macOS ScreenCaptureServiceProtocol.
/// </summary>
public interface IScreenCaptureService
{
    /// <summary>Capture the entire primary display.</summary>
    Task<CaptureResult> CaptureScreenAsync(int? displayIndex = null, CancellationToken ct = default);

    /// <summary>Capture a specific window by its title or process name.</summary>
    Task<CaptureResult> CaptureWindowAsync(string appIdentifier, int? windowIndex = null, CancellationToken ct = default);

    /// <summary>Capture a specific window by its OS window handle.</summary>
    Task<CaptureResult> CaptureWindowAsync(nint windowHandle, CancellationToken ct = default);

    /// <summary>Capture the frontmost window of the active application.</summary>
    Task<CaptureResult> CaptureFrontmostAsync(CancellationToken ct = default);

    /// <summary>Capture a rectangular area of the screen.</summary>
    Task<CaptureResult> CaptureAreaAsync(Rect rect, CancellationToken ct = default);

    /// <summary>Check whether screen capture permissions are available.</summary>
    Task<bool> HasPermissionAsync(CancellationToken ct = default);

    /// <summary>List all available displays/monitors.</summary>
    Task<IReadOnlyList<DisplayInfo>> ListDisplaysAsync(CancellationToken ct = default);
}
