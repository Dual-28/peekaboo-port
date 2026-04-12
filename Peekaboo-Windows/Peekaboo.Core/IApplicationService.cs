namespace Peekaboo.Core;

/// <summary>
/// Application management service — list, launch, activate, and quit applications.
/// Maps to macOS ApplicationServiceProtocol.
/// </summary>
public interface IApplicationService
{
    /// <summary>List all running applications with their details.</summary>
    Task<IReadOnlyList<ServiceApplicationInfo>> ListApplicationsAsync(CancellationToken ct = default);

    /// <summary>Find an application by name or process identifier.</summary>
    Task<ServiceApplicationInfo> FindApplicationAsync(string identifier, CancellationToken ct = default);

    /// <summary>List all windows belonging to a specific application.</summary>
    Task<IReadOnlyList<ServiceWindowInfo>> ListWindowsAsync(string appIdentifier, CancellationToken ct = default);

    /// <summary>Get information about the currently active (foreground) application.</summary>
    Task<ServiceApplicationInfo> GetForegroundApplicationAsync(CancellationToken ct = default);

    /// <summary>Check whether an application is currently running.</summary>
    Task<bool> IsApplicationRunningAsync(string identifier, CancellationToken ct = default);

    /// <summary>Launch an application by name or path.</summary>
    Task<ServiceApplicationInfo> LaunchApplicationAsync(string identifier, CancellationToken ct = default);

    /// <summary>Activate (bring to foreground) an application.</summary>
    Task ActivateApplicationAsync(string identifier, CancellationToken ct = default);

    /// <summary>Quit an application gracefully.</summary>
    Task<bool> QuitApplicationAsync(string identifier, bool force = false, CancellationToken ct = default);

    /// <summary>Hide (minimize) all windows of an application.</summary>
    Task HideApplicationAsync(string identifier, CancellationToken ct = default);

    /// <summary>Show all hidden windows of an application.</summary>
    Task UnhideApplicationAsync(string identifier, CancellationToken ct = default);
}
