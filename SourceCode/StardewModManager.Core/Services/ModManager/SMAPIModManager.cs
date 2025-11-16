namespace StardewModManager.Core.Services.ModManager;

using System.Diagnostics;
using System.IO.Compression;
using System.Reactive.Subjects;
using BigHelp.Http;
using Configuration;
using Constants;
using Data;
using GitHubManager;
using NLog;
using Octokit;
using SteamManager;

public class SMAPIModManager : IModManger
{
    private static readonly ILogger s_logger = LogManager.GetCurrentClassLogger();
    private readonly        GitHubRepositoryManager m_gitHubRepositoryManager = new("Pathoschild", "SMAPI");
    private readonly        ISteamManager m_steamManager;
    private readonly        IConfigurationService<ModManagerConfig> m_configurationService;
    private const           string ModsFolder = "Mods";
    private const           string DisabledModsFolder = "DisabledMods";

    public SMAPIModManager(ISteamManager steamManager, IConfigurationService<ModManagerConfig> configurationService)
    {
        m_steamManager = steamManager;
        m_configurationService = configurationService;

        StardewPath = ResolveStardewPath();

        IsInstalled = CheckIsSMAPIInstalled();
        IsEnabled = CheckIsSMAPIEnabled();

        UpdateModsList();
        
        steamManager.CurrentUserChanged += SteamManagerOn_CurrentUserChanged;
    }

    public string StardewPath { get; private set; }

    public bool IsEnabled { get; private set; }

    public bool IsInstalled { get; private set; }

    public IReadOnlyList<Mod> Mods { get; private set; } = [];

    public async Task InstallLatestAsync(IObserver<LoadingProgress>? observer)
    {
        List<string> tempPaths = [];
        try
        {
            s_logger.Info("Downloading latest release...");
            Release? latestRelease = await m_gitHubRepositoryManager.GetLatestReleaseAsync();

            if (latestRelease is null) return;

            s_logger.Info("Release found: {asset}...", latestRelease.TagName);

            ReleaseAsset? asset =
                latestRelease.Assets.FirstOrDefault(
                    it => it.Name.StartsWith("SMAPI") && it.Name.EndsWith("installer.zip")
                );

            if (asset is null) return;

            s_logger.Info("Asset found: {asset}...", asset.Name);

            string assetDownloadPath = Path.GetTempFileName();
            tempPaths.Add(assetDownloadPath);

            var stage = new LoadingProgress
            {
                StageName = "Загружаем файлы...",
                TotalTasksQuantity = new BehaviorSubject<int>(100),
            };
            observer?.OnNext(stage);

            await m_gitHubRepositoryManager.DownloadAsset(
                asset,
                assetDownloadPath,
                new Progress<HttpDownloadProgress>(
                    progress =>
                    {
                        s_logger.Trace("Download progress: {percent}%", progress.PercentDownloaded * 100);
                        stage.ProcessedTasksQuantity.OnNext((int)(progress.PercentDownloaded * 100));
                    }
                )
            );

            s_logger.Info("Extracting SMAPI installer...");
            var extractStage = new LoadingProgress
            {
                StageName = "Распаковываем установщик...",
                TotalTasksQuantity = new BehaviorSubject<int>(100),
            };
            observer?.OnNext(extractStage);

            string extractPath = await ExtractInstallerAsync(assetDownloadPath, observer);
            tempPaths.Add(extractPath);

            s_logger.Info("Installing SMAPI...");
            var installStage = new LoadingProgress
            {
                StageName = "Устанавливаем SMAPI...",
                TotalTasksQuantity = new BehaviorSubject<int>(100),
            };
            observer?.OnNext(installStage);

            await InstallSMAPIAsync(extractPath, StardewPath);

            IsInstalled = CheckIsSMAPIInstalled();

            if (IsInstalled)
            {
                s_logger.Info("SMAPI installation completed successfully!");
            }
            else
            {
                s_logger.Warn("Something went wrong, installation completed, but SMAPI not installed");
            }
        }
        finally
        {
            CleanupTempFiles(tempPaths);
        }
    }

    public void SetCustomStardewPath(string? path)
    {
        m_configurationService.UpdateConfig(
            m_configurationService.Config with
            {
                CustomStardewPath = path
            }
        );

        StardewPath = ResolveStardewPath();

        IsInstalled = CheckIsSMAPIInstalled();
    }

    public void ToggleIsEnabled()
    {
        m_steamManager.CloseSteam();

        var launchOptions = IsEnabled ? "" : GetStardewLaunchOptions();

        m_steamManager.SetLaunchOptions(SteamAppIds.StardewValley, launchOptions);

        IsEnabled = !IsEnabled;
    }

    public async Task ExportToModPackAsync(string path)
    {
        if (File.Exists(path)) File.Delete(path);

        var modsPath = GetModsFolderPath();

        await ZipFile.CreateFromDirectoryAsync(modsPath, path);
    }

    public async Task InstallModPackAsync(string path)
    {
        var modsPath = GetModsFolderPath();
        var disabledModsPath = GetDisabledModsFolderPath();

        ClearFolderOrCreateNew(modsPath);
        ClearFolderOrCreateNew(disabledModsPath);

        await ZipFile.ExtractToDirectoryAsync(path, modsPath);

        UpdateModsList();
    }

