namespace StardewModManager.AvaloniaImpl.Views.Main;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.Data;
using Core.Services.SteamManager;
using FanatikiLauncher.MVVM.ViewModels;
using Mods;
using ReactiveUI;
using Splat;
using ViewModels;

public class MainWindowViewModel : ViewModelBase, IScreen
{
    private readonly ISteamManager m_steamManager;

    public MainWindowViewModel(ISteamManager steamManager, ModsPageViewModel modsPageViewModel)
    {
        m_steamManager = steamManager;

        SteamUsers = steamManager.GetLocalUsersList().Select(it => new SteamUserViewModel(it)).ToArray();

        var firstUser = SteamUsers.FirstOrDefault(it => it.User == steamManager.CurrentUser);

        if (firstUser is not null) firstUser.IsSelected = true;

        CanSelectUser = SteamUsers.Count > 1;

        Router.Navigate.Execute(modsPageViewModel);
    }

    public RoutingState Router { get; } = new();

    public IReadOnlyList<SteamUserViewModel> SteamUsers { get; }

    public bool CanSelectUser { get; }
    
    public void SelectUser(SteamUser user)
    {
        m_steamManager.CurrentUser = user;
    }

    public void SelectPage(Type pageType)
    {
        var page = Locator.Current.GetService(pageType) as IRoutableViewModel;
        
        if(page is null) return;

        Router.Navigate.Execute(page);
    }
}