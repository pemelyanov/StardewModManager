namespace StardewModManager.AvaloniaImpl.Services;

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using BigHelp.Http;
using Core.Data;
using NLog;
using Octokit;
using ProductHeaderValue = Octokit.ProductHeaderValue;

public class SMAPIInstallService
{
    #region Fields

    private const           string  RepoName  = "SMAPI";
    private const           string  RepoOwner = "Pathoschild";
    private static readonly ILogger s_logger  = LogManager.GetCurrentClassLogger();

    #endregion

    #region Properties

    public static SMAPIInstallService Instance { get; } = new();

    #endregion

    #region Methods

    public async Task InstallLatestAsync(IObserver<LoadingProgress>? observer, string stardewFolder)
    {
        s_logger.Info("Downloading latest release...");
        Release? latestRelease = await GetLatestReleaseAsync();

        if (latestRelease is null) return;

        s_logger.Info("Release found: {asset}...", latestRelease.TagName);

        ReleaseAsset? launcherAsset =
            latestRelease.Assets.FirstOrDefault(it => it.Name.StartsWith("SMAPI") && it.Name.EndsWith("installer.zip"));

        if (launcherAsset is null) return;

        s_logger.Info("Asset found: {asset}...", launcherAsset.BrowserDownloadUrl);

        string updatePath = Path.GetTempFileName();

        await DownloadUpdateAsync(launcherAsset.BrowserDownloadUrl, updatePath, observer);
        
        // Распаковка архива
        s_logger.Info("Extracting SMAPI installer...");
        var extractStage = new LoadingProgress
        {
            StageName = "Распаковываем установщик...",
            TotalTasksQuantity = new BehaviorSubject<int>(100),
        };
        observer?.OnNext(extractStage);
        
        string extractPath = await ExtractInstallerAsync(updatePath, observer);
        
        // Установка SMAPI
        s_logger.Info("Installing SMAPI...");
        var installStage = new LoadingProgress
        {
            StageName = "Устанавливаем SMAPI...",
            TotalTasksQuantity = new BehaviorSubject<int>(100),
        };
        observer?.OnNext(installStage);
        
        await InstallSMAPIAsync(extractPath, stardewFolder, observer);
        
        // Очистка временных файлов
        CleanupTempFiles(updatePath, extractPath);
        
        s_logger.Info("SMAPI installation completed successfully!");
    }

    private Task<string> ExtractInstallerAsync(string archivePath, IObserver<LoadingProgress>? observer)
    {
        string tempExtractPath = Path.Combine(Path.GetTempPath(), "SMAPI_Installer_" + Guid.NewGuid().ToString()[..8]);
        
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
                    
                    // Если это директория - создаем её
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(fullPath);
                    }
                    else
                    {
                        // Создаем родительские директории если нужно
                        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                        entry.ExtractToFile(fullPath, true);
                    }
                    
                    processed++;
                    observer?.OnNext(new LoadingProgress
                    {
                        StageName = "Распаковываем установщик...",
                        TotalTasksQuantity = new BehaviorSubject<int>(totalEntries),
                        ProcessedTasksQuantity = new BehaviorSubject<int>(processed)
                    });
                }
                catch (Exception ex)
                {
                    s_logger.Warn(ex, "Failed to extract entry: {entry}", entry.FullName);
                }
            }
            
            s_logger.Info("Extraction completed to: {path}", tempExtractPath);
            return Task.FromResult(tempExtractPath);
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, "Failed to extract SMAPI installer");
            throw;
        }
    }

    private async Task InstallSMAPIAsync(string extractPath, string stardewFolder, IObserver<LoadingProgress>? observer)
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
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    s_logger.Info("SMAPI Installer: {output}", e.Data);
                }
            };
            process.ErrorDataReceived += (sender, e) =>
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
            observer?.OnNext(new LoadingProgress
            {
                StageName = "Установка завершена!",
                TotalTasksQuantity = new BehaviorSubject<int>(100),
                ProcessedTasksQuantity = new BehaviorSubject<int>(100)
            });
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, "SMAPI installation failed");
            throw;
        }
    }

    private void CleanupTempFiles(string archivePath, string extractPath)
    {
        try
        {
            // Удаляем временный архив
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
                s_logger.Info("Temporary archive deleted: {path}", archivePath);
            }
            
            // Удаляем временную папку распаковки
            if (Directory.Exists(extractPath))
            {
                Directory.Delete(extractPath, true);
                s_logger.Info("Temporary extraction directory deleted: {path}", extractPath);
            }
        }
        catch (Exception ex)
        {
            s_logger.Warn(ex, "Failed to clean up temporary files");
        }
    }

    private async Task<Release?> GetLatestReleaseAsync()
    {
        try
        {
            var client = new GitHubClient(new ProductHeaderValue(RepoName));
            // client.Credentials = new Credentials(AccessToken);
            Release? release = await client.Repository.Release.GetLatest(RepoOwner, RepoName);

            return release;
        }
        catch
        {
            return null;
        }
    }

    private async Task DownloadUpdateAsync(
        string downloadUrl,
        string downloadPath,
        IObserver<LoadingProgress>? observer
    )
    {
        s_logger.Info("Downloading asset: {url} -> {path}", downloadUrl, downloadPath);
        var stage = new LoadingProgress
        {
            StageName = "Загружаем файлы...",
            TotalTasksQuantity = new BehaviorSubject<int>(100),
        };
        observer?.OnNext(stage);

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)"
        );

        httpClient.DefaultRequestHeaders.Add("Accept", "application/octet-stream");

        await httpClient.DownloadFileAsync(
            downloadUrl,
            Path.GetDirectoryName(downloadPath)!,
            Path.GetFileName(downloadPath),
            new Progress<HttpDownloadProgress>(
                progress =>
                {
                    s_logger.Trace("Download progress: {percent}%", progress.PercentDownloaded * 100);
                    stage.ProcessedTasksQuantity.OnNext((int)(progress.PercentDownloaded * 100));
                }
            )
        );
    }

    #endregion
}