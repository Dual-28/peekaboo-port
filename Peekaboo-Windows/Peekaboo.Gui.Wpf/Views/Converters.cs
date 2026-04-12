using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Peekaboo.Gui.Wpf.ViewModels;

namespace Peekaboo.Gui.Wpf.Views;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}

public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class BoolToStatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? "..." : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class BoolToPermissionColorConverter : IValueConverter
{
    private static readonly Brush SuccessBrush = new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0));
    private static readonly Brush WarningBrush = new SolidColorBrush(Color.FromRgb(0xD4, 0xA7, 0x2C));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? SuccessBrush : WarningBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class ToolNameToIconConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var toolName = values.FirstOrDefault()?.ToString()?.ToLowerInvariant() ?? string.Empty;

        if (toolName.Contains("capture") || toolName.Contains("screen")) return "[]";
        if (toolName.Contains("click") || toolName.Contains("mouse")) return ">";
        if (toolName.Contains("type") || toolName.Contains("key")) return "T";
        if (toolName.Contains("app")) return "A";
        if (toolName.Contains("window")) return "W";
        if (toolName.Contains("clipboard")) return "C";
        if (toolName.Contains("permission")) return "!";
        return "*";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        targetTypes.Select(_ => Binding.DoNothing).ToArray();
}

public sealed class MessageTemplateSelector : DataTemplateSelector
{
    public DataTemplate? UserTemplate { get; set; }
    public DataTemplate? AssistantTemplate { get; set; }
    public DataTemplate? SystemTemplate { get; set; }
    public DataTemplate? ToolTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not ChatMessageEntry message)
            return base.SelectTemplate(item, container);

        return message.Role.ToLowerInvariant() switch
        {
            "user" => UserTemplate,
            "assistant" => AssistantTemplate,
            "system" => SystemTemplate,
            "tool" => ToolTemplate,
            _ => SystemTemplate ?? base.SelectTemplate(item, container),
        };
    }
}
