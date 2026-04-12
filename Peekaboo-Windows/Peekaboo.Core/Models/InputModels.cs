namespace Peekaboo.Core;

/// <summary>
/// Target for click operations — can be an element ID, coordinates, or a text query.
/// </summary>
public abstract record ClickTarget
{
    private ClickTarget() { }

    public sealed record ElementId(string Id) : ClickTarget;
    public sealed record Coordinates(Point Point) : ClickTarget;
    public sealed record Query(string Text) : ClickTarget;
}

/// <summary>
/// Type of click to perform.
/// </summary>
public enum ClickType
{
    Single,
    Double,
    Right
}

/// <summary>
/// Direction for scroll operations.
/// </summary>
public enum ScrollDirection
{
    Up,
    Down,
    Left,
    Right
}

/// <summary>
/// Modifier keys for hotkey and click operations.
/// </summary>
[Flags]
public enum ModifierKey
{
    None = 0,
    Control = 1,
    Alt = 2,
    Shift = 4,
    Win = 8
}

/// <summary>
/// Window targeting options for window management operations.
/// </summary>
public abstract record WindowTarget
{
    private WindowTarget() { }

    public sealed record Application(string AppName) : WindowTarget;
    public sealed record Title(string Text) : WindowTarget;
    public sealed record Index(string AppName, int WindowIndex) : WindowTarget;
    public sealed record ApplicationAndTitle(string AppName, string TitleText) : WindowTarget;
    public sealed record Frontmost : WindowTarget;
    public sealed record WindowId(int Id) : WindowTarget;
    public sealed record All : WindowTarget;
}

/// <summary>
/// Window context for element detection — provides scoping information.
/// </summary>
public record WindowContext(
    string? ApplicationName = null,
    string? ApplicationBundleId = null,
    int? ApplicationProcessId = null,
    string? WindowTitle = null,
    int? WindowId = null,
    Rect? WindowBounds = null
);
