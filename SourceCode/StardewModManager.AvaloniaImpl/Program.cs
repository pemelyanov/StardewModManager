namespace StardewModManager.AvaloniaImpl;

using System;
using Avalonia;
using Avalonia.ReactiveUI;
using FanatikiLauncher.MVVM.Extensions;
using ReactiveUI;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        RxApp.MainThreadScheduler = AvaloniaScheduler.Instance;
        
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace()
        .UseReactiveUI()
        .UseBootstrapper<StardewModManagerBootstrapper>();
}