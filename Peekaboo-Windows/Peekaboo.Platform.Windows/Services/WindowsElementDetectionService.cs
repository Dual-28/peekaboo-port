using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Microsoft.Extensions.Logging;
using Peekaboo.Core;

namespace Peekaboo.Platform.Windows.Services;

/// <summary>
/// Windows element detection using FlaUI (UI Automation API).
/// Correlates the UIA tree with screen positions to produce DetectedElement instances.
/// </summary>
public sealed class WindowsElementDetectionService : IElementDetectionService
{
    private readonly ILogger<WindowsElementDetectionService> _logger;
    private readonly UIA3Automation _automation;
    private int _elementCounter;
    private readonly object _counterLock = new();

    public WindowsElementDetectionService(ILogger<WindowsElementDetectionService> logger)
    {
        _logger = logger;
        _automation = new UIA3Automation();
    }

    public Task<ElementDetectionResult> DetectElementsAsync(
        byte[] imageData,
        WindowContext? windowContext = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        _elementCounter = 0;

        // Walk the UIA tree from the desktop root (or scoped to a specific window)
        AutomationElement root;
        if (windowContext?.ApplicationProcessId != null)
        {
            root = GetElementFromProcessId(windowContext.ApplicationProcessId.Value);
        }
        else if (windowContext?.ApplicationName != null)
        {
            var proc = System.Diagnostics.Process.GetProcesses()
                .FirstOrDefault(p => string.Equals(p.ProcessName, windowContext.ApplicationName, StringComparison.OrdinalIgnoreCase));
            root = proc != null ? GetElementFromProcessId(proc.Id) : _automation.GetDesktop();
        }
        else
        {
            root = _automation.GetDesktop();
        }

        var allElements = new List<DetectedElement>();

        try
        {
            WalkTree(root, allElements, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error walking UIA tree");
        }

        sw.Stop();

        var grouped = GroupElements(allElements);
        var snapshotId = $"snap_{DateTimeOffset.Now:yyyyMMdd_HHmmss}_{_elementCounter}";

        var result = new ElementDetectionResult(
            SnapshotId: snapshotId,
            ScreenshotPath: null,
            Elements: grouped,
            Metadata: new DetectionMetadata(
                DetectionTime: sw.Elapsed,
                ElementCount: grouped.All.Count,
                Method: "uia3-tree-walk",
                WindowContext: windowContext
            )
        );

        _logger.LogInformation("Element detection complete: {Count} elements in {Elapsed}ms",
            grouped.All.Count, sw.ElapsedMilliseconds);

        return Task.FromResult(result);
    }

    public Task<DetectedElement> FindElementAsync(
        string label,
        ElementType? type = null,
        string? appName = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        AutomationElement root = appName != null
            ? FindAppRoot(appName)
            : _automation.GetDesktop();

        var condition = _automation.ConditionFactory.ByName(label);
        var elements = root.FindAll(FlaUI.Core.Definitions.TreeScope.Descendants, condition);

        foreach (var el in elements)
        {
            var mapped = MapElementType(el.ControlType);
            if (type == null || mapped == type)
            {
                return Task.FromResult(CreateDetectedElement(el));
            }
        }

        throw new ElementNotFoundException($"Element '{label}' not found{(appName != null ? $" in {appName}" : "")}");
    }

    private void WalkTree(AutomationElement parent, List<DetectedElement> elements, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        AutomationElement[] children;
        try
        {
            children = parent.FindAllChildren();
        }
        catch
        {
            return; // Element no longer accessible
        }

        foreach (var child in children)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // Only include visible, named, or interactive elements
                if (ShouldIncludeElement(child))
                {
                    elements.Add(CreateDetectedElement(child));
                }

                // Recurse into children
                WalkTree(child, elements, ct);
            }
            catch
            {
                // Element became inaccessible during walk — skip
            }
        }
    }

    private bool ShouldIncludeElement(AutomationElement el)
    {
        // Must be visible and have a meaningful control type
        if (!el.IsOffscreen)
        {
            var ct = el.ControlType;
            return ct == ControlType.Button ||
                   ct == ControlType.Edit ||
                   ct == ControlType.CheckBox ||
                   ct == ControlType.RadioButton ||
                   ct == ControlType.ComboBox ||
                   ct == ControlType.ListItem ||
                   ct == ControlType.MenuItem ||
                   ct == ControlType.Hyperlink ||
                   ct == ControlType.Slider ||
                   ct == ControlType.TabItem ||
                   ct == ControlType.TreeItem ||
                   ct == ControlType.Text ||
                   ct == ControlType.Document ||
                   ct == ControlType.Image ||
                   ct == ControlType.Group ||
                   ct == ControlType.Window ||
                   ct == ControlType.Pane;
        }
        return false;
    }

    private DetectedElement CreateDetectedElement(AutomationElement el)
    {
        var id = NextId(el.ControlType);
        var label = el.Name;
        var value = "";
        var bounds = el.BoundingRectangle;

        try { value = el.Patterns.Value.Pattern.Value.Value ?? ""; } catch { }

        var attrs = new Dictionary<string, string>();
            try
            {
                attrs["control_type"] = el.ControlType.ToString();
                if (!string.IsNullOrEmpty(el.AutomationId))
                    attrs["automation_id"] = el.AutomationId;
            }
            catch { }

        return new DetectedElement(
            Id: id,
            Type: MapElementType(el.ControlType),
            Label: string.IsNullOrEmpty(label) ? null : label,
            Value: string.IsNullOrEmpty(value) ? null : value,
            Bounds: new Core.Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            IsEnabled: el.IsEnabled,
            IsSelected: null,
            Attributes: attrs
        );
    }

    private static string NextId(ControlType ct)
    {
        var prefix = ct switch
        {
            ControlType.Button => "B",
            ControlType.Edit => "T",
            ControlType.Hyperlink => "L",
            ControlType.Image => "I",
            ControlType.Group or ControlType.Pane => "G",
            ControlType.Slider => "S",
            ControlType.CheckBox => "C",
            ControlType.MenuItem => "M",
            _ => "O"
        };

        // Thread-safe counter
        int num;
        lock (s_counterLock)
        {
            var current = s_counters.GetValueOrDefault(prefix, 0) + 1;
            s_counters[prefix] = current;
            num = current;
        }
        return $"{prefix}{num}";
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> s_counters = new();
    private static readonly object s_counterLock = new();

    private static DetectedElements GroupElements(IReadOnlyList<DetectedElement> elements)
    {
        return new DetectedElements(
            Buttons: elements.Where(e => e.Type == ElementType.Button).ToList(),
            TextFields: elements.Where(e => e.Type == ElementType.TextField).ToList(),
            Links: elements.Where(e => e.Type == ElementType.Link).ToList(),
            Images: elements.Where(e => e.Type == ElementType.Image).ToList(),
            Groups: elements.Where(e => e.Type == ElementType.Group).ToList(),
            Sliders: elements.Where(e => e.Type == ElementType.Slider).ToList(),
            Checkboxes: elements.Where(e => e.Type == ElementType.Checkbox).ToList(),
            Menus: elements.Where(e => e.Type == ElementType.Menu).ToList(),
            Other: elements.Where(e => e.Type == ElementType.Other).ToList()
        );
    }

    private static ElementType MapElementType(ControlType ct) => ct switch
    {
        ControlType.Button => ElementType.Button,
        ControlType.Edit or ControlType.Document => ElementType.TextField,
        ControlType.Hyperlink => ElementType.Link,
        ControlType.Image => ElementType.Image,
        ControlType.Group or ControlType.Pane => ElementType.Group,
        ControlType.Slider => ElementType.Slider,
        ControlType.CheckBox => ElementType.Checkbox,
        ControlType.MenuItem or ControlType.MenuBar => ElementType.Menu,
        _ => ElementType.Other
    };

    private AutomationElement FindAppRoot(string appName)
    {
        var proc = System.Diagnostics.Process.GetProcesses()
            .FirstOrDefault(p => string.Equals(p.ProcessName, appName, StringComparison.OrdinalIgnoreCase));
        
        if (proc != null && proc.MainWindowHandle != nint.Zero)
        {
            try
            {
                return _automation.FromHandle(proc.MainWindowHandle);
            }
            catch { }
        }
        return _automation.GetDesktop();
    }

    private AutomationElement GetElementFromProcessId(int pid)
    {
        // FlaUI doesn't have FromProcessId — find the main window handle for the process
        try
        {
            var proc = System.Diagnostics.Process.GetProcessById(pid);
            if (proc.MainWindowHandle != nint.Zero)
                return _automation.FromHandle(proc.MainWindowHandle);
        }
        catch { }
        return _automation.GetDesktop();
    }

    public void Dispose()
    {
        _automation.Dispose();
    }
}
