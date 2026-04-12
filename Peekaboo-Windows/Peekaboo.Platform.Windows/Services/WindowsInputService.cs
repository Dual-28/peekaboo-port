using System.Runtime.InteropServices;
using Peekaboo.Core;
using Peekaboo.Platform.Windows.Native;

namespace Peekaboo.Platform.Windows.Services;

/// <summary>
/// Windows input service using SendInput for mouse and keyboard simulation.
/// </summary>
public sealed class WindowsInputService : IInputService
{
    private readonly IElementDetectionService? _elementDetection;

    public WindowsInputService(IElementDetectionService? elementDetection = null)
    {
        _elementDetection = elementDetection;
    }

    public async Task ClickAsync(ClickTarget target, ClickType clickType = ClickType.Single, CancellationToken ct = default)
    {
        var point = target switch
        {
            ClickTarget.ElementId eid => await GetElementCenterAsync(eid.Id, ct),
            ClickTarget.Coordinates coord => coord.Point,
            ClickTarget.Query q => throw new InputException("Click by query requires element detection — not yet implemented"),
            _ => throw new InputException($"Unsupported click target: {target.GetType().Name}")
        };

        NativeMethods.SetCursorPos((int)point.X, (int)point.Y);

        switch (clickType)
        {
            case ClickType.Single:
                MouseDownLeft();
                MouseUpLeft();
                break;
            case ClickType.Double:
                MouseDownLeft(); MouseUpLeft();
                MouseDownLeft(); MouseUpLeft();
                break;
            case ClickType.Right:
                MouseDownRight();
                MouseUpRight();
                break;
        }
    }

    public async Task TypeAsync(string text, ClickTarget? target = null, bool clearExisting = false, int typingDelayMs = 0, CancellationToken ct = default)
    {
        if (target != null)
        {
            await ClickAsync(target, ClickType.Single, ct);
            await Task.Delay(50, ct);
        }

        if (clearExisting)
        {
            // Ctrl+A to select all, then Delete
            SendKeyDown(VK_CONTROL);
            SendKeyDown((ushort)'A');
            SendKeyUp((ushort)'A');
            SendKeyUp(VK_CONTROL);
            await Task.Delay(30, ct);
            SendKeyDown(VK_DELETE);
            SendKeyUp(VK_DELETE);
            await Task.Delay(30, ct);
        }

        foreach (char c in text)
        {
            ct.ThrowIfCancellationRequested();
            SendChar(c);
            if (typingDelayMs > 0)
                await Task.Delay(typingDelayMs, ct);
        }
    }

    public Task HotkeyAsync(string keys, int holdDurationMs = 50, CancellationToken ct = default)
    {
        var keyParts = keys.Split(new[] { ',', '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var vks = new List<ushort>();

        foreach (var part in keyParts)
        {
            var trimmed = part.Trim().ToLowerInvariant();
            vks.Add(trimmed switch
            {
                "ctrl" or "control" => VK_CONTROL,
                "alt" => VK_MENU,
                "shift" => VK_SHIFT,
                "win" or "windows" or "meta" => VK_LWIN,
                "enter" or "return" => VK_RETURN,
                "tab" => VK_TAB,
                "esc" or "escape" => VK_ESCAPE,
                "delete" or "del" => VK_DELETE,
                "backspace" or "back" => VK_BACK,
                "up" => VK_UP,
                "down" => VK_DOWN,
                "left" => VK_LEFT,
                "right" => VK_RIGHT,
                "home" => VK_HOME,
                "end" => VK_END,
                "pageup" or "page_up" => VK_PRIOR,
                "pagedown" or "page_down" => VK_NEXT,
                "f1" => VK_F1,
                "f2" => VK_F2,
                "f3" => VK_F3,
                "f4" => VK_F4,
                "f5" => VK_F5,
                "f6" => VK_F6,
                "f7" => VK_F7,
                "f8" => VK_F8,
                "f9" => VK_F9,
                "f10" => VK_F10,
                "f11" => VK_F11,
                "f12" => VK_F12,
                _ when part.Length == 1 => (ushort)char.ToUpperInvariant(part[0]),
                _ => throw new InputException($"Unknown key: {part}")
            });
        }

        // Press all keys down
        foreach (var vk in vks)
            SendKeyDown(vk);

        // Hold
        Thread.Sleep(holdDurationMs);

        // Release in reverse order
        for (int i = vks.Count - 1; i >= 0; i--)
            SendKeyUp(vks[i]);

        return Task.CompletedTask;
    }

    public Task ScrollAsync(ScrollDirection direction, int amount = 3, ClickTarget? target = null, CancellationToken ct = default)
    {
        if (target != null)
            ClickAsync(target, ClickType.Single, ct).Wait(ct);

        int delta = direction switch
        {
            ScrollDirection.Up => amount * 120,
            ScrollDirection.Down => -amount * 120,
            ScrollDirection.Left => amount * 120,
            ScrollDirection.Right => -amount * 120,
            _ => 0
        };

        var inputs = new INPUT[1];
        inputs[0] = new INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            mi = new MOUSEINPUT
            {
                dwFlags = NativeMethods.MOUSEEVENTF_WHEEL,
                mouseData = (uint)delta,
                dwExtraInfo = NativeMethods.GetMessageExtraInfo()
            }
        };
        NativeMethods.SendInput(1, inputs, Marshal.SizeOf<INPUT>());

        return Task.CompletedTask;
    }

    public async Task DragAsync(Point from, Point to, int durationMs = 200, int steps = 10, CancellationToken ct = default)
    {
        NativeMethods.SetCursorPos((int)from.X, (int)from.Y);
        MouseDownLeft();

        for (int i = 1; i <= steps; i++)
        {
            var t = i / (double)steps;
            var x = (int)(from.X + (to.X - from.X) * t);
            var y = (int)(from.Y + (to.Y - from.Y) * t);
            NativeMethods.SetCursorPos(x, y);
            await Task.Delay(durationMs / steps, ct);
        }

        MouseUpLeft();
    }

    public Task MoveMouseAsync(Point to, int durationMs = 0, int steps = 1, CancellationToken ct = default)
    {
        if (durationMs == 0 || steps <= 1)
        {
            NativeMethods.SetCursorPos((int)to.X, (int)to.Y);
            return Task.CompletedTask;
        }

        // Get current position
        NativeMethods.GetCursorPos(out var current);

        for (int i = 1; i <= steps; i++)
        {
            var t = i / (double)steps;
            var x = (int)(current.X + (to.X - current.X) * t);
            var y = (int)(current.Y + (to.Y - current.Y) * t);
            NativeMethods.SetCursorPos(x, y);
            Thread.Sleep(durationMs / steps);
        }

        return Task.CompletedTask;
    }

    public Task<DetectedElement?> GetFocusedElementAsync(CancellationToken ct = default)
    {
        if (_elementDetection == null)
            return Task.FromResult<DetectedElement?>(null);

        // TODO: Use UIA to get the focused element directly
        return Task.FromResult<DetectedElement?>(null);
    }

    public Task<bool> WaitForElementAsync(string elementId, TimeSpan timeout, CancellationToken ct = default)
    {
        // TODO: Implement with UIA polling
        return Task.FromResult(false);
    }

    // ── Private helpers ──────────────────────────────────────────────────

    private async Task<Point> GetElementCenterAsync(string elementId, CancellationToken ct)
    {
        if (_elementDetection == null)
            throw new InputException("Element detection service not available — cannot resolve element ID");

        // We'd need a snapshot to resolve this; for now throw
        throw new InputException("Click by element ID requires a prior detect call — use Coordinates target instead");
    }

    private static void MouseDownLeft()
    {
        var inputs = new INPUT[1];
        inputs[0] = new INPUT { type = NativeMethods.INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = NativeMethods.MOUSEEVENTF_LEFTDOWN, dwExtraInfo = NativeMethods.GetMessageExtraInfo() } };
        NativeMethods.SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    private static void MouseUpLeft()
    {
        var inputs = new INPUT[1];
        inputs[0] = new INPUT { type = NativeMethods.INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = NativeMethods.MOUSEEVENTF_LEFTUP, dwExtraInfo = NativeMethods.GetMessageExtraInfo() } };
        NativeMethods.SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    private static void MouseDownRight()
    {
        var inputs = new INPUT[1];
        inputs[0] = new INPUT { type = NativeMethods.INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = NativeMethods.MOUSEEVENTF_RIGHTDOWN, dwExtraInfo = NativeMethods.GetMessageExtraInfo() } };
        NativeMethods.SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    private static void MouseUpRight()
    {
        var inputs = new INPUT[1];
        inputs[0] = new INPUT { type = NativeMethods.INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = NativeMethods.MOUSEEVENTF_RIGHTUP, dwExtraInfo = NativeMethods.GetMessageExtraInfo() } };
        NativeMethods.SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    private static void SendKeyDown(ushort vk)
    {
        var inputs = new INPUT[1];
        inputs[0] = new INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            ki = new KEYBDINPUT { wVk = vk, wScan = (ushort)NativeMethods.MapVirtualKey(vk, NativeMethods.MAPVK_VK_TO_VSC), dwExtraInfo = NativeMethods.GetMessageExtraInfo() }
        };
        NativeMethods.SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    private static void SendKeyUp(ushort vk)
    {
        var inputs = new INPUT[1];
        inputs[0] = new INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            ki = new KEYBDINPUT { wVk = vk, wScan = (ushort)NativeMethods.MapVirtualKey(vk, NativeMethods.MAPVK_VK_TO_VSC), dwFlags = NativeMethods.KEYEVENTF_KEYUP, dwExtraInfo = NativeMethods.GetMessageExtraInfo() }
        };
        NativeMethods.SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    private static void SendChar(char c)
    {
        // Use Unicode input for reliable character entry
        var inputs = new INPUT[2];
        inputs[0] = new INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            ki = new KEYBDINPUT { wScan = c, dwFlags = NativeMethods.KEYEVENTF_UNICODE, dwExtraInfo = NativeMethods.GetMessageExtraInfo() }
        };
        inputs[1] = new INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            ki = new KEYBDINPUT { wScan = c, dwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP, dwExtraInfo = NativeMethods.GetMessageExtraInfo() }
        };
        NativeMethods.SendInput(2, inputs, Marshal.SizeOf<INPUT>());
    }

    // Virtual key codes
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_MENU = 0x12; // Alt
    private const ushort VK_SHIFT = 0x10;
    private const ushort VK_LWIN = 0x5B;
    private const ushort VK_RETURN = 0x0D;
    private const ushort VK_TAB = 0x09;
    private const ushort VK_ESCAPE = 0x1B;
    private const ushort VK_DELETE = 0x2E;
    private const ushort VK_BACK = 0x08;
    private const ushort VK_UP = 0x26;
    private const ushort VK_DOWN = 0x28;
    private const ushort VK_LEFT = 0x25;
    private const ushort VK_RIGHT = 0x27;
    private const ushort VK_HOME = 0x24;
    private const ushort VK_END = 0x23;
    private const ushort VK_PRIOR = 0x21; // Page Up
    private const ushort VK_NEXT = 0x22;  // Page Down
    private const ushort VK_F1 = 0x70;
    private const ushort VK_F2 = 0x71;
    private const ushort VK_F3 = 0x72;
    private const ushort VK_F4 = 0x73;
    private const ushort VK_F5 = 0x74;
    private const ushort VK_F6 = 0x75;
    private const ushort VK_F7 = 0x76;
    private const ushort VK_F8 = 0x77;
    private const ushort VK_F9 = 0x78;
    private const ushort VK_F10 = 0x79;
    private const ushort VK_F11 = 0x7A;
    private const ushort VK_F12 = 0x7B;
}
