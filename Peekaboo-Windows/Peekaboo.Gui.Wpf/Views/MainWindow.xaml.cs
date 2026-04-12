using System.Windows;
using Peekaboo.Gui.Wpf.ViewModels;

namespace Peekaboo.Gui.Wpf.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
