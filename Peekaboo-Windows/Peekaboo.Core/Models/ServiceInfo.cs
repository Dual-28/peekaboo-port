namespace Peekaboo.Core;

/// <summary>
/// Information about a running application.
/// On Windows, BundleIdentifier is typically null (use ProcessName instead).
/// </summary>
public record ServiceApplicationInfo(
    int ProcessId,
    string? BundleIdentifier,
    string Name,
    string? BundlePath,
    bool IsActive,
    bool IsHidden,
    int WindowCount
);

/// <summary>
/// Information about a window.
/// </summary>
public record ServiceWindowInfo(
    nint WindowId,
    string Title,
    Rect Bounds,
    bool IsMinimized,
    bool IsMainWindow,
    int WindowLevel,
    double Alpha,
    int Index,
    bool IsOnScreen,
    int? ProcessId = null,
    string? ProcessName = null
);

/// <summary>
/// Information about a display/monitor.
/// </summary>
public record DisplayInfo(
    int Index,
    string? Name,
    Rect Bounds,
    double ScaleFactor
);
