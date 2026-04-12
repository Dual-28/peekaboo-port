namespace Peekaboo.Core;

/// <summary>
/// Dialog button types.
/// </summary>
public enum DialogButton
{
    Ok,
    Cancel,
    Open,
    Save,
    Yes,
    No
}

/// <summary>
/// Dialog service for driving system file open/save dialogs.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Set the file path in an open/save dialog.
    /// </summary>
    Task SetPathAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Set the file type filter (e.g., "Text Files|*.txt|All Files|*.*")
    /// </summary>
    Task SetFilterAsync(string filter, CancellationToken ct = default);

    /// <summary>
    /// Click a button in the dialog.
    /// </summary>
    Task ClickButtonAsync(DialogButton button, CancellationToken ct = default);

    /// <summary>
    /// Wait for a dialog to appear.
    /// </summary>
    Task<bool> WaitForDialogAsync(int timeoutMs = 5000, CancellationToken ct = default);
}
