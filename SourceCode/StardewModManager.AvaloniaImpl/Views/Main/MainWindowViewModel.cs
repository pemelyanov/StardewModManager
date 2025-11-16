namespace StardewModManager.AvaloniaImpl.Views.Main;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using ReactiveUI.Fody.Helpers;
using Core.Data;
using Core.Services.ModManager;
using Core.Services.SteamManager;
using ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly ISteamManager m_steamManager;
    private readonly IModManger    m_modManger;
    private const    string        StardewValleyAppId = "413150";
    private const    string        ModsFolder         = "Mods";
    private const    string        DisabledModsFolder = "DisabledMods";

    public MainWindowViewModel(ISteamManager steamManager, IModManger modManger)
    {
        m_steamManager = steamManager;
        m_modManger = modManger;
        StardewPath = m_modManger.StardewPath;
        IsSMAPIInstalled = CheckIsSMAPIInstalled();

        SteamUsers = steamManager.GetLocalUsersList().Select(it => new SteamUserViewModel(it)).ToArray();

        var firstUser = SteamUsers.FirstOrDefault();
        steamManager.CurrentUser = firstUser?.User;

        if (firstUser is not null) firstUser.IsSelected = true;

        CanSelectUser = SteamUsers.Count > 1;

        IsSMAPIEnabled = CheckIsSMAPIEnabled();
        UpdateModsList();
    }

    [Reactive]
    public string StardewPath { get; set; }

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
    public IReadOnlyList<ModViewModel> Mods { get; private set; } = [];
    
    [Reactive]
    public IObservable<LoadingProgress>? SMAPIInstallationProgress { get; private set; }

    public async Task ExportModPack()
    {
        if (SaveModPackAction is null) return;

        var packPath = await SaveModPackAction.Invoke();

        if (packPath is null) return;
        
        if(File.Exists(packPath)) File.Delete(packPath);

        var modsPath = GetModsFolderPath();
        
        ZipFile.CreateFromDirectory(modsPath, packPath);
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

        try
        {
            await m_modManger.InstallLatestAsync(progress);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        
        IsSMAPIInstalled = CheckIsSMAPIInstalled();

        SMAPIInstallationProgress = null;
        
        if(!IsSMAPIEnabled)
            ToggleSMAPIEnabled();
    }

    public void SelectUser(SteamUser user)
    {
        m_steamManager.CurrentUser = user;
        IsSMAPIEnabled = CheckIsSMAPIEnabled();
    }

    public void LaunchStardew() => m_steamManager.LaunchSteamGame(StardewValleyAppId);

    public void ToggleSMAPIEnabled()
    {
        var isEnabled = CheckIsSMAPIEnabled();

        m_steamManager.CloseSteam();

        if (isEnabled)
        {
            m_steamManager.SetLaunchOptions(StardewValleyAppId, "");
        }
        else
        {
            m_steamManager.SetLaunchOptions(StardewValleyAppId, GetStardewLaunchOptions());
        }
    }

    public async Task SelectStardewPathAsync()
    {
        if (SelectStardewFolderAction is null) return;

        var path = await SelectStardewFolderAction.Invoke();

        if (string.IsNullOrEmpty(path)) return;

        StardewPath = path;

        IsSMAPIInstalled = CheckIsSMAPIInstalled();
    }

    public async Task InstallModPackAsync()
    {
        if (OpenModPackAction is null) return;

        var packPath = await OpenModPackAction.Invoke();

        if (packPath is null) return;

        var modsPath = GetModsFolderPath();
        var disabledModsPath = GetDisabledModsFolderPath();

        ClearFolderOrCreateNew(modsPath);
        ClearFolderOrCreateNew(disabledModsPath);

        ZipFile.ExtractToDirectory(packPath, modsPath);
        
        UpdateModsList();
    }
    
    public void ToggleMod(ModViewModel mod)
    {
        var disabledMods = GetDisabledModsFolderPath();
        var mods = GetModsFolderPath();

        if (!Directory.Exists(disabledMods)) Directory.CreateDirectory(disabledMods);
        if (!Directory.Exists(mods)) Directory.CreateDirectory(mods);
        
        if (mod.IsEnabled)
            Directory.Move(Path.Combine(disabledMods, mod.Name), Path.Combine(mods, mod.Name));
        else
            Directory.Move(Path.Combine(mods, mod.Name), Path.Combine(disabledMods, mod.Name));
    }

    private bool CheckIsSMAPIInstalled() => File.Exists(GetSMAPIPath());

    private bool CheckIsSMAPIEnabled()
    {
        var launchOptions = m_steamManager.GetLaunchOptions(StardewValleyAppId);

        return launchOptions == GetStardewLaunchOptions();
    }

    private string GetSMAPIPath() => Path.Combine(StardewPath, "StardewModdingAPI.exe");

    private string GetStardewLaunchOptions() => $@"""{GetSMAPIPath()}"" %command%";

    private string GetModsFolderPath() => Path.Combine(StardewPath, ModsFolder);

    private string GetDisabledModsFolderPath() => Path.Combine(StardewPath, DisabledModsFolder);

    private void ClearFolderOrCreateNew(string folderPath)
    {
        if (Directory.Exists(folderPath))
        {
            var dirs = Directory.GetDirectories(folderPath);
            foreach (var folder in dirs)
                Directory.Delete(folder, true);

            var files = Directory.GetFiles(folderPath);
            foreach (var file in files)
                File.Delete(file);

            return;
        }

        Directory.CreateDirectory(folderPath);
    }

    private void UpdateModsList()
    {
        var modsFolder = GetModsFolderPath();
        var disabledModsFolder = GetDisabledModsFolderPath();

        IEnumerable<ModViewModel> mods = [];

        if (Directory.Exists(modsFolder))
        {
            var enabledMods = Directory.GetDirectories(modsFolder)
                .Select(
                    it => new ModViewModel(Path.GetFileName(it))
                    {
                        IsEnabled = true
                    }
                );

            mods = mods.Concat(enabledMods);
        }

        if (Directory.Exists(disabledModsFolder))
        {
            var disabledMods = Directory.GetDirectories(disabledModsFolder)
                .Select(
                    it => new ModViewModel(Path.GetFileName(it))
                    {
                        IsEnabled = false
                    }
                );

            mods = mods.Concat(disabledMods);
        }

        Mods = mods.OrderBy(it => it.Name).ToArray();
    }
}