namespace Peekaboo.Core;

/// <summary>
/// Input service — simulate mouse clicks, keyboard typing, hotkeys, scroll, and drag gestures.
/// Maps to macOS ClickService, TypeService, ScrollService, and parts of UIAutomationServiceProtocol.
/// </summary>
public interface IInputService
{
    /// <summary>Click at a target location or element.</summary>
    Task ClickAsync(ClickTarget target, ClickType clickType = ClickType.Single, CancellationToken ct = default);

    /// <summary>Type text at the current focus or a specific element.</summary>
    Task TypeAsync(string text, ClickTarget? target = null, bool clearExisting = false, int typingDelayMs = 0, CancellationToken ct = default);

    /// <summary>Press a hotkey combination (e.g. "ctrl,c").</summary>
    Task HotkeyAsync(string keys, int holdDurationMs = 50, CancellationToken ct = default);

    /// <summary>Scroll in the specified direction.</summary>
    Task ScrollAsync(ScrollDirection direction, int amount = 3, ClickTarget? target = null, CancellationToken ct = default);

    /// <summary>Perform a drag/swipe gesture from one point to another.</summary>
    Task DragAsync(Point from, Point to, int durationMs = 200, int steps = 10, CancellationToken ct = default);

    /// <summary>Move the mouse cursor to a specific location.</summary>
    Task MoveMouseAsync(Point to, int durationMs = 0, int steps = 1, CancellationToken ct = default);

    /// <summary>Get information about the currently focused UI element.</summary>
    Task<DetectedElement?> GetFocusedElementAsync(CancellationToken ct = default);

    /// <summary>Wait for an element matching the criteria to appear.</summary>
    Task<bool> WaitForElementAsync(string elementId, TimeSpan timeout, CancellationToken ct = default);
}
