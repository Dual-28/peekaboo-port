namespace Peekaboo.Core;

/// <summary>
/// A detected UI element on screen, analogous to the Swift DetectedElement struct.
/// Elements are assigned IDs like "B1" (Button 1), "T2" (TextField 2) for click targeting.
/// </summary>
public record DetectedElement(
    string Id,
    ElementType Type,
    string? Label,
    string? Value,
    Rect Bounds,
    bool IsEnabled,
    bool? IsSelected,
    Dictionary<string, string> Attributes
);

/// <summary>
/// Container for detected UI elements organized by type.
/// </summary>
public record DetectedElements(
    IReadOnlyList<DetectedElement> Buttons,
    IReadOnlyList<DetectedElement> TextFields,
    IReadOnlyList<DetectedElement> Links,
    IReadOnlyList<DetectedElement> Images,
    IReadOnlyList<DetectedElement> Groups,
    IReadOnlyList<DetectedElement> Sliders,
    IReadOnlyList<DetectedElement> Checkboxes,
    IReadOnlyList<DetectedElement> Menus,
    IReadOnlyList<DetectedElement> Other
)
{
    /// <summary>All elements as a flat list.</summary>
    public IReadOnlyList<DetectedElement> All =>
        Buttons.Concat(TextFields).Concat(Links).Concat(Images)
            .Concat(Groups).Concat(Sliders).Concat(Checkboxes).Concat(Menus).Concat(Other).ToList();

    /// <summary>Find an element by its ID (e.g. "B1").</summary>
    public DetectedElement? FindById(string id) => All.FirstOrDefault(e => e.Id == id);
}

/// <summary>
/// Metadata about an element detection operation.
/// </summary>
public record DetectionMetadata(
    TimeSpan DetectionTime,
    int ElementCount,
    string Method,
    IReadOnlyList<string>? Warnings = null,
    WindowContext? WindowContext = null,
    bool IsDialog = false
);

/// <summary>
/// Result of an element detection operation.
/// </summary>
public record ElementDetectionResult(
    string SnapshotId,
    string? ScreenshotPath,
    DetectedElements Elements,
    DetectionMetadata Metadata
);
