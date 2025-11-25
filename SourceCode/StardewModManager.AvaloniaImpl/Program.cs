namespace StardewModManager.AvaloniaImpl;

using System;
using Avalonia;
using Avalonia.ReactiveUI;
using Core.Services.Logger;
using FanatikiLauncher.MVVM.Extensions;
using NLog;
using ReactiveUI;

sealed class Program
{
    private static readonly ILogger s_logger = LogManager.GetCurrentClassLogger();
    
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        SetupLogger();
        
        s_logger.Info("Logger configured");
        
        RxApp.MainThreadScheduler = AvaloniaScheduler.Instance;
        
        s_logger.Info("Starting app...");

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
            s_logger.Info("App shutted down");
        }
        catch (Exception e)
        {
            s_logger.Fatal(e);
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace()
        .UseReactiveUI()
        .UseBootstrapper<StardewModManagerBootstrapper>([typeof(App).Assembly]);

    private static void SetupLogger()
    {
        NLogConfigManager.EnsureNLogConfig(typeof(Program).Assembly, "StardewModManager.AvaloniaImpl.NLog.config");

        LogManager.Setup(cfg => cfg.LoadConfigurationFromFile(NLogConfigManager.NLogConfigPath));
        LogManager.ReconfigExistingLoggers();
    }
}