namespace Peekaboo.Core;

/// <summary>
/// Clipboard service — get and set clipboard content (text, files).
/// </summary>
public interface IClipboardService
{
    /// <summary>Get the current clipboard text, if any.</summary>
    Task<string?> GetTextAsync(CancellationToken ct = default);

    /// <summary>Set clipboard text content.</summary>
    Task SetTextAsync(string text, CancellationToken ct = default);

    /// <summary>Get file paths currently on the clipboard, if any.</summary>
    Task<IReadOnlyList<string>> GetFilesAsync(CancellationToken ct = default);

    /// <summary>Set file paths on the clipboard.</summary>
    Task SetFilesAsync(IEnumerable<string> filePaths, CancellationToken ct = default);
}
