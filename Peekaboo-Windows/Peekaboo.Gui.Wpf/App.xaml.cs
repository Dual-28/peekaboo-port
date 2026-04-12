using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Peekaboo.Core;
using Peekaboo.Gui.Wpf.Ai;
using Peekaboo.Gui.Wpf.Sessions;
using Peekaboo.Gui.Wpf.ViewModels;
using Peekaboo.Gui.Wpf.Views;
using Peekaboo.Platform.Windows.Services;

namespace Peekaboo.Gui.Wpf;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _services = ConfigureServices();
        var mainWindow = _services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddConsole().SetMinimumLevel(LogLevel.Warning);
        });

        services.AddSingleton<IScreenCaptureService, WindowsScreenCaptureService>();
        services.AddSingleton<IElementDetectionService, WindowsElementDetectionService>();
        services.AddSingleton<IInputService, WindowsInputService>();
        services.AddSingleton<IApplicationService, WindowsApplicationService>();
        services.AddSingleton<IWindowManagementService, WindowsWindowManagementService>();
        services.AddSingleton<IClipboardService, WindowsClipboardService>();
        services.AddSingleton<IPermissionsService, WindowsPermissionsService>();
        services.AddSingleton<IMenuDiscoveryService, WindowsMenuDiscoveryService>();
        services.AddSingleton<ITaskbarService, WindowsTaskbarService>();
        services.AddSingleton<IDialogService, WindowsDialogService>();
        services.AddSingleton<IVirtualDesktopService, WindowsVirtualDesktopService>();

        services.AddSingleton(AiSettings.Load());
        services.AddSingleton<SessionStore>();
        services.AddSingleton<IAgentService, PeekabooAgentService>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }
}
