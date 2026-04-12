using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Peekaboo.Core;
using Peekaboo.Platform.Windows.Services;

namespace Peekaboo.Cli;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static int Main(string[] args)
    {
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Output results as JSON",
            Recursive = true
        };

        // ═══════════════════════════════════════════
        // CAPTURE COMMANDS
        // ═══════════════════════════════════════════
        var captureScreenCmd = new Command("screen", "Capture the entire screen");
        var displayIndexOpt = new Option<int?>("--display") { Description = "Display index (0-based)" };
        captureScreenCmd.Options.Add(displayIndexOpt);
        captureScreenCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var display = parseResult.GetValue(displayIndexOpt);
            await using var sp = CreateServices(json);
            var capture = sp.GetRequiredService<IScreenCaptureService>();
            var result = await capture.CaptureScreenAsync(display);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, mode = "screen", width = result.Metadata.Size.Width, height = result.Metadata.Size.Height, path = result.SavedPath ?? "(in-memory)", byteCount = result.ImageData.Length }, JsonOpts));
            else
                Console.WriteLine($"Captured screen ({result.Metadata.Size.Width}x{result.Metadata.Size.Height}) - {result.ImageData.Length} bytes");
        });

        var captureWindowCmd = new Command("window", "Capture a specific window");
        var capAppOpt = new Option<string>("--app") { Description = "Application name" };
        var capWinIdxOpt = new Option<int?>("--window-index") { Description = "Window index" };
        captureWindowCmd.Options.Add(capAppOpt);
        captureWindowCmd.Options.Add(capWinIdxOpt);
        captureWindowCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var app = parseResult.GetValue(capAppOpt);
            var wIdx = parseResult.GetValue(capWinIdxOpt);
            if (string.IsNullOrEmpty(app))
            {
                Console.Error.WriteLine("Error: --app is required");
                return 1;
            }
            await using var sp = CreateServices(json);
            var capture = sp.GetRequiredService<IScreenCaptureService>();
            var result = await capture.CaptureWindowAsync(app!, wIdx);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, mode = "window", app, width = result.Metadata.Size.Width, height = result.Metadata.Size.Height, byteCount = result.ImageData.Length }, JsonOpts));
            else
                Console.WriteLine($"Captured window '{app}' ({result.Metadata.Size.Width}x{result.Metadata.Size.Height})");
            return 0;
        });

        var captureAreaCmd = new Command("area", "Capture a rectangular area");
        var areaX = new Option<int>("--x") { Description = "X coordinate" };
        var areaY = new Option<int>("--y") { Description = "Y coordinate" };
        var areaW = new Option<int>("--width") { Description = "Width" };
        var areaH = new Option<int>("--height") { Description = "Height" };
        captureAreaCmd.Options.Add(areaX);
        captureAreaCmd.Options.Add(areaY);
        captureAreaCmd.Options.Add(areaW);
        captureAreaCmd.Options.Add(areaH);
        captureAreaCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var x = parseResult.GetValue(areaX);
            var y = parseResult.GetValue(areaY);
            var w = parseResult.GetValue(areaW);
            var h = parseResult.GetValue(areaH);
            await using var sp = CreateServices(json);
            var capture = sp.GetRequiredService<IScreenCaptureService>();
            var result = await capture.CaptureAreaAsync(new Rect(x, y, w, h));
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, mode = "area", x, y, width = w, height = h, byteCount = result.ImageData.Length }, JsonOpts));
            else
                Console.WriteLine($"Captured area ({x},{y},{w}x{h}) - {result.ImageData.Length} bytes");
        });

        var captureFrontmostCmd = new Command("frontmost", "Capture the frontmost window");
        captureFrontmostCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            await using var sp = CreateServices(json);
            var capture = sp.GetRequiredService<IScreenCaptureService>();
            var result = await capture.CaptureFrontmostAsync();
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, mode = "frontmost", width = result.Metadata.Size.Width, height = result.Metadata.Size.Height, byteCount = result.ImageData.Length }, JsonOpts));
            else
                Console.WriteLine($"Captured frontmost window ({result.Metadata.Size.Width}x{result.Metadata.Size.Height})");
        });

        var captureCmd = new Command("capture", "Capture screenshots");
        captureCmd.Subcommands.Add(captureScreenCmd);
        captureCmd.Subcommands.Add(captureWindowCmd);
        captureCmd.Subcommands.Add(captureAreaCmd);
        captureCmd.Subcommands.Add(captureFrontmostCmd);

        // ═══════════════════════════════════════════
        // SEE COMMAND
        // ═══════════════════════════════════════════
        var seeCmd = new Command("see", "Detect UI elements on screen");
        var seeAppOpt = new Option<string?>("--app") { Description = "Scope to application" };
        var seeWindowOpt = new Option<string?>("--window") { Description = "Scope to window title" };
        seeCmd.Options.Add(seeAppOpt);
        seeCmd.Options.Add(seeWindowOpt);
        seeCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var app = parseResult.GetValue(seeAppOpt);
            var window = parseResult.GetValue(seeWindowOpt);
            await using var sp = CreateServices(json);
            var capture = sp.GetRequiredService<IScreenCaptureService>();
            var detection = sp.GetRequiredService<IElementDetectionService>();
            var shot = await capture.CaptureFrontmostAsync();
            var wCtx = string.IsNullOrEmpty(app) && string.IsNullOrEmpty(window)
                ? null
                : new WindowContext(ApplicationName: app, WindowTitle: window);
            var result = await detection.DetectElementsAsync(shot.ImageData, wCtx);
            if (json)
            {
                var output = new
                {
                    success = true,
                    snapshotId = result.SnapshotId,
                    elementCount = result.Metadata.ElementCount,
                    buttons = result.Elements.Buttons.Count,
                    textFields = result.Elements.TextFields.Count,
                    links = result.Elements.Links.Count,
                    images = result.Elements.Images.Count,
                    groups = result.Elements.Groups.Count,
                    other = result.Elements.Other.Count,
                    detectionTimeMs = result.Metadata.DetectionTime.TotalMilliseconds,
                };
                Console.WriteLine(JsonSerializer.Serialize(output, JsonOpts));
            }
            else
            {
                Console.WriteLine($"Detected {result.Metadata.ElementCount} elements in {result.Metadata.DetectionTime.TotalMilliseconds:F0}ms:");
                Console.WriteLine($"  Buttons: {result.Elements.Buttons.Count}");
                Console.WriteLine($"  Text Fields: {result.Elements.TextFields.Count}");
                Console.WriteLine($"  Links: {result.Elements.Links.Count}");
                Console.WriteLine($"  Images: {result.Elements.Images.Count}");
                Console.WriteLine($"  Groups: {result.Elements.Groups.Count}");
                Console.WriteLine($"  Other: {result.Elements.Other.Count}");
            }
        });

        // ═══════════════════════════════════════════
        // CLICK COMMAND
        // ═══════════════════════════════════════════
        var clickCmd = new Command("click", "Click at a location or element");
        var clickXOpt = new Option<int?>("--x") { Description = "X coordinate" };
        var clickYOpt = new Option<int?>("--y") { Description = "Y coordinate" };
        var clickElOpt = new Option<string?>("--element-id") { Description = "Element ID to click" };
        var clickTypeOpt = new Option<ClickType>("--type") { Description = "Click type", DefaultValueFactory = _ => ClickType.Single };
        clickCmd.Options.Add(clickXOpt);
        clickCmd.Options.Add(clickYOpt);
        clickCmd.Options.Add(clickElOpt);
        clickCmd.Options.Add(clickTypeOpt);
        clickCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var x = parseResult.GetValue(clickXOpt);
            var y = parseResult.GetValue(clickYOpt);
            var elId = parseResult.GetValue(clickElOpt);
            var type = parseResult.GetValue(clickTypeOpt);
            ClickTarget target;
            if (!string.IsNullOrEmpty(elId))
                target = new ClickTarget.ElementId(elId!);
            else if (x.HasValue && y.HasValue)
                target = new ClickTarget.Coordinates(new Point(x.Value, y.Value));
            else
            {
                Console.Error.WriteLine("Error: provide --x/--y or --element-id");
                return 1;
            }
            await using var sp = CreateServices(json);
            var input = sp.GetRequiredService<IInputService>();
            await input.ClickAsync(target, type);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "click", type = type.ToString() }, JsonOpts));
            else
                Console.WriteLine($"Clicked ({type})");
            return 0;
        });

        // ═══════════════════════════════════════════
        // TYPE COMMAND
        // ═══════════════════════════════════════════
        var typeCmd = new Command("type", "Type text");
        var typeTextOpt = new Option<string>("--text") { Description = "Text to type" };
        var typeClearOpt = new Option<bool>("--clear") { Description = "Clear existing text first" };
        typeCmd.Options.Add(typeTextOpt);
        typeCmd.Options.Add(typeClearOpt);
        typeCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var text = parseResult.GetValue(typeTextOpt);
            var clear = parseResult.GetValue(typeClearOpt);
            if (string.IsNullOrEmpty(text))
            {
                Console.Error.WriteLine("Error: --text is required");
                return 1;
            }
            await using var sp = CreateServices(json);
            var input = sp.GetRequiredService<IInputService>();
            await input.TypeAsync(text!, clearExisting: clear);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "type", length = text!.Length }, JsonOpts));
            else
                Console.WriteLine($"Typed {text!.Length} characters");
            return 0;
        });

        // ═══════════════════════════════════════════
        // HOTKEY COMMAND
        // ═══════════════════════════════════════════
        var hotkeyCmd = new Command("hotkey", "Press a hotkey combination");
        var hotkeyArg = new Argument<string>("keys") { Description = "Keys (e.g. ctrl,c)" };
        hotkeyCmd.Arguments.Add(hotkeyArg);
        hotkeyCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var keys = parseResult.GetValue(hotkeyArg);
            if (string.IsNullOrEmpty(keys))
            {
                Console.Error.WriteLine("Error: keys argument is required");
                return 1;
            }
            await using var sp = CreateServices(json);
            var input = sp.GetRequiredService<IInputService>();
            await input.HotkeyAsync(keys!);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "hotkey", keys }, JsonOpts));
            else
                Console.WriteLine($"Pressed {keys}");
            return 0;
        });

        // ═══════════════════════════════════════════
        // SCROLL COMMAND
        // ═══════════════════════════════════════════
        var scrollCmd = new Command("scroll", "Scroll in a direction");
        var scrollDirOpt = new Option<ScrollDirection>("--direction") { Description = "Scroll direction" };
        var scrollAmtOpt = new Option<int>("--amount") { Description = "Scroll amount", DefaultValueFactory = _ => 3 };
        scrollCmd.Options.Add(scrollDirOpt);
        scrollCmd.Options.Add(scrollAmtOpt);
        scrollCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var dir = parseResult.GetValue(scrollDirOpt);
            var amt = parseResult.GetValue(scrollAmtOpt);
            await using var sp = CreateServices(json);
            var input = sp.GetRequiredService<IInputService>();
            await input.ScrollAsync(dir, amt);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "scroll", direction = dir.ToString(), amount = amt }, JsonOpts));
            else
                Console.WriteLine($"Scrolled {dir} ({amt} notches)");
        });

        // ═══════════════════════════════════════════
        // DRAG COMMAND
        // ═══════════════════════════════════════════
        var dragCmd = new Command("drag", "Drag from one point to another");
        var dragFromX = new Option<int>("--from-x") { Description = "Start X" };
        var dragFromY = new Option<int>("--from-y") { Description = "Start Y" };
        var dragToX = new Option<int>("--to-x") { Description = "End X" };
        var dragToY = new Option<int>("--to-y") { Description = "End Y" };
        dragCmd.Options.Add(dragFromX);
        dragCmd.Options.Add(dragFromY);
        dragCmd.Options.Add(dragToX);
        dragCmd.Options.Add(dragToY);
        dragCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var fx = parseResult.GetValue(dragFromX);
            var fy = parseResult.GetValue(dragFromY);
            var tx = parseResult.GetValue(dragToX);
            var ty = parseResult.GetValue(dragToY);
            await using var sp = CreateServices(json);
            var input = sp.GetRequiredService<IInputService>();
            await input.DragAsync(new Point(fx, fy), new Point(tx, ty));
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "drag", from = new { x = fx, y = fy }, to = new { x = tx, y = ty } }, JsonOpts));
            else
                Console.WriteLine($"Dragged ({fx},{fy}) -> ({tx},{ty})");
        });

        // ═══════════════════════════════════════════
        // APP COMMANDS
        // ═══════════════════════════════════════════
        var appListCmd = new Command("list", "List running applications");
        appListCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            await using var sp = CreateServices(json);
            var appSvc = sp.GetRequiredService<IApplicationService>();
            var apps = await appSvc.ListApplicationsAsync();
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, count = apps.Count, applications = apps.Select(a => new { a.Name, a.ProcessId, a.BundleIdentifier }) }, JsonOpts));
            else
            {
                Console.WriteLine($"Running applications ({apps.Count}):");
                foreach (var a in apps)
                    Console.WriteLine($"  [{a.ProcessId}] {a.Name}" + (!string.IsNullOrEmpty(a.BundleIdentifier) ? $" ({a.BundleIdentifier})" : ""));
            }
        });

        var appFindCmd = new Command("find", "Find an application");
        var appFindArg = new Argument<string>("name") { Description = "Application name or PID" };
        appFindCmd.Arguments.Add(appFindArg);
        appFindCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var name = parseResult.GetValue(appFindArg);
            if (string.IsNullOrEmpty(name))
            {
                Console.Error.WriteLine("Error: name argument is required");
                return 1;
            }
            await using var sp = CreateServices(json);
            var appSvc = sp.GetRequiredService<IApplicationService>();
            var app = await appSvc.FindApplicationAsync(name!);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, application = new { app.Name, app.ProcessId, app.BundleIdentifier } }, JsonOpts));
            else
                Console.WriteLine($"Found: {app.Name} (PID {app.ProcessId})");
            return 0;
        });

        var appLaunchCmd = new Command("launch", "Launch an application");
        var appLaunchArg = new Argument<string>("name") { Description = "Application name" };
        appLaunchCmd.Arguments.Add(appLaunchArg);
        appLaunchCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var name = parseResult.GetValue(appLaunchArg);
            if (string.IsNullOrEmpty(name))
            {
                Console.Error.WriteLine("Error: name argument is required");
                return 1;
            }
            await using var sp = CreateServices(json);
            var appSvc = sp.GetRequiredService<IApplicationService>();
            var app = await appSvc.LaunchApplicationAsync(name!);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "launch", application = new { app.Name, app.ProcessId } }, JsonOpts));
            else
                Console.WriteLine($"Launched {app.Name} (PID {app.ProcessId})");
            return 0;
        });

        var appQuitCmd = new Command("quit", "Quit an application");
        var appQuitArg = new Argument<string>("name") { Description = "Application name" };
        var appForceOpt = new Option<bool>("--force") { Description = "Force quit" };
        appQuitCmd.Arguments.Add(appQuitArg);
        appQuitCmd.Options.Add(appForceOpt);
        appQuitCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var name = parseResult.GetValue(appQuitArg);
            var force = parseResult.GetValue(appForceOpt);
            if (string.IsNullOrEmpty(name))
            {
                Console.Error.WriteLine("Error: name argument is required");
                return 1;
            }
            await using var sp = CreateServices(json);
            var appSvc = sp.GetRequiredService<IApplicationService>();
            var ok = await appSvc.QuitApplicationAsync(name!, force);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = ok, action = "quit", name, force }, JsonOpts));
            else
                Console.WriteLine(ok ? $"Quit {name}" : $"Failed to quit {name}");
            return ok ? 0 : 1;
        });

        var appActivateCmd = new Command("activate", "Activate an application");
        var appActivateArg = new Argument<string>("name") { Description = "Application name" };
        appActivateCmd.Arguments.Add(appActivateArg);
        appActivateCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var name = parseResult.GetValue(appActivateArg);
            if (string.IsNullOrEmpty(name))
            {
                Console.Error.WriteLine("Error: name argument is required");
                return 1;
            }
            await using var sp = CreateServices(json);
            var appSvc = sp.GetRequiredService<IApplicationService>();
            await appSvc.ActivateApplicationAsync(name!);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "activate", name }, JsonOpts));
            else
                Console.WriteLine($"Activated {name}");
            return 0;
        });

        var appCmd = new Command("app", "Manage applications");
        appCmd.Subcommands.Add(appListCmd);
        appCmd.Subcommands.Add(appFindCmd);
        appCmd.Subcommands.Add(appLaunchCmd);
        appCmd.Subcommands.Add(appQuitCmd);
        appCmd.Subcommands.Add(appActivateCmd);

        // ═══════════════════════════════════════════
        // WINDOW COMMANDS
        // ═══════════════════════════════════════════
        var winAppOpt = new Option<string?>("--app") { Description = "Application name" };
        var winTitleOpt = new Option<string?>("--title") { Description = "Window title" };

        WindowTarget ParseWinTarget(ParseResult pr)
        {
            var app = pr.GetValue(winAppOpt);
            var title = pr.GetValue(winTitleOpt);
            if (!string.IsNullOrEmpty(app)) return new WindowTarget.Application(app!);
            if (!string.IsNullOrEmpty(title)) return new WindowTarget.Title(title!);
            return new WindowTarget.Frontmost();
        }

        var winListCmd = new Command("list", "List windows");
        winListCmd.Options.Add(winAppOpt);
        winListCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            await using var sp = CreateServices(json);
            var winSvc = sp.GetRequiredService<IWindowManagementService>();
            var target = string.IsNullOrEmpty(parseResult.GetValue(winAppOpt)) && string.IsNullOrEmpty(parseResult.GetValue(winTitleOpt))
                ? new WindowTarget.All()
                : ParseWinTarget(parseResult);
            var windows = await winSvc.ListWindowsAsync(target);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, count = windows.Count, windows = windows.Select(w => new { w.Title, w.ProcessId, w.Bounds }) }, JsonOpts));
            else
            {
                Console.WriteLine($"Windows ({windows.Count}):");
                foreach (var w in windows)
                    Console.WriteLine($"  [{w.ProcessId}] {w.Title} ({w.Bounds.Width}x{w.Bounds.Height})");
            }
        });

        var winCloseCmd = new Command("close", "Close a window");
        winCloseCmd.Options.Add(winAppOpt);
        winCloseCmd.Options.Add(winTitleOpt);
        winCloseCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            await using var sp = CreateServices(json);
            var winSvc = sp.GetRequiredService<IWindowManagementService>();
            await winSvc.CloseWindowAsync(ParseWinTarget(parseResult));
            if (json) Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "close" }, JsonOpts));
            else Console.WriteLine("Window closed");
        });

        var winMinCmd = new Command("minimize", "Minimize a window");
        winMinCmd.Options.Add(winAppOpt);
        winMinCmd.Options.Add(winTitleOpt);
        winMinCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            await using var sp = CreateServices(json);
            var winSvc = sp.GetRequiredService<IWindowManagementService>();
            await winSvc.MinimizeWindowAsync(ParseWinTarget(parseResult));
            if (json) Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "minimize" }, JsonOpts));
            else Console.WriteLine("Window minimized");
        });

        var winMaxCmd = new Command("maximize", "Maximize a window");
        winMaxCmd.Options.Add(winAppOpt);
        winMaxCmd.Options.Add(winTitleOpt);
        winMaxCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            await using var sp = CreateServices(json);
            var winSvc = sp.GetRequiredService<IWindowManagementService>();
            await winSvc.MaximizeWindowAsync(ParseWinTarget(parseResult));
            if (json) Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "maximize" }, JsonOpts));
            else Console.WriteLine("Window maximized");
        });

        var winFocusCmd = new Command("focus", "Focus a window");
        winFocusCmd.Options.Add(winAppOpt);
        winFocusCmd.Options.Add(winTitleOpt);
        winFocusCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            await using var sp = CreateServices(json);
            var winSvc = sp.GetRequiredService<IWindowManagementService>();
            await winSvc.FocusWindowAsync(ParseWinTarget(parseResult));
            if (json) Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "focus" }, JsonOpts));
            else Console.WriteLine("Window focused");
        });

        var winMoveCmd = new Command("move", "Move a window");
        var moveX = new Option<int>("--x") { Description = "X coordinate" };
        var moveY = new Option<int>("--y") { Description = "Y coordinate" };
        winMoveCmd.Options.Add(winAppOpt);
        winMoveCmd.Options.Add(winTitleOpt);
        winMoveCmd.Options.Add(moveX);
        winMoveCmd.Options.Add(moveY);
        winMoveCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var x = parseResult.GetValue(moveX);
            var y = parseResult.GetValue(moveY);
            await using var sp = CreateServices(json);
            var winSvc = sp.GetRequiredService<IWindowManagementService>();
            await winSvc.MoveWindowAsync(ParseWinTarget(parseResult), new Point(x, y));
            if (json) Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "move", x, y }, JsonOpts));
            else Console.WriteLine($"Window moved to ({x},{y})");
        });

        var winResizeCmd = new Command("resize", "Resize a window");
        var resizeW = new Option<int>("--width") { Description = "Width" };
        var resizeH = new Option<int>("--height") { Description = "Height" };
        winResizeCmd.Options.Add(winAppOpt);
        winResizeCmd.Options.Add(winTitleOpt);
        winResizeCmd.Options.Add(resizeW);
        winResizeCmd.Options.Add(resizeH);
        winResizeCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var w = parseResult.GetValue(resizeW);
            var h = parseResult.GetValue(resizeH);
            await using var sp = CreateServices(json);
            var winSvc = sp.GetRequiredService<IWindowManagementService>();
            await winSvc.ResizeWindowAsync(ParseWinTarget(parseResult), new Size(w, h));
            if (json) Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "resize", width = w, height = h }, JsonOpts));
            else Console.WriteLine($"Window resized to {w}x{h}");
        });

        var winCmd = new Command("window", "Manage windows");
        winCmd.Subcommands.Add(winListCmd);
        winCmd.Subcommands.Add(winCloseCmd);
        winCmd.Subcommands.Add(winMinCmd);
        winCmd.Subcommands.Add(winMaxCmd);
        winCmd.Subcommands.Add(winFocusCmd);
        winCmd.Subcommands.Add(winMoveCmd);
        winCmd.Subcommands.Add(winResizeCmd);

        // ═══════════════════════════════════════════
        // CLIPBOARD COMMANDS
        // ═══════════════════════════════════════════
        var clipGetCmd = new Command("get", "Get clipboard text");
        clipGetCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            await using var sp = CreateServices(json);
            var clip = sp.GetRequiredService<IClipboardService>();
            var text = await clip.GetTextAsync();
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "get", text }, JsonOpts));
            else
                Console.WriteLine(string.IsNullOrEmpty(text) ? "Clipboard is empty" : text);
        });

        var clipSetCmd = new Command("set", "Set clipboard text");
        var clipTextOpt = new Option<string>("--text") { Description = "Text to set" };
        clipSetCmd.Options.Add(clipTextOpt);
        clipSetCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var text = parseResult.GetValue(clipTextOpt);
            if (string.IsNullOrEmpty(text))
            {
                Console.Error.WriteLine("Error: --text is required");
                return 1;
            }
            await using var sp = CreateServices(json);
            var clip = sp.GetRequiredService<IClipboardService>();
            await clip.SetTextAsync(text!);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "set", length = text!.Length }, JsonOpts));
            else
                Console.WriteLine($"Set clipboard ({text!.Length} chars)");
            return 0;
        });

        var clipClearCmd = new Command("clear", "Clear clipboard");
        clipClearCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            await using var sp = CreateServices(json);
            var clip = sp.GetRequiredService<IClipboardService>();
            await clip.SetTextAsync("");
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "clear" }, JsonOpts));
            else
                Console.WriteLine("Clipboard cleared");
        });

        var clipCmd = new Command("clipboard", "Manage clipboard");
        clipCmd.Subcommands.Add(clipGetCmd);
        clipCmd.Subcommands.Add(clipSetCmd);
        clipCmd.Subcommands.Add(clipClearCmd);

        // ═══════════════════════════════════════════
        // PERMISSIONS COMMAND
        // ═══════════════════════════════════════════
        var permCmd = new Command("permissions", "Check automation permissions");
        permCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            await using var sp = CreateServices(json);
            var perm = sp.GetRequiredService<IPermissionsService>();
            var status = await perm.CheckAllAsync();
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, isAdmin = status.IsAdministrator, hasUiAccess = status.HasUiAccess, fullyCapable = status.IsFullyCapable, warnings = status.Warnings }, JsonOpts));
            else
            {
                Console.WriteLine("Permissions:");
                Console.WriteLine($"  Administrator: {(status.IsAdministrator ? "Yes" : "No")}");
                Console.WriteLine($"  UIAccess: {(status.HasUiAccess ? "Yes" : "No")}");
                Console.WriteLine($"  Fully capable: {(status.IsFullyCapable ? "Yes" : "No")}");
                if (status.Warnings.Count > 0)
                {
                    Console.WriteLine("  Warnings:");
                    foreach (var w in status.Warnings)
                        Console.WriteLine($"    - {w}");
                }
            }
            return status.IsFullyCapable ? 0 : 1;
        });

        // ═══════════════════════════════════════════
        // CLEAN COMMAND
        // ═══════════════════════════════════════════
        var cleanCmd = new Command("clean", "Clean up snapshot cache and temporary files");
        var cleanAllOpt = new Option<bool>("--all-snapshots") { Description = "Remove all snapshot data" };
        var cleanOlderOpt = new Option<int?>("--older-than") { Description = "Remove snapshots older than N hours" };
        var cleanSnapOpt = new Option<string?>("--snapshot") { Description = "Remove specific snapshot by ID" };
        var cleanDryOpt = new Option<bool>("--dry-run") { Description = "Preview what would be deleted" };
        cleanCmd.Options.Add(cleanAllOpt);
        cleanCmd.Options.Add(cleanOlderOpt);
        cleanCmd.Options.Add(cleanSnapOpt);
        cleanCmd.Options.Add(cleanDryOpt);
        cleanCmd.SetAction(parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var all = parseResult.GetValue(cleanAllOpt);
            var older = parseResult.GetValue(cleanOlderOpt);
            var snap = parseResult.GetValue(cleanSnapOpt);
            var dry = parseResult.GetValue(cleanDryOpt);
            var optCount = (all ? 1 : 0) + (older.HasValue ? 1 : 0) + (!string.IsNullOrEmpty(snap) ? 1 : 0);
            if (optCount != 1)
            {
                Console.Error.WriteLine("Error: specify exactly one of --all-snapshots, --older-than, or --snapshot");
                return 1;
            }
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "clean", allSnapshots = all, olderThan = older, snapshot = snap, dryRun = dry }, JsonOpts));
            else
            {
                var prefix = dry ? "Would clean" : "Cleaned";
                if (all) Console.WriteLine($"{prefix} all snapshots");
                else if (older.HasValue) Console.WriteLine($"{prefix} snapshots older than {older}h");
                else if (!string.IsNullOrEmpty(snap)) Console.WriteLine($"{prefix} snapshot '{snap}'");
            }
            return 0;
        });

        // ═══════════════════════════════════════════
        // MENU COMMANDS
        // ═══════════════════════════════════════════
        var menuListCmd = new Command("list", "List menu items");
        var menuAppOpt = new Option<string?>("--app") { Description = "Application name (default: frontmost)" };
        var menuSysOpt = new Option<bool>("--system") { Description = "Use system menu instead of app menu" };
        menuListCmd.Options.Add(menuAppOpt);
        menuListCmd.Options.Add(menuSysOpt);
        menuListCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var app = parseResult.GetValue(menuAppOpt);
            var useSys = parseResult.GetValue(menuSysOpt);
            await using var sp = CreateServices(json);
            var menuSvc = sp.GetRequiredService<IMenuDiscoveryService>();
            var target = new MenuTarget(app, useSys);
            var items = await menuSvc.ListMenuItemsAsync(target);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, count = items.Count, items = items.Select(i => new { i.Id, i.Label, i.Shortcut, i.IsEnabled, i.IsChecked, i.HasSubmenu, i.Position }) }, JsonOpts));
            else
            {
                Console.WriteLine($"Menu items ({items.Count}):");
                foreach (var i in items)
                    Console.WriteLine($"  [{i.Position}] {i.Label}" + (i.HasSubmenu ? " >" : "") + (i.IsEnabled ? "" : " (disabled)"));
            }
        });

        var menuClickCmd = new Command("click", "Click a menu item");
        var menuClickAppOpt = new Option<string?>("--app") { Description = "Application name" };
        var menuClickSysOpt = new Option<bool>("--system") { Description = "Use system menu" };
        var menuClickPathArg = new Argument<string>("path") { Description = "Menu path (e.g., File > Save As)" };
        menuClickCmd.Options.Add(menuClickAppOpt);
        menuClickCmd.Options.Add(menuClickSysOpt);
        menuClickCmd.Arguments.Add(menuClickPathArg);
        menuClickCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var app = parseResult.GetValue(menuClickAppOpt);
            var useSys = parseResult.GetValue(menuClickSysOpt);
            var path = parseResult.GetValue(menuClickPathArg);
            if (string.IsNullOrEmpty(path))
            {
                Console.Error.WriteLine("Error: menu path is required");
                return 1;
            }
            await using var sp = CreateServices(json);
            var menuSvc = sp.GetRequiredService<IMenuDiscoveryService>();
            var target = new MenuTarget(app, useSys);
            await menuSvc.ClickMenuItemAsync(target, path!);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "click", path }, JsonOpts));
            else
                Console.WriteLine($"Clicked menu: {path}");
            return 0;
        });

        var menuHasCmd = new Command("has", "Check if app has menu bar");
        var menuHasAppArg = new Argument<string>("app") { Description = "Application name" };
        menuHasCmd.Arguments.Add(menuHasAppArg);
        menuHasCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var app = parseResult.GetValue(menuHasAppArg);
            if (string.IsNullOrEmpty(app))
            {
                Console.Error.WriteLine("Error: app argument is required");
                return 1;
            }
            await using var sp = CreateServices(json);
            var menuSvc = sp.GetRequiredService<IMenuDiscoveryService>();
            var has = await menuSvc.HasMenuBarAsync(app!);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, app, hasMenuBar = has }, JsonOpts));
            else
                Console.WriteLine(has ? $"{app} has a menu bar" : $"{app} has no menu bar");
            return 0;
        });

        var menuCmd = new Command("menu", "Discover and interact with menu bars");
        menuCmd.Subcommands.Add(menuListCmd);
        menuCmd.Subcommands.Add(menuClickCmd);
        menuCmd.Subcommands.Add(menuHasCmd);

        // ═══════════════════════════════════════════
        // TASKBAR COMMANDS
        // ═══════════════════════════════════════════
        var taskbarListCmd = new Command("list", "List taskbar items");
        taskbarListCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            await using var sp = CreateServices(json);
            var taskbarSvc = sp.GetRequiredService<ITaskbarService>();
            var items = await taskbarSvc.ListTaskbarItemsAsync();
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, count = items.Count, items = items.Select(i => new { i.Id, i.Name, i.ProcessName, i.ProcessId, i.IsActive, i.IsPinned }) }, JsonOpts));
            else
            {
                Console.WriteLine($"Taskbar items ({items.Count}):");
                foreach (var i in items)
                    Console.WriteLine($"  [{i.Id}] {i.Name} (PID {i.ProcessId})" + (i.IsPinned ? " pinned" : "") + (i.IsActive ? " active" : ""));
            }
        });

        var taskbarClickCmd = new Command("click", "Click a taskbar item");
        var taskbarClickIdArg = new Argument<string>("id") { Description = "Taskbar item ID or name" };
        taskbarClickCmd.Arguments.Add(taskbarClickIdArg);
        taskbarClickCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var id = parseResult.GetValue(taskbarClickIdArg);
            if (string.IsNullOrEmpty(id))
            {
                Console.Error.WriteLine("Error: item id/name is required");
                return 1;
            }
            await using var sp = CreateServices(json);
            var taskbarSvc = sp.GetRequiredService<ITaskbarService>();
            await taskbarSvc.ClickTaskbarItemAsync(id!);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "click", itemId = id }, JsonOpts));
            else
                Console.WriteLine($"Clicked taskbar item: {id}");
            return 0;
        });

        var taskbarHideCmd = new Command("hide", "Hide a taskbar item");
        var taskbarHideArg = new Argument<string>("id") { Description = "Taskbar item ID" };
        taskbarHideCmd.Arguments.Add(taskbarHideArg);
        taskbarHideCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var id = parseResult.GetValue(taskbarHideArg);
            if (string.IsNullOrEmpty(id))
            {
                Console.Error.WriteLine("Error: item id is required");
                return 1;
            }
            await using var sp = CreateServices(json);
            var taskbarSvc = sp.GetRequiredService<ITaskbarService>();
            await taskbarSvc.HideTaskbarItemAsync(id!);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "hide", itemId = id }, JsonOpts));
            else
                Console.WriteLine($"Hidden taskbar item: {id}");
            return 0;
        });

        var taskbarShowCmd = new Command("show", "Show a hidden taskbar item");
        var taskbarShowArg = new Argument<string>("id") { Description = "Taskbar item ID" };
        taskbarShowCmd.Arguments.Add(taskbarShowArg);
        taskbarShowCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var id = parseResult.GetValue(taskbarShowArg);
            if (string.IsNullOrEmpty(id))
            {
                Console.Error.WriteLine("Error: item id is required");
                return 1;
            }
            await using var sp = CreateServices(json);
            var taskbarSvc = sp.GetRequiredService<ITaskbarService>();
            await taskbarSvc.ShowTaskbarItemAsync(id!);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "show", itemId = id }, JsonOpts));
            else
                Console.WriteLine($"Showed taskbar item: {id}");
            return 0;
        });

        var taskbarCmd = new Command("taskbar", "Interact with Windows taskbar");
        taskbarCmd.Subcommands.Add(taskbarListCmd);
        taskbarCmd.Subcommands.Add(taskbarClickCmd);
        taskbarCmd.Subcommands.Add(taskbarHideCmd);
        taskbarCmd.Subcommands.Add(taskbarShowCmd);

        // ═══════════════════════════════════════════
        // DIALOG COMMANDS
        // ═══════════════════════════════════════════
        var dialogWaitCmd = new Command("wait", "Wait for a dialog to appear");
        var dialogTimeoutOpt = new Option<int>("--timeout") { Description = "Timeout in ms", DefaultValueFactory = _ => 5000 };
        dialogWaitCmd.Options.Add(dialogTimeoutOpt);
        dialogWaitCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var timeout = parseResult.GetValue(dialogTimeoutOpt);
            await using var sp = CreateServices(json);
            var dialogSvc = sp.GetRequiredService<IDialogService>();
            var found = await dialogSvc.WaitForDialogAsync(timeout);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "wait", dialogFound = found, timeoutMs = timeout }, JsonOpts));
            else
                Console.WriteLine(found ? "Dialog appeared" : "No dialog within timeout");
            return found ? 0 : 1;
        });

        var dialogSetPathCmd = new Command("set-path", "Set file path in dialog");
        var dialogPathArg = new Argument<string>("path") { Description = "File path" };
        dialogSetPathCmd.Arguments.Add(dialogPathArg);
        dialogSetPathCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var path = parseResult.GetValue(dialogPathArg);
            if (string.IsNullOrEmpty(path))
            {
                Console.Error.WriteLine("Error: path is required");
                return 1;
            }
            await using var sp = CreateServices(json);
            var dialogSvc = sp.GetRequiredService<IDialogService>();
            await dialogSvc.SetPathAsync(path!);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "set-path", path }, JsonOpts));
            else
                Console.WriteLine($"Set dialog path: {path}");
            return 0;
        });

        var dialogSetFilterCmd = new Command("set-filter", "Set file type filter");
        var dialogFilterArg = new Argument<string>("filter") { Description = "Filter string (e.g., Text Files|*.txt|All Files|*.*)" };
        dialogSetFilterCmd.Arguments.Add(dialogFilterArg);
        dialogSetFilterCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var filter = parseResult.GetValue(dialogFilterArg);
            if (string.IsNullOrEmpty(filter))
            {
                Console.Error.WriteLine("Error: filter is required");
                return 1;
            }
            await using var sp = CreateServices(json);
            var dialogSvc = sp.GetRequiredService<IDialogService>();
            await dialogSvc.SetFilterAsync(filter!);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "set-filter", filter }, JsonOpts));
            else
                Console.WriteLine($"Set dialog filter: {filter}");
            return 0;
        });

        var dialogClickCmd = new Command("click", "Click dialog button");
        var dialogButtonOpt = new Option<DialogButton>("--button") { Description = "Button to click" };
        dialogClickCmd.Options.Add(dialogButtonOpt);
        dialogClickCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var button = parseResult.GetValue(dialogButtonOpt);
            await using var sp = CreateServices(json);
            var dialogSvc = sp.GetRequiredService<IDialogService>();
            await dialogSvc.ClickButtonAsync(button);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "click", button = button.ToString() }, JsonOpts));
            else
                Console.WriteLine($"Clicked dialog button: {button}");
            return 0;
        });

        var dialogCmd = new Command("dialog", "Drive file open/save dialogs");
        dialogCmd.Subcommands.Add(dialogWaitCmd);
        dialogCmd.Subcommands.Add(dialogSetPathCmd);
        dialogCmd.Subcommands.Add(dialogSetFilterCmd);
        dialogCmd.Subcommands.Add(dialogClickCmd);

        // ═══════════════════════════════════════════
        // SPACE (VIRTUAL DESKTOP) COMMANDS
        // ═══════════════════════════════════════════
        var spaceListCmd = new Command("list", "List virtual desktops");
        spaceListCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            await using var sp = CreateServices(json);
            var spaceSvc = sp.GetRequiredService<IVirtualDesktopService>();
            var desktops = await spaceSvc.ListDesktopsAsync();
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, count = desktops.Count, desktops = desktops.Select(d => new { d.Id, d.Name, d.Index, d.IsCurrent }) }, JsonOpts));
            else
            {
                Console.WriteLine($"Virtual desktops ({desktops.Count}):");
                foreach (var d in desktops)
                    Console.WriteLine($"  [{d.Index}] {d.Name}" + (d.IsCurrent ? " (current)" : ""));
            }
        });

        var spaceSwitchCmd = new Command("switch", "Switch to a virtual desktop");
        var spaceSwitchArg = new Argument<string>("id") { Description = "Desktop ID" };
        spaceSwitchCmd.Arguments.Add(spaceSwitchArg);
        spaceSwitchCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var id = parseResult.GetValue(spaceSwitchArg);
            if (string.IsNullOrEmpty(id))
            {
                Console.Error.WriteLine("Error: desktop id is required");
                return 1;
            }
            await using var sp = CreateServices(json);
            var spaceSvc = sp.GetRequiredService<IVirtualDesktopService>();
            await spaceSvc.SwitchToDesktopAsync(id!);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "switch", desktopId = id }, JsonOpts));
            else
                Console.WriteLine($"Switched to desktop: {id}");
            return 0;
        });

        var spaceCreateCmd = new Command("create", "Create a new virtual desktop");
        spaceCreateCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            await using var sp = CreateServices(json);
            var spaceSvc = sp.GetRequiredService<IVirtualDesktopService>();
            var id = await spaceSvc.CreateDesktopAsync();
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "create", desktopId = id }, JsonOpts));
            else
                Console.WriteLine($"Created desktop: {id}");
            return 0;
        });

        var spaceDeleteCmd = new Command("delete", "Delete a virtual desktop");
        var spaceDeleteArg = new Argument<string>("id") { Description = "Desktop ID" };
        spaceDeleteCmd.Arguments.Add(spaceDeleteArg);
        spaceDeleteCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var id = parseResult.GetValue(spaceDeleteArg);
            if (string.IsNullOrEmpty(id))
            {
                Console.Error.WriteLine("Error: desktop id is required");
                return 1;
            }
            await using var sp = CreateServices(json);
            var spaceSvc = sp.GetRequiredService<IVirtualDesktopService>();
            await spaceSvc.DeleteDesktopAsync(id!);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "delete", desktopId = id }, JsonOpts));
            else
                Console.WriteLine($"Deleted desktop: {id}");
            return 0;
        });

        var spaceMoveCmd = new Command("move", "Move window to a virtual desktop");
        var spaceMoveWindowArg = new Argument<string>("window") { Description = "Window title" };
        var spaceMoveDesktopArg = new Argument<string>("desktop") { Description = "Desktop ID" };
        spaceMoveCmd.Arguments.Add(spaceMoveWindowArg);
        spaceMoveCmd.Arguments.Add(spaceMoveDesktopArg);
        spaceMoveCmd.SetAction(async parseResult =>
        {
            var json = parseResult.GetValue(jsonOption);
            var window = parseResult.GetValue(spaceMoveWindowArg);
            var desktop = parseResult.GetValue(spaceMoveDesktopArg);
            if (string.IsNullOrEmpty(window) || string.IsNullOrEmpty(desktop))
            {
                Console.Error.WriteLine("Error: window and desktop arguments are required");
                return 1;
            }
            await using var sp = CreateServices(json);
            var spaceSvc = sp.GetRequiredService<IVirtualDesktopService>();
            await spaceSvc.MoveWindowToDesktopAsync(window!, desktop!);
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, action = "move", window, desktopId = desktop }, JsonOpts));
            else
                Console.WriteLine($"Moved window '{window}' to desktop: {desktop}");
            return 0;
        });

        var spaceCmd = new Command("space", "Manage virtual desktops (Windows Spaces)");
        spaceCmd.Subcommands.Add(spaceListCmd);
        spaceCmd.Subcommands.Add(spaceSwitchCmd);
        spaceCmd.Subcommands.Add(spaceCreateCmd);
        spaceCmd.Subcommands.Add(spaceDeleteCmd);
        spaceCmd.Subcommands.Add(spaceMoveCmd);

        // ═══════════════════════════════════════════
        // ROOT COMMAND
        // ═══════════════════════════════════════════
        var rootCommand = new RootCommand("Peekaboo for Windows - desktop automation CLI");
        rootCommand.Options.Add(jsonOption);
        rootCommand.Subcommands.Add(captureCmd);
        rootCommand.Subcommands.Add(seeCmd);
        rootCommand.Subcommands.Add(clickCmd);
        rootCommand.Subcommands.Add(typeCmd);
        rootCommand.Subcommands.Add(hotkeyCmd);
        rootCommand.Subcommands.Add(scrollCmd);
        rootCommand.Subcommands.Add(dragCmd);
        rootCommand.Subcommands.Add(appCmd);
        rootCommand.Subcommands.Add(winCmd);
        rootCommand.Subcommands.Add(clipCmd);
        rootCommand.Subcommands.Add(permCmd);
        rootCommand.Subcommands.Add(cleanCmd);
        rootCommand.Subcommands.Add(menuCmd);
        rootCommand.Subcommands.Add(taskbarCmd);
        rootCommand.Subcommands.Add(dialogCmd);
        rootCommand.Subcommands.Add(spaceCmd);

        // Default action when invoked without arguments
        rootCommand.SetAction(parseResult =>
        {
            Console.WriteLine(rootCommand.Description);
            Console.WriteLine();
            Console.WriteLine("Available commands:");
            Console.WriteLine("  capture    - Capture screenshots");
            Console.WriteLine("  see        - Detect UI elements");
            Console.WriteLine("  click      - Click at location");
            Console.WriteLine("  type       - Type text");
            Console.WriteLine("  hotkey     - Press hotkey");
            Console.WriteLine("  scroll     - Scroll");
            Console.WriteLine("  drag       - Drag between points");
            Console.WriteLine("  app        - Manage applications");
            Console.WriteLine("  window     - Manage windows");
            Console.WriteLine("  clipboard  - Manage clipboard");
            Console.WriteLine("  permissions - Check automation permissions");
            Console.WriteLine("  clean      - Clean up snapshots");
            Console.WriteLine("  menu       - Discover and interact with menu bars");
            Console.WriteLine("  taskbar    - Interact with Windows taskbar");
            Console.WriteLine("  dialog     - Drive file open/save dialogs");
            Console.WriteLine("  space      - Manage virtual desktops (Windows Spaces)");
            Console.WriteLine();
            Console.WriteLine("Use --help with any command for more details.");
            return 0;
        });

        return rootCommand.Parse(args).Invoke();
    }

    private static ServiceProvider CreateServices(bool json)
    {
        var minLevel = json ? LogLevel.Warning : LogLevel.Information;
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(minLevel));

        return new ServiceCollection()
            .AddSingleton<ILoggerFactory>(loggerFactory)
            .AddSingleton(typeof(ILogger<>), typeof(Logger<>))
            .AddSingleton<IScreenCaptureService, WindowsScreenCaptureService>()
            .AddSingleton<IElementDetectionService, WindowsElementDetectionService>()
            .AddSingleton<IInputService, WindowsInputService>()
            .AddSingleton<IApplicationService, WindowsApplicationService>()
            .AddSingleton<IWindowManagementService, WindowsWindowManagementService>()
            .AddSingleton<IClipboardService, WindowsClipboardService>()
            .AddSingleton<IPermissionsService, WindowsPermissionsService>()
            .AddSingleton<IMenuDiscoveryService, WindowsMenuDiscoveryService>()
            .AddSingleton<ITaskbarService, WindowsTaskbarService>()
            .AddSingleton<IDialogService, WindowsDialogService>()
            .AddSingleton<IVirtualDesktopService, WindowsVirtualDesktopService>()
            .BuildServiceProvider();
    }
}
