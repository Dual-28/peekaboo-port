namespace Peekaboo.Core;

/// <summary>
/// Simple rectangle struct matching CGRect semantics.
/// </summary>
public readonly record struct Rect(double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;

    public bool Contains(double x, double y) =>
        x >= Left && x <= Right && y >= Top && y <= Bottom;

    public bool Intersects(Rect other) =>
        Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;

    public static Rect Empty => new(0, 0, 0, 0);
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

/// <summary>
/// Simple point struct.
/// </summary>
public readonly record struct Point(double X, double Y);

/// <summary>
/// Simple size struct.
/// </summary>
public readonly record struct Size(double Width, double Height);
