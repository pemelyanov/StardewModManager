namespace StardewModManager.Core.Services.SteamManager;

using System.Diagnostics;
using Configuration;
using Data;
using Microsoft.Win32;
using NLog;
using Utils;

public class SteamManger : ISteamManager
{
    private static readonly ILogger s_logger = LogManager.GetCurrentClassLogger();
    private readonly IConfigurationService<ModManagerConfig> m_configurationService;

    public SteamManger(IConfigurationService<ModManagerConfig> configurationService)
    {
        m_configurationService = configurationService;
        SteamPath = ResolveSteamPath();
        CurrentUser = GetLocalUsersList().FirstOrDefault();
        s_logger.Info("SteamManager initialized with path: {SteamPath}", SteamPath);
    }

    public event EventHandler<SteamUser>? CurrentUserChanged;

    public string SteamPath { get; private set; }

    public SteamUser? CurrentUser
    {
        get;
        set
        {
            field = value;
            if (value is not null)
                CurrentUserChanged?.Invoke(this, value!);
        }
    }

    public void SetCustomSteamPath(string? path)
    {
        s_logger.Info("Setting custom Steam path: {Path}", path ?? "null");
        m_configurationService.UpdateConfig(
            m_configurationService.Config with
            {
                CustomSteamPath = path
            }
        );

        SteamPath = ResolveSteamPath();
        s_logger.Info("Steam path updated to: {SteamPath}", SteamPath);
    }

    public IReadOnlyList<SteamUser> GetLocalUsersList()
    {
        s_logger.Debug("Retrieving local users list");
        
        try
        {
            var localConfigs = FindAllLocalConfigs();

            var users = localConfigs.Select(
                    config =>
                    {
                        try
                        {
                            var content = SteamVdfParser.Parse(File.ReadAllText(config));
                            var friendsNode = FindEntry(content, "friends") as OrderedDictionary<string, object>;

                            if (friendsNode is null)
                            {
                                s_logger.Warn("Friends node not found in config: {Config}", config);
                                return null;
                            }
                            
                            var id = friendsNode.First().Key;
                            var nickName = FindEntry(friendsNode, "PersonaName") as string;
                            nickName = nickName?.Trim('\"');

                            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(nickName))
                            {
                                s_logger.Warn("Invalid user data in config: {Config}, ID: {Id}, Nickname: {Nickname}", 
                                    config, id, nickName);
                                return null;
                            }

                            s_logger.Debug("Found Steam user: {Id} - {Nickname}", id, nickName);
                            return new SteamUser()
                            {
                                Id = id,
                                NickName = nickName
                            };
                        }
                        catch (Exception ex)
                        {
                            s_logger.Error(ex, "Failed to parse user config: {Config}", config);
                            return null;
                        }
                    }
                )
                .Where(it => it is not null)
                .Select(it => it!)
                .ToArray();

            s_logger.Info("Retrieved {UserCount} local Steam users", users.Length);
            return users;
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, "Failed to get local users list");
            throw;
        }
    }

    public string? GetLaunchOptions(string appId)
    {
        if (string.IsNullOrEmpty(appId))
            throw new ArgumentException("App ID cannot be null or empty", nameof(appId));

        s_logger.Debug("Getting launch options for app: {AppId}", appId);

        try
        {
            var localConfigPath = ResolveLocalConfigPath();

            if (!File.Exists(localConfigPath))
                throw new FileNotFoundException($"Config file not found: {localConfigPath}");

            var content = File.ReadAllText(localConfigPath);
            var options = ExtractLaunchOptionsFromContent(content, appId);

            s_logger.Debug("Launch options for app {AppId}: {Options}", appId, options ?? "null");
            return options;
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, "Failed to get launch options for app: {AppId}", appId);
            throw;
        }
    }

    public void SetLaunchOptions(string appId, string launchOptions)
    {
        if (string.IsNullOrEmpty(appId))
            throw new ArgumentException("App ID cannot be null or empty", nameof(appId));

        s_logger.Info("Setting launch options for app: {AppId}, options: {Options}", appId, launchOptions);

        try
        {
            var localConfigPath = ResolveLocalConfigPath();

            if (!File.Exists(localConfigPath))
                throw new FileNotFoundException($"Config file not found: {localConfigPath}");

            var content = File.ReadAllText(localConfigPath);
            var updatedContent = UpdateLaunchOptionsInContent(content, appId, launchOptions);

            if (string.IsNullOrEmpty(updatedContent))
            {
                s_logger.Warn("No changes made to launch options for app: {AppId}", appId);
                return;
            }

            File.WriteAllText(localConfigPath, updatedContent);
            s_logger.Info("Successfully updated launch options for app: {AppId}", appId);
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, "Failed to set launch options for app: {AppId}", appId);
            throw;
        }
    }

    public void LaunchSteamGame(string appId)
    {
        s_logger.Info("Launching Steam game: {AppId}", appId);
        
        try
        {
            string steamUri = $"steam://rungameid/{appId}";
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = steamUri,
                    UseShellExecute = true
                }
            );
            s_logger.Debug("Steam game launch initiated: {AppId}", appId);
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, "Failed to launch Steam game: {AppId}", appId);
            throw;
        }
    }

    public void CloseSteam()
    {
        s_logger.Info("Closing Steam");
        
        try
        {
            Process[] steamProcesses = Process.GetProcessesByName("steam");
            s_logger.Debug("Found {ProcessCount} Steam processes", steamProcesses.Length);

            foreach (Process process in steamProcesses)
            {
                try
                {
                    if (!process.Responding)
                    {
                        s_logger.Debug("Killing unresponsive Steam process: {Id}", process.Id);
                        process.Kill();
                        continue;
                    }

                    if (process.CloseMainWindow())
                    {
                        s_logger.Debug("Sent close signal to Steam process: {Id}", process.Id);
                        process.WaitForExit(3000);
                    }

                    if (!process.HasExited)
                    {
                        s_logger.Debug("Forcibly killing Steam process: {Id}", process.Id);
                        process.Kill();
                        process.WaitForExit(1000);
                    }
                    
                    s_logger.Debug("Successfully closed Steam process: {Id}", process.Id);
                }
                catch (Exception ex)
                {
                    s_logger.Warn(ex, "Failed to close Steam process: {Id}", process.Id);
                }
            }
            
            s_logger.Info("Steam closure completed");
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, "Failed to close Steam processes");
            throw;
        }
    }

    private string ResolveSteamPath()
    {
        var path = m_configurationService.Config.CustomSteamPath ?? GetSteamPath();
        s_logger.Debug("Resolved Steam path: {Path}", path);
        return path;
    }

    private string ResolveLocalConfigPath()
    {
        if (CurrentUser is null)
        {
            s_logger.Error("No user selected when resolving local config path");
            throw new InvalidOperationException("No user selected");
        }
        
        var path = FindLocalConfig(CurrentUser);
        s_logger.Debug("Resolved local config path: {Path}", path);
        return path;
    }

    private IReadOnlyList<string> FindAllLocalConfigs(SteamUser? user = null)
    {
        var userDataPath = Path.Combine(SteamPath, "userdata");
        if (user is not null) userDataPath = Path.Combine(userDataPath, user.Id);

        s_logger.Debug("Searching for local configs in: {Path}", userDataPath);

        if (!Directory.Exists(userDataPath))
        {
            s_logger.Error("Steam userdata directory not found: {Path}", userDataPath);
            throw new DirectoryNotFoundException($"Steam userdata directory not found: {userDataPath}");
        }

        var configFiles = Directory.GetFiles(userDataPath, "localconfig.vdf", SearchOption.AllDirectories);

        if (configFiles.Length == 0)
        {
            s_logger.Error("No localconfig.vdf files found in: {Path}", userDataPath);
            throw new FileNotFoundException("No localconfig.vdf files found");
        }

        s_logger.Debug("Found {Count} local config files", configFiles.Length);
        return configFiles;
    }

    private string FindLocalConfig(SteamUser user)
    {
        var configs = FindAllLocalConfigs(user);
        var config = configs[0];
        s_logger.Debug("Selected local config for user {UserId}: {Config}", user.Id, config);
        return config;
    }

    private string? UpdateLaunchOptionsInContent(string content, string appId, string launchOptions)
    {
        try
        {
            var parsedContent = SteamVdfParser.Parse(content);
            var gameEntry = FindEntry(parsedContent, appId) as IDictionary<string, object>;

            if (gameEntry is null)
            {
                s_logger.Warn("Game entry not found for app: {AppId}", appId);
                return null;
            }

            var replaced = ReplaceForFirstKey(gameEntry, "LaunchOptions", $"\"{SteamVdfParser.EscapeVdfString(launchOptions)}\"");
            
            if (!replaced)
            {
                s_logger.Warn("LaunchOptions key not found for app: {AppId}", appId);
                return null;
            }

            var updatedContent = SteamVdfParser.ToString(parsedContent);
            s_logger.Debug("Successfully updated launch options in content for app: {AppId}", appId);
            return updatedContent;
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, "Failed to update launch options in content for app: {AppId}", appId);
            return null;
        }
    }

    private string? ExtractLaunchOptionsFromContent(string content, string appId)
    {
        try
        {
            var parsedContent = SteamVdfParser.Parse(content);
            var gameEntry = FindEntry(parsedContent, appId) as IDictionary<string, object>;

            if (gameEntry is null)
            {
                s_logger.Debug("Game entry not found for app: {AppId}", appId);
                return null;
            }

            var options = FindEntry(gameEntry, "LaunchOptions") as string;
            if (options is null)
            {
                s_logger.Debug("LaunchOptions not found for app: {AppId}", appId);
                return null;
            }

            options = options.Trim('\"');
            var unescapedOptions = SteamVdfParser.UnescapeVdfString(options);
            s_logger.Debug("Extracted launch options for app {AppId}: {Options}", appId, unescapedOptions);
            return unescapedOptions;
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, "Failed to extract launch options from content for app: {AppId}", appId);
            return null;
        }
    }

    private object? FindEntry(IDictionary<string, object> content, string key)
    {
        if (content.TryGetValue(key, out object? entry))
            return entry;

        foreach (var subContent in content.Values.OfType<IDictionary<string, object>>())
            if (FindEntry(subContent, key) is { } foundedEntry)
                return foundedEntry;

        s_logger.Trace("Entry not found for key: {Key}", key);
        return null;
    }

    private bool ReplaceForFirstKey(IDictionary<string, object> content, string key, string value)
    {
        if (content.ContainsKey(key))
        {
            content[key] = value;
            s_logger.Trace("Replaced value for key: {Key}", key);
            return true;
        }

        foreach (var subContent in content.Values.OfType<IDictionary<string, object>>())
            if (ReplaceForFirstKey(subContent, key, value))
                return true;

        s_logger.Trace("Key not found for replacement: {Key}", key);
        return false;
    }

    private static string GetSteamPath()
    {
        s_logger.Debug("Resolving Steam installation path");

        var registryPath = GetSteamPathFromRegistry();
        if (!string.IsNullOrEmpty(registryPath) && Directory.Exists(registryPath))
        {
            s_logger.Debug("Using Steam path from registry: {Path}", registryPath);
            return registryPath;
        }

        var envPath = GetSteamPathFromEnvironment();
        if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
        {
            s_logger.Debug("Using Steam path from environment: {Path}", envPath);
            return envPath;
        }

        var defaultPaths = GetDefaultSteamPaths();
        foreach (var path in defaultPaths)
        {
            if (Directory.Exists(path))
            {
                s_logger.Debug("Using default Steam path: {Path}", path);
                return path;
            }
        }

        s_logger.Warn("Using fallback Steam path");
        return "C:\\Program Files (x86)\\Steam";
    }

    private static string? GetSteamPathFromRegistry()
    {
        try
        {
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
            {
                var path = key?.GetValue("InstallPath") as string;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    return path;
            }

            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
            {
                var path = key?.GetValue("InstallPath") as string;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    return path;
            }

            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam"))
            {
                var path = key?.GetValue("SteamPath") as string;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    return Path.GetFullPath(path);
            }
        }
        catch (Exception ex)
        {
            s_logger.Warn(ex, "Failed to read Steam path from registry");
        }

        return null;
    }

    private static string? GetSteamPathFromEnvironment()
    {
        var steamPath = Environment.GetEnvironmentVariable("STEAM_PATH");
        if (!string.IsNullOrEmpty(steamPath) && Directory.Exists(steamPath))
            return steamPath;

        var programFiles = Environment.GetEnvironmentVariable("ProgramFiles(x86)")
            ?? Environment.GetEnvironmentVariable("ProgramFiles");

        if (!string.IsNullOrEmpty(programFiles))
        {
            var defaultSteamPath = Path.Combine(programFiles, "Steam");
            if (Directory.Exists(defaultSteamPath))
                return defaultSteamPath;
        }

        return null;
    }

    private static string[] GetDefaultSteamPaths()
    {
        var drives = DriveInfo.GetDrives();
        var paths = new List<string>();

        foreach (var drive in drives)
        {
            if (drive.DriveType == DriveType.Fixed)
            {
                paths.Add(Path.Combine(drive.Name, "Program Files", "Steam"));
                paths.Add(Path.Combine(drive.Name, "Program Files (x86)", "Steam"));
                paths.Add(Path.Combine(drive.Name, "Games", "Steam"));
                paths.Add(Path.Combine(drive.Name, "Steam"));
                paths.Add(Path.Combine(drive.Name, "Portable Steam", "Steam"));
            }
        }

        s_logger.Trace("Generated {Count} default Steam paths", paths.Count);
        return paths.ToArray();
    }
}