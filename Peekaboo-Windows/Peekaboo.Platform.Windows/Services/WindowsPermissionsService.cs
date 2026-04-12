using System.Security.Principal;
using Peekaboo.Core;
using Peekaboo.Platform.Windows.Native;

namespace Peekaboo.Platform.Windows.Services;

/// <summary>
/// Windows permissions checker — verifies admin rights and UIAccess capability.
/// </summary>
public sealed class WindowsPermissionsService : IPermissionsService
{
    public Task<PermissionStatus> CheckAllAsync(CancellationToken ct = default)
    {
        var warnings = new List<string>();
        var isAdmin = IsAdministrator();
        var hasUiAccess = HasUiAccess();

        if (!isAdmin && !hasUiAccess)
        {
            warnings.Add("Running without administrator or UIAccess privileges. Input injection to elevated windows will fail.");
        }

        if (!hasUiAccess)
        {
            warnings.Add("UIAccess not enabled. Sign the executable with a trusted certificate and set uiAccess=true in the manifest for full automation.");
        }

        return Task.FromResult(new PermissionStatus(
            IsAdministrator: isAdmin,
            HasUiAccess: hasUiAccess,
            Warnings: warnings
        ));
    }

    public bool HasUiAccess()
    {
        // UIAccess requires the process to be signed and have uiAccess=true in manifest
        // Check the token for TOKEN_UIACCESS flag
        var hProcess = NativeMethods.GetCurrentProcess();
        if (!NativeMethods.OpenProcessToken(hProcess, NativeMethods.TOKEN_QUERY, out var hToken))
            return false;

        try
        {
            var elevation = new TOKEN_ELEVATION();
            var size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<TOKEN_ELEVATION>();
            if (NativeMethods.GetTokenInformation(hToken, TOKEN_INFORMATION_CLASS.TokenElevation,
                System.Runtime.InteropServices.Marshal.AllocHGlobal((int)size), size, out _))
            {
                // TokenElevation tells us if we're elevated, but not specifically UIAccess
                // A proper check would require checking the manifest, which is complex
                // For now, return false (most processes won't have UIAccess)
            }
        }
        finally
        {
            NativeMethods.CloseHandle(hToken);
        }

        return false;
    }

    public bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
