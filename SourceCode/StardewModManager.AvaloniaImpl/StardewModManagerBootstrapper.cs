namespace StardewModManager.AvaloniaImpl;

using Autofac;
using Core.Services.Configuration;
using Core.Services.ModManager;
using Core.Services.SteamManager;
using FanatikiLauncher.MVVM;
using Views.Main;

public class StardewModManagerBootstrapper : BootstrapperBase<StardewModManagerBootstrapper>
{
    protected override void RegisterViewModels(ContainerBuilder builder)
    {
        builder.RegisterType<MainWindowViewModel>().SingleInstance();
    }

    protected override void RegisterServices(ContainerBuilder builder)
    {
        builder.RegisterType<SMAPIModManager>().As<IModManger>().SingleInstance();
        builder.RegisterType<SteamManger>().As<ISteamManager>().SingleInstance();
        builder.RegisterType<JsonModManagerConfigurationService>().AsImplementedInterfaces().SingleInstance();
    }
}