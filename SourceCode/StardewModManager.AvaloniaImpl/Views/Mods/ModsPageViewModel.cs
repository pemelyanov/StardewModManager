namespace StardewModManager.AvaloniaImpl.Views.Mods;

using System.Reactive.Subjects;
using Core.Constants;
using Core.Data;
using Core.Services.ModManager;
using Core.Services.SteamManager;
using FanatikiLauncher.MVVM.ViewModels;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ViewModels;

public class ModsPageViewModel : ViewModelBase, IRoutableViewModel
{
    private readonly Lazy<IScreen> m_hostScreen;
    private readonly IModManger    m_modManger;
    private readonly ISteamManager m_steamManager;

    public ModsPageViewModel(Lazy<IScreen> hostScreen, IModManger modManger, ISteamManager steamManager)
    {
        m_hostScreen = hostScreen;
        m_modManger = modManger;
        m_steamManager = steamManager;
        
        UpdateRecentModPacksList();
        UpdateModsList();
    }

    public string UrlPathSegment => "mods";

    public IScreen HostScreen => m_hostScreen.Value;

    public IObservable<bool> IsSMAPIInstalled => m_modManger.IsInstalled;

    public IObservable<bool> IsSMAPIEnabled => m_modManger.IsEnabled;

    public Func<Task<string?>>? OpenModPackAction { get; set; }

    public Func<Task<string?>>? SaveModPackAction { get; set; }

    [Reactive]
    public IReadOnlyList<Mod> Mods { get; private set; } = [];
    
    [Reactive]
    public IReadOnlyList<RecentModPackViewModel> RecentModPacks { get; private set; } = [];

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

        SMAPIInstallationProgress = null;
    }

    public void LaunchStardew() => m_steamManager.LaunchSteamGame(SteamAppIds.StardewValley);

    public void ToggleSMAPIEnabled()
    {
        m_modManger.ToggleIsEnabled();
    }

    public async Task InstallModPackAsync()
    {
        if (OpenModPackAction is null) return;

        var packPath = await OpenModPackAction.Invoke();

        if (packPath is null) return;

        await InstallModPackByPathAsync(packPath);
    }

    public async Task InstallModPackByPathAsync(string packPath)
    {
        await m_modManger.InstallModPackAsync(packPath);

        UpdateRecentModPacksList();
        UpdateModsList();
    }

    public void ToggleMod(Mod mod)
    {
        m_modManger.ToggleMod(mod);
    }

    public void DeleteRecentModPack(RecentModPackViewModel viewModel)
    {
        m_modManger.DeleteRecentMod(viewModel.Info);
        
        UpdateRecentModPacksList();
    }

    private void UpdateModsList()
    {
        Mods = m_modManger.Mods;
    }

    private void UpdateRecentModPacksList()
    {
        RecentModPacks = m_modManger.RecentModPacks.Select(it => new RecentModPackViewModel(it)).ToArray();
    }
}