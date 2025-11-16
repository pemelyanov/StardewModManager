namespace StardewModManager.AvaloniaImpl.Views.Main;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Core.Constants;
using ReactiveUI.Fody.Helpers;
using Core.Data;
using Core.Services.ModManager;
using Core.Services.SteamManager;
using ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly ISteamManager m_steamManager;
    private readonly IModManger    m_modManger;

    public MainWindowViewModel(ISteamManager steamManager, IModManger modManger)
    {
        m_steamManager = steamManager;
        m_modManger = modManger;
        StardewPath = m_modManger.StardewPath;
        IsSMAPIInstalled = m_modManger.IsInstalled;

        SteamUsers = steamManager.GetLocalUsersList().Select(it => new SteamUserViewModel(it)).ToArray();

        var firstUser = SteamUsers.FirstOrDefault(it => it.User == steamManager.CurrentUser);

        if (firstUser is not null) firstUser.IsSelected = true;

        CanSelectUser = SteamUsers.Count > 1;

        IsSMAPIEnabled = m_modManger.IsEnabled;

        UpdateModsList();
    }

    [Reactive]
    public string StardewPath { get; private set; }

    [Reactive]
    public bool IsSMAPIInstalled { get; private set; }

    [Reactive]
    public bool IsSMAPIEnabled { get; set; }

    public IReadOnlyList<SteamUserViewModel> SteamUsers { get; }

    public bool CanSelectUser { get; }

    public Func<Task<string?>>? SelectStardewFolderAction { get; set; }

    public Func<Task<string?>>? OpenModPackAction { get; set; }

    public Func<Task<string?>>? SaveModPackAction { get; set; }

    [Reactive]
    public IReadOnlyList<Mod> Mods { get; private set; } = [];

    [Reactive]
    public IObservable<LoadingProgress>? SMAPIInstallationProgress { get; private set; }

    public async Task ExportModPackAsync()
    {
        if (SaveModPackAction is null) return;

        var packPath = await SaveModPackAction.Invoke();

        if (packPath is null) return;

        await m_modManger.ExportToModPackAsync(packPath);
    }

    public async Task InstallSMAPIAsync()
    {
        var progress = new BehaviorSubject<LoadingProgress>(
            new LoadingProgress
            {
                StageName = "Начало установки..."
            }
        );

        SMAPIInstallationProgress = progress;

        await m_modManger.InstallLatestAsync(progress);

        IsSMAPIInstalled = m_modManger.IsInstalled;

        SMAPIInstallationProgress = null;

        if (!IsSMAPIEnabled)
            ToggleSMAPIEnabled();
    }

    public void SelectUser(SteamUser user)
    {
        m_steamManager.CurrentUser = user;
        IsSMAPIEnabled = m_modManger.IsEnabled;
    }

    public void LaunchStardew() => m_steamManager.LaunchSteamGame(SteamAppIds.StardewValley);

    public void ToggleSMAPIEnabled()
    {
        m_modManger.ToggleIsEnabled();
    }

    public async Task SelectStardewPathAsync()
    {
        if (SelectStardewFolderAction is null) return;

        var path = await SelectStardewFolderAction.Invoke();

        if (string.IsNullOrEmpty(path)) return;

        m_modManger.SetCustomStardewPath(path);

        StardewPath = path;

        IsSMAPIInstalled = m_modManger.IsInstalled;
    }

    public async Task InstallModPackAsync()
    {
        if (OpenModPackAction is null) return;

        var packPath = await OpenModPackAction.Invoke();

        if (packPath is null) return;

        await m_modManger.InstallModPackAsync(packPath);

        UpdateModsList();
    }

    public void ToggleMod(Mod mod)
    {
        m_modManger.ToggleMod(mod);
    }
    
    private void UpdateModsList()
    {
        Mods = m_modManger.Mods;
    }
}