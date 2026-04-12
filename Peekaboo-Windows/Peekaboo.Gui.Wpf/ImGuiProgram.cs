using System;
using System.IO;
using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Peekaboo.Core;
using Peekaboo.Gui.Wpf.Ai;
using Peekaboo.Gui.Wpf.Sessions;
using Peekaboo.Gui.Wpf.Rendering;
using Peekaboo.Platform.Windows.Gui;
using Peekaboo.Platform.Windows.Services;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Peekaboo.Gui.Wpf;

public static class ImGuiProgram
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Peekaboo", "peekaboo.log");

    private static void Log(string msg)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath)!;
            Directory.CreateDirectory(dir);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n");
        }
        catch { }
    }

    public static int Run(string[] args)
    {
        Log("Peekaboo ImGui starting...");

        var services = ConfigureServices();
        var viewModel = services.GetRequiredService<ImGuiMainViewModel>();
        var imGuiManager = services.GetRequiredService<ImGuiManager>();

        var options = WindowOptions.Default;
        options.Title = "Peekaboo - Windows Automation Agent";
        options.Size = new System.Numerics.Vector2i(1280, 800);
        options.Position = new System.Numerics.Vector2i(100, 100);
        options.VSync = true;

        using var window = Silk.NET.Windowing.Window.Create(options);
        
        window.Load += () =>
        {
            Log("Window loading...");
            var gl = GL.GetApi(window);
            
            try
            {
                ImGuiInterop.Init(window, gl);
                imGuiManager.Initialize();
                viewModel.Initialize();
                Log("ImGui initialized successfully");
            }
            catch (Exception ex)
            {
                Log($"ERROR: Failed to initialize ImGui: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        };

        window.Render += (delta) =>
        {
            try
            {
                var gl = GL.GetApi(window);
                gl.Viewport(0, 0, (uint)window.Size.X, (uint)window.Size.Y);
                gl.ClearColor(0.06f, 0.06f, 0.06f, 1.0f);
                gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                ImGuiInterop.NewFrame(window);
                imGuiManager.NewFrame();
                
                viewModel.Render();
                
                ImGuiInterop.Render(gl);
            }
            catch (Exception ex)
            {
                Log($"Render error: {ex.Message}");
            }
        };

        window.Closing += () =>
        {
            Log("Window closing...");
            try
            {
                viewModel.Shutdown();
                imGuiManager.Shutdown();
                ImGuiInterop.Shutdown();
            }
            catch (Exception ex)
            {
                Log($"Shutdown error: {ex.Message}");
            }
        };

        window.Run();
        
        if (services is IDisposable disposable)
            disposable.Dispose();
        
        Log("Peekaboo ImGui exited");
        return 0;
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        var loggerFactory = LoggerFactory.Create(b =>
        {
            b.SetMinimumLevel(LogLevel.Debug);
            b.AddConsole().SetMinimumLevel(LogLevel.Warning);
        });
        services.AddSingleton<ILoggerFactory>(loggerFactory);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        Log("Configuring Peekaboo services...");

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

        var settings = AiSettings.Load();
        Log($"AI settings loaded: provider={settings.SelectedProvider}, model={settings.SelectedModel}");
        services.AddSingleton(settings);
        services.AddSingleton<SessionStore>();
        services.AddSingleton<IAgentService, PeekabooAgentService>();

        services.AddSingleton<ImGuiManager>();
        services.AddSingleton<ImGuiMainViewModel>();

        return services.BuildServiceProvider();
    }

    public static void Main(string[] args)
    {
        try
        {
            Run(args);
        }
        catch (Exception ex)
        {
            Log($"FATAL: {ex.Message}\n{ex.StackTrace}");
            Console.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}