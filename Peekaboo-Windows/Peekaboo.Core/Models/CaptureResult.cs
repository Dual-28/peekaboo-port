namespace Peekaboo.Core;

/// <summary>
/// Capture mode used for a screenshot operation.
/// </summary>
public enum CaptureMode
{
    FullScreen,
    Window,
    Area,
    Frontmost
}

/// <summary>
/// Metadata about a screen capture operation.
/// </summary>
public record CaptureMetadata(
    Size Size,
    CaptureMode Mode,
    ServiceApplicationInfo? ApplicationInfo = null,
    ServiceWindowInfo? WindowInfo = null,
    DisplayInfo? DisplayInfo = null,
    DateTimeOffset? CaptureTimestamp = null
)
{
    public DateTimeOffset EffectiveTimestamp => CaptureTimestamp ?? DateTimeOffset.Now;
}

/// <summary>
/// Result of a screen capture operation.
/// </summary>
public record CaptureResult(
    byte[] ImageData,
    string? SavedPath,
    CaptureMetadata Metadata,
    string? Warning = null
);
