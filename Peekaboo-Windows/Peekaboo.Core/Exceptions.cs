namespace Peekaboo.Core;

/// <summary>Base exception for Peekaboo operations.</summary>
public class PeekabooException : Exception
{
    public PeekabooException(string message) : base(message) { }
    public PeekabooException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Thrown when a UI element cannot be found.</summary>
public class ElementNotFoundException : PeekabooException
{
    public ElementNotFoundException(string message) : base(message) { }
}

/// <summary>Thrown when a required permission is not granted.</summary>
public class PermissionDeniedException : PeekabooException
{
    public string PermissionName { get; }
    public PermissionDeniedException(string permission) : base($"Permission not granted: {permission}")
    {
        PermissionName = permission;
    }
}

/// <summary>Thrown when a screen capture operation fails.</summary>
public class CaptureException : PeekabooException
{
    public CaptureException(string message) : base(message) { }
    public CaptureException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Thrown when an input operation (click, type, etc.) fails.</summary>
public class InputException : PeekabooException
{
    public InputException(string message) : base(message) { }
    public InputException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Thrown when an application cannot be found or manipulated.</summary>
public class ApplicationNotFoundException : PeekabooException
{
    public string ApplicationName { get; }
    public ApplicationNotFoundException(string name) : base($"Application not found: {name}")
    {
        ApplicationName = name;
    }
}

/// <summary>Thrown when a window cannot be found or manipulated.</summary>
public class WindowNotFoundException : PeekabooException
{
    public WindowNotFoundException(string message) : base(message) { }
}
