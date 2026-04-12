namespace Peekaboo.Core;

/// <summary>
/// Permissions service — check whether the required OS-level permissions are granted.
/// On Windows this covers UIAccess, administrator rights, and integrity level checks.
/// </summary>
public interface IPermissionsService
{
    /// <summary>Check all required permissions for full automation capability.</summary>
    Task<PermissionStatus> CheckAllAsync(CancellationToken ct = default);

    /// <summary>Check whether the process has UIAccess capability (needed for input injection to elevated windows).</summary>
    bool HasUiAccess();

    /// <summary>Check whether the process is running as administrator.</summary>
    bool IsAdministrator();
}

/// <summary>Overall permission check result.</summary>
public record PermissionStatus(
    bool IsAdministrator,
    bool HasUiAccess,
    IReadOnlyList<string> Warnings
)
{
    public bool IsFullyCapable => IsAdministrator || HasUiAccess;
}
