namespace StardewModManager.AvaloniaImpl.ViewModels;

using Core.Data;
using FanatikiLauncher.MVVM.ViewModels;
using ReactiveUI.Fody.Helpers;

public class SteamUserViewModel(SteamUser user) : ViewModelBase
{
    public SteamUser User { get; } = user;

    [Reactive]
    public bool IsSelected { get; set; }
}