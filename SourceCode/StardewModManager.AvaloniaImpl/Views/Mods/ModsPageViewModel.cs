namespace StardewModManager.AvaloniaImpl.Views.Mods;

using System.Reactive.Subjects;
using Core.Constants;
using Core.Data;
using Core.Services.Dialog;
using Core.Services.ModManager;
using Core.Services.SteamManager;
using FanatikiLauncher.MVVM.ViewModels;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ViewModels;

public class ModsPageViewModel : ViewModelBase, IRoutableViewModel
{
    private readonly Lazy<IScreen>  m_hostScreen;
    private readonly IModManger     m_modManger;
    private readonly ISteamManager  m_steamManager;
    private readonly IDialogService m_dialogService;

    public ModsPageViewModel(
        Lazy<IScreen> hostScreen,
        IModManger modManger,
        ISteamManager steamManager,
        IDialogService dialogService
    )
    {
        m_hostScreen = hostScreen;
        m_modManger = modManger;
        m_steamManager = steamManager;
        m_dialogService = dialogService;

        UpdateRecentModPacksList();
        UpdateModsList();
    }

    public string UrlPathSegment => "mods";

    public IScreen HostScreen => m_hostScreen.Value;

    public IObservable<bool> IsSMAPIInstalled => m_modManger.IsInstalled;

    public IObservable<bool> IsSMAPIEnabled => m_modManger.IsEnabled;

    public Func<Task<string?>>? OpenArchiveAction { get; set; }

    public Func<Task<string?>>? SaveModPackAction { get; set; }

    public bool ShouldConfirmModDeletion { get; set; } = true;

    [Reactive]
    public IReadOnlyList<Mod> Mods { get; private set; } = [];

    [Reactive]
    public IReadOnlyList<RecentModPackViewModel> RecentModPacks { get; private set; } = [];

    [Reactive]
    public IObservable<LoadingProgress>? SMAPIInstallationProgress { get; private set; }

    public async Task DeleteAllMods()
    {
        if (!await m_dialogService.ConfirmAsync(
            "Вы уверены что хотите удалить все моды?",
            "Удаление модов",
            "Да",
            "Отмена"
        )) return;

        foreach (var mod in Mods)
            m_modManger.DeleteMod(mod);

        UpdateModsList();
    }

    public async Task InstallModAsync()
    {
        if (OpenArchiveAction is null) return;

        var modPath = await OpenArchiveAction.Invoke();

        if (modPath is null) return;

        await m_modManger.InstallModAsync(modPath);

        UpdateModsList();
    }

    public async Task DeleteModAsync(Mod mod)
    {
        if (ShouldConfirmModDeletion && !await m_dialogService.ConfirmAsync(
            $"Вы уверены что хотите удалить мод {mod.Name}?",
            "Удаление мода",
            "Да",
            "Отмена"
        )) return;

        m_modManger.DeleteMod(mod);

        UpdateModsList();
    }

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
        if (OpenArchiveAction is null) return;

        if (!await ConfirmModPackInstallationIfNeeded()) return;

        var packPath = await OpenArchiveAction.Invoke();

        if (packPath is null) return;

        await InstallModPackByPathWithAsync(packPath);
    }

    public async Task InstallModPackByPathWithConfirmationAsync(string packPath)
    {
        if (!await ConfirmModPackInstallationIfNeeded()) return;

        await InstallModPackByPathWithAsync(packPath);
    }

    public void ToggleMod(Mod mod)
    {
        m_modManger.ToggleMod(mod);
    }

    public void DeleteRecentModPack(RecentModPackViewModel viewModel)
    {
        m_modManger.DeleteRecentModPack(viewModel.Info);

        UpdateRecentModPacksList();
    }

    private async Task InstallModPackByPathWithAsync(string packPath)
    {
        await m_modManger.InstallModPackAsync(packPath);

        UpdateRecentModPacksList();
        UpdateModsList();
    }

    private void UpdateModsList()
    {
        Mods = m_modManger.Mods;
    }

    private void UpdateRecentModPacksList()
    {
        RecentModPacks = m_modManger.RecentModPacks.Select(it => new RecentModPackViewModel(it)).ToArray();
    }

    private async Task<bool> ConfirmModPackInstallationIfNeeded()
    {
        if (Mods.Count > 0 && !await m_dialogService.ConfirmAsync(
            "Все текущие моды будут удалены. Вы уверены?",
            "Установка сборки"
        )) return false;

        return true;
    }
}