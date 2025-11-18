namespace StardewModManager.AvaloniaImpl;

using Autofac;
using Core.Services.Configuration;
using Core.Services.Dialog;
using Core.Services.ModManager;
using Core.Services.SteamManager;
using FanatikiLauncher.MVVM;
using ReactiveUI;
using Views.Main;
using Views.Mods;
using Views.Settings;

public class StardewModManagerBootstrapper : BootstrapperBase<StardewModManagerBootstrapper>
{
    protected override void RegisterViewModels(ContainerBuilder builder)
    {
        builder.RegisterType<MainWindowViewModel>().As<IScreen>().AsSelf().SingleInstance();
        builder.RegisterType<ModsPageViewModel>().SingleInstance();
        builder.RegisterType<SettingsPageViewModel>().SingleInstance();
    }

    protected override void RegisterServices(ContainerBuilder builder)
    {
        builder.RegisterType<MainWindow>().AsSelf().As<IDialogService>().SingleInstance();
        builder.RegisterType<SMAPIModManager>().As<IModManger>().SingleInstance();
        builder.RegisterType<SteamManger>().As<ISteamManager>().SingleInstance();
        builder.RegisterType<JsonModManagerConfigurationService>().AsImplementedInterfaces().SingleInstance();
    }
}