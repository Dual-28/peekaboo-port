namespace Peekaboo.Core;

/// <summary>
/// Element detection service — correlate screenshots with the UI Automation tree
/// to produce a list of clickable, identifiable UI elements.
/// Maps to macOS UIAutomationServiceProtocol.detectElements.
/// </summary>
public interface IElementDetectionService
{
    /// <summary>
    /// Detect UI elements from a screenshot image, correlating with the live UI Automation tree.
    /// </summary>
    /// <param name="imageData">Screenshot image as PNG/JPEG bytes.</param>
    /// <param name="windowContext">Optional window scoping information.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Detection result with element IDs, types, labels, and bounds.</returns>
    Task<ElementDetectionResult> DetectElementsAsync(
        byte[] imageData,
        WindowContext? windowContext = null,
        CancellationToken ct = default);

    /// <summary>
    /// Find a specific element in the UI Automation tree by label, identifier, or type.
    /// </summary>
    Task<DetectedElement> FindElementAsync(
        string label,
        ElementType? type = null,
        string? appName = null,
        CancellationToken ct = default);
}