    public void ToggleMod(Mod mod)
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

        IEnumerable<Mod> mods = [];

        if (Directory.Exists(modsFolder))
        {
            var enabledMods = Directory.GetDirectories(modsFolder)
                .Select(
                    it => new Mod(Path.GetFileName(it))
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
                    it => new Mod(Path.GetFileName(it))
                    {
                        IsEnabled = false
                    }
                );

            mods = mods.Concat(disabledMods);
        }

        Mods = mods.OrderBy(it => it.Name).ToArray();
    }

    private bool CheckIsSMAPIInstalled() => File.Exists(GetSMAPIPath());

    private bool CheckIsSMAPIEnabled()
    {
        var launchOptions = m_steamManager.GetLaunchOptions(SteamAppIds.StardewValley);

        return launchOptions == GetStardewLaunchOptions();
    }

    private string GetSMAPIPath() => Path.Combine(StardewPath, "StardewModdingAPI.exe");

    private string GetStardewLaunchOptions() => $@"""{GetSMAPIPath()}"" %command%";

    private string ResolveStardewPath() => m_configurationService.Config.CustomStardewPath ?? GetDefaultStardewPath();

    private string GetDefaultStardewPath() => Path.Combine(
        m_steamManager.SteamPath,
        "steamapps",
        "common",
        "Stardew Valley"
    );

    private Task<string> ExtractInstallerAsync(string archivePath, IObserver<LoadingProgress>? observer) => Task.Run(
        () =>
        {
            string tempExtractPath = Path.Combine(
                Path.GetTempPath(),
                "SMAPI_Installer_" + Guid.NewGuid().ToString()[..8]
            );

            try
            {
                Directory.CreateDirectory(tempExtractPath);

                using var archive = ZipFile.OpenRead(archivePath);
                var totalEntries = archive.Entries.Count;
                var processed = 0;

                foreach (var entry in archive.Entries)
                {
                    try
                    {
                        string fullPath = Path.Combine(tempExtractPath, entry.FullName);

                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(fullPath);
                        }
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                            entry.ExtractToFile(fullPath, true);
                        }

                        processed++;
                        observer?.OnNext(
                            new LoadingProgress
                            {
                                StageName = "Распаковываем установщик...",
                                TotalTasksQuantity = new BehaviorSubject<int>(totalEntries),
                                ProcessedTasksQuantity = new BehaviorSubject<int>(processed)
                            }
                        );
                    }
                    catch (Exception ex)
                    {
                        s_logger.Warn(ex, "Failed to extract entry: {entry}", entry.FullName);
                    }
                }

                s_logger.Info("Extraction completed to: {path}", tempExtractPath);
                return tempExtractPath;
            }
            catch (Exception ex)
            {
                s_logger.Error(ex, "Failed to extract SMAPI installer");
                throw;
            }
        }
    );

    private async Task InstallSMAPIAsync(string extractPath, string stardewFolder)
    {
        try
        {
            // Ищем папку с установщиком (формат: "SMAPI X.X.X installer")
            var installerDir = Directory.GetDirectories(extractPath)
                .FirstOrDefault(dir => dir.Contains("SMAPI") && dir.Contains("installer"));

            if (string.IsNullOrEmpty(installerDir))
            {
                throw new FileNotFoundException("SMAPI installer directory not found in extracted files");
            }

            // Путь к исполняемому файлу установщика
            string installerExePath = Path.Combine(installerDir, "internal", "windows", "SMAPI.Installer.exe");

            if (!File.Exists(installerExePath))
            {
                throw new FileNotFoundException($"SMAPI installer not found at: {installerExePath}");
            }

            s_logger.Info("Found SMAPI installer at: {path}", installerExePath);

            // Запускаем установщик
            var processStartInfo = new ProcessStartInfo
            {
                FileName = installerExePath,
                Arguments = $"--install --no-prompt --game-path \"{stardewFolder}\"",
                UseShellExecute = true,
                CreateNoWindow = false,
                WorkingDirectory = Path.GetDirectoryName(installerExePath)
            };

            using var process = new Process();
            process.StartInfo = processStartInfo;
            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    s_logger.Info("SMAPI Installer: {output}", e.Data);
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    s_logger.Error("SMAPI Installer Error: {error}", e.Data);
                }
            };

            s_logger.Info("Starting SMAPI installation...");
            bool started = process.Start();

            if (!started)
            {
                throw new InvalidOperationException("Failed to start SMAPI installer process");
            }

            // Ждем завершения установки
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new Exception($"SMAPI installer failed with exit code: {process.ExitCode}");
            }

            s_logger.Info("SMAPI installation completed successfully");
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, "SMAPI installation failed");
            throw;
        }
    }

    private void CleanupTempFiles(params IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    s_logger.Info("Temporary file deleted: {path}", path);
                }

                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                    s_logger.Info("Temporary directory deleted: {path}", path);
                }
            }
            catch (Exception ex)
            {
                s_logger.Warn(ex, "Failed to clean up temporary files");
            }
        }
    }

    private void SteamManagerOn_CurrentUserChanged(object? sender, SteamUser e)
    {
        IsEnabled = CheckIsSMAPIEnabled();
    }
}