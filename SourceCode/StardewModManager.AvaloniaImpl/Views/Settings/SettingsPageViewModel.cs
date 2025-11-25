namespace StardewModManager.AvaloniaImpl.Views.Settings;

using System.Diagnostics;
using Core.Services.Logger;
using Core.Services.ModManager;
using Core.Services.SteamManager;
using FanatikiLauncher.MVVM.ViewModels;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

public class SettingsPageViewModel(Lazy<IScreen> hostScreen, IModManger modManger, ISteamManager steamManager)
    : ViewModelBase, IRoutableViewModel
{
    public string UrlPathSegment => "settings";

    public IScreen HostScreen => hostScreen.Value;

    public Func<Task<string?>>? SelectFolderAction { get; set; }

    [Reactive]
    public string StardewPath { get; private set; } = modManger.StardewPath;

    [Reactive]
    public string SteamPath { get; private set; } = steamManager.SteamPath;

    public string LogsPath { get; } = NLogConfigManager.LogsFolder;

    public async Task SelectStardewPathAsync()
    {
        if (SelectFolderAction is null) return;

        var path = await SelectFolderAction.Invoke();

        if (string.IsNullOrEmpty(path)) return;

        modManger.SetCustomStardewPath(path);

        StardewPath = path;
    }

    public async Task SelectSteamPathAsync()
    {
        if (SelectFolderAction is null) return;

        var path = await SelectFolderAction.Invoke();

        if (string.IsNullOrEmpty(path)) return;

        steamManager.SetCustomSteamPath(path);

        SteamPath = path;
    }
    
    public void OpenInExplorer(string? path)
    {
        if(string.IsNullOrEmpty(path)) return;
        
        Process.Start("explorer.exe", path);
    }
}