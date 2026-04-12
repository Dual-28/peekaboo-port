using System.Runtime.InteropServices;
using System.Text;
using Peekaboo.Core;
using Peekaboo.Platform.Windows.Native;

namespace Peekaboo.Platform.Windows.Services;

/// <summary>
/// Windows clipboard service using Win32 clipboard APIs.
/// </summary>
public sealed class WindowsClipboardService : IClipboardService
{
    public Task<string?> GetTextAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!NativeMethods.OpenClipboard(nint.Zero))
            return Task.FromResult<string?>(null);

        try
        {
            var hData = NativeMethods.GetClipboardData(NativeMethods.CF_UNICODETEXT);
            if (hData == nint.Zero) return Task.FromResult<string?>(null);

            var ptr = NativeMethods.GlobalLock(hData);
            if (ptr == nint.Zero) return Task.FromResult<string?>(null);

            try
            {
                return Task.FromResult<string?>(Marshal.PtrToStringUni(ptr));
            }
            finally
            {
                NativeMethods.GlobalUnlock(hData);
            }
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }

    public Task SetTextAsync(string text, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!NativeMethods.OpenClipboard(nint.Zero))
            throw new PeekabooException("Failed to open clipboard");

        try
        {
            NativeMethods.EmptyClipboard();

            var bytes = Encoding.Unicode.GetBytes(text + "\0");
            var hGlobal = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE | NativeMethods.GMEM_ZEROINIT, (nuint)bytes.Length);
            if (hGlobal == nint.Zero)
                throw new PeekabooException("Failed to allocate clipboard memory");

            var ptr = NativeMethods.GlobalLock(hGlobal);
            try
            {
                Marshal.Copy(bytes, 0, ptr, bytes.Length);
            }
            finally
            {
                NativeMethods.GlobalUnlock(hGlobal);
            }

            NativeMethods.SetClipboardData(NativeMethods.CF_UNICODETEXT, hGlobal);
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetFilesAsync(CancellationToken ct = default)
    {
        // TODO: Implement CF_HDROP parsing
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    public Task SetFilesAsync(IEnumerable<string> filePaths, CancellationToken ct = default)
    {
        // TODO: Implement CF_HDROP creation
        return Task.CompletedTask;
    }
}
