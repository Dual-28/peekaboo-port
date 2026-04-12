using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Extensions.Logging;
using Peekaboo.Core;
using Peekaboo.Platform.Windows.Native;

namespace Peekaboo.Platform.Windows.Services;

/// <summary>
/// Windows screen capture implementation using GDI (Bitmap.CopyFromScreen).
/// TODO: Upgrade to Windows.Graphics.Capture for better performance and per-window capture.
/// </summary>
public sealed class WindowsScreenCaptureService : IScreenCaptureService
{
    private readonly ILogger<WindowsScreenCaptureService> _logger;

    public WindowsScreenCaptureService(ILogger<WindowsScreenCaptureService> logger)
    {
        _logger = logger;
    }

    public Task<CaptureResult> CaptureScreenAsync(int? displayIndex = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var display = GetDisplay(displayIndex ?? 0);
        var bounds = display.Bounds;

        _logger.LogDebug("Capturing screen at display {Index}: {Bounds}", displayIndex, bounds);

        using var bitmap = CaptureRectangle((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height);
        var result = BitmapToCaptureResult(bitmap, CaptureMode.FullScreen, displayInfo: display);

        _logger.LogInformation("Screen capture complete: {Width}x{Height}, {Size} bytes",
            bitmap.Width, bitmap.Height, result.ImageData.Length);

        return Task.FromResult(result);
    }

    public Task<CaptureResult> CaptureWindowAsync(string appIdentifier, int? windowIndex = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        throw new NotImplementedException("Window capture by app name requires Windows.Graphics.Capture. Use CaptureWindowAsync(nint) with a known window handle instead.");
    }

    public Task<CaptureResult> CaptureWindowAsync(nint windowHandle, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!NativeMethods.GetWindowRect(windowHandle, out var rect))
            throw new CaptureException($"Failed to get window rect for handle {windowHandle:X}");

        using var bitmap = CaptureRectangle(rect.Left, rect.Top, rect.Width, rect.Height);
        var result = BitmapToCaptureResult(bitmap, CaptureMode.Window);

        _logger.LogInformation("Window capture complete: {Width}x{Height}", bitmap.Width, bitmap.Height);
        return Task.FromResult(result);
    }

    public Task<CaptureResult> CaptureFrontmostAsync(CancellationToken ct = default)
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == nint.Zero)
            throw new CaptureException("No foreground window found");

        return CaptureWindowAsync(hwnd, ct);
    }

    public Task<CaptureResult> CaptureAreaAsync(Rect rect, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var bitmap = CaptureRectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
        return Task.FromResult(BitmapToCaptureResult(bitmap, CaptureMode.Area));
    }

    public Task<bool> HasPermissionAsync(CancellationToken ct = default)
    {
        // GDI capture doesn't require special permissions on Windows (unlike macOS ScreenCaptureKit)
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<DisplayInfo>> ListDisplaysAsync(CancellationToken ct = default)
    {
        var displays = EnumerateDisplays();
        return Task.FromResult<IReadOnlyList<DisplayInfo>>(displays);
    }

    private static Bitmap CaptureRectangle(int x, int y, int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);
        return bitmap;
    }

    private static CaptureResult BitmapToCaptureResult(Bitmap bitmap, CaptureMode mode, DisplayInfo? displayInfo = null)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        var imageData = ms.ToArray();

        return new CaptureResult(
            ImageData: imageData,
            SavedPath: null,
            Metadata: new CaptureMetadata(
                Size: new Core.Size(bitmap.Width, bitmap.Height),
                Mode: mode,
                DisplayInfo: displayInfo
            )
        );
    }

    private static DisplayInfo GetDisplay(int index)
    {
        var displays = EnumerateDisplays();
        if (index < 0 || index >= displays.Count)
            throw new CaptureException($"Display index {index} is out of range. {displays.Count} display(s) available.");
        return displays[index];
    }

    private static List<DisplayInfo> EnumerateDisplays()
    {
        var displays = new List<DisplayInfo>();
        int idx = 0;

        NativeMethods.MonitorEnumProc proc = (nint hMonitor, nint hdcMonitor, ref RECT rect, nint dwData) =>
        {
            var mi = new MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
            NativeMethods.GetMonitorInfo(hMonitor, ref mi);

            var scale = GetDisplayScaleFactor(hMonitor);

            displays.Add(new DisplayInfo(
                Index: idx++,
                Name: mi.dwFlags == NativeMethods.MONITORINFOF_PRIMARY ? "Primary Display" : $"Display {idx}",
                Bounds: new Core.Rect(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Width, mi.rcMonitor.Height),
                ScaleFactor: scale
            ));
            return true;
        };

        NativeMethods.EnumDisplayMonitors(nint.Zero, nint.Zero, proc, nint.Zero);
        return displays;
    }

    private static double GetDisplayScaleFactor(nint hMonitor)
    {
        // TODO: Use GetDpiForMonitor from shcore.dll for accurate scale factor
        // For now, return 1.0 (will be correct at 100% scaling)
        return 1.0;
    }
}
