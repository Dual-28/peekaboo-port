using System.Windows;
using Peekaboo.Gui.Wpf.Ai;
using Peekaboo.Gui.Wpf.ViewModels;

namespace Peekaboo.Gui.Wpf.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(AiSettings settings)
    {
        InitializeComponent();
        DataContext = new SettingsViewModel(settings);
    }

    private void SaveAndClose_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
