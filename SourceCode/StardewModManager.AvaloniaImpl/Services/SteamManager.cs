namespace StardewModManager.AvaloniaImpl.Services;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Core.Data;
using Microsoft.Win32;

public class SteamManager
{
    public SteamManager(string? steamPath = null)
    {
        SteamPath = steamPath ?? GetSteamPath() ?? "C:\\Program Files (x86)\\Steam";
    }
    
    public string SteamPath { get; }
    
    public SteamUser? CurrentUser { get; set; }

    public static SteamManager Instance { get; } = new();

    public IReadOnlyList<SteamUser> GetLocalUsersList()
    {
        var localConfigs = FindAllLocalConfigs();

        return localConfigs.Select(
                confing =>
                {
                    var parser = new SteamVdfParser();

                    var content = parser.Parse(File.ReadAllText(confing));

                    var friendsNode = FindEntry(content, "friends") as OrderedDictionary<string, object>;

                    if (friendsNode is null) return null;
                    var id = friendsNode.First().Key;
                    var nickName = FindEntry(friendsNode, "PersonaName") as string;
                    nickName = nickName?.Trim('\"');

                    return string.IsNullOrEmpty(id) || string.IsNullOrEmpty(nickName)
                        ? null
                        : new SteamUser()
                        {
                            Id = id,
                            NickName = nickName
                        };
                }
            )
            .Where(it => it is not null)
            .Select(it => it!)
            .ToArray();
    }

    public void SetLaunchOptions(string appId, string launchOptions)
    {
        if (string.IsNullOrEmpty(appId))
            throw new ArgumentException("App ID cannot be null or empty", nameof(appId));

        var localConfigPath = ResolveLocalConfigPath();
        
        if (!File.Exists(localConfigPath))
            throw new FileNotFoundException($"Config file not found: {localConfigPath}");

        var content = File.ReadAllText(localConfigPath);
        var updatedContent = UpdateLaunchOptionsInContent(content, appId, launchOptions);

        if (string.IsNullOrEmpty(updatedContent)) return;

        File.WriteAllText(localConfigPath, updatedContent);
    }

    public string? GetLaunchOptions(string appId)
    {
        if (string.IsNullOrEmpty(appId))
            throw new ArgumentException("App ID cannot be null or empty", nameof(appId));

        var localConfigPath = ResolveLocalConfigPath();
        
        if (!File.Exists(localConfigPath))
            throw new FileNotFoundException($"Config file not found: {localConfigPath}");

        var content = File.ReadAllText(localConfigPath);
        return ExtractLaunchOptionsFromContent(content, appId);
    }

    // Utility method to get installed games
    public Dictionary<string, string> GetInstalledGames()
    {
        var games = new Dictionary<string, string>();
        var steamAppsPath = Path.Combine(SteamPath, "steamapps");

        if (!Directory.Exists(steamAppsPath))
            return games;

        // Read appmanifest files to get installed games
        var manifestFiles = Directory.GetFiles(steamAppsPath, "appmanifest_*.acf");

        foreach (var manifestFile in manifestFiles)
        {
            try
            {
                var content = File.ReadAllText(manifestFile);
                var appIdMatch = Regex.Match(content, @"\""appid\""\s*\""(\d+)\""");
                var nameMatch = Regex.Match(content, @"\""name\""\s*\""(.*?)\""");

                if (appIdMatch.Success && nameMatch.Success)
                {
                    games[appIdMatch.Groups[1].Value] = nameMatch.Groups[1].Value;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading manifest {manifestFile}: {ex.Message}");
            }
        }

        return games;
    }

    private string ResolveLocalConfigPath()
    {
        if (CurrentUser is null) throw new InvalidOperationException("No user selected");

        return FindLocalConfig(CurrentUser);
    }

    private IReadOnlyList<string> FindAllLocalConfigs(SteamUser? user = null)
    {
        var userDataPath = Path.Combine(SteamPath, "userdata");
        if (user is not null) userDataPath = Path.Combine(userDataPath, user.Id);

        if (!Directory.Exists(userDataPath))
            throw new DirectoryNotFoundException($"Steam userdata directory not found: {userDataPath}");

        var configFiles = Directory.GetFiles(userDataPath, "localconfig.vdf", SearchOption.AllDirectories);

        if (configFiles.Length == 0)
            throw new FileNotFoundException("No localconfig.vdf files found");

        return configFiles;
    }

    private string FindLocalConfig(SteamUser user)
    {
        return FindAllLocalConfigs(user)[0];
    }

    private string? UpdateLaunchOptionsInContent(string content, string appId, string launchOptions)
    {
        var parser = new SteamVdfParser();
        var parsedContent = parser.Parse(content);

        var gameEntry = FindEntry(parsedContent, appId) as IDictionary<string, object>;

        if (gameEntry is null) return null;

        ReplaceForFirstKey(gameEntry, "LaunchOptions", $"\"{EscapeVdfString(launchOptions)}\"");

        var sb = new StringBuilder();
        parser.ToString(sb, parsedContent);
        return sb.ToString();
    }

    private string? ExtractLaunchOptionsFromContent(string content, string appId)
    {
        var parser = new SteamVdfParser();
        var parsedContent = parser.Parse(content);

        var gameEntry = FindEntry(parsedContent, appId) as IDictionary<string, object>;

        if (gameEntry is null) return null;

        var options = FindEntry(gameEntry, "LaunchOptions") as string;

        if (options is null) return null;

        options = options.Trim('\"');

        return UnescapeVdfString(options);
    }

    private string EscapeVdfString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Basic VDF escaping - you might need to extend this
        return input.Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }

    private string UnescapeVdfString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Basic VDF unescaping
        return input.Replace("\\\\", "\\")
            .Replace("\\\"", "\"")
            .Replace("\\n", "\n")
            .Replace("\\t", "\t");
    }

    private object? FindEntry(IDictionary<string, object> content, string key)
    {
        if (content.TryGetValue(key, out object? entry))
            return entry;

        foreach (var subContent in content.Values.OfType<IDictionary<string, object>>())
            if (FindEntry(subContent, key) is { } foundedEntry)
                return foundedEntry;

        return null;
    }

    private bool ReplaceForFirstKey(IDictionary<string, object> content, string key, string value)
    {
        if (content.ContainsKey(key))
        {
            content[key] = value;
            return true;
        }

        foreach (var subContent in content.Values.OfType<IDictionary<string, object>>())
            if (ReplaceForFirstKey(subContent, key, value))
                return true;

        return false;
    }

    public static string? GetSteamPath()
    {
        // 1. Попробовать из реестра (основной способ)
        var registryPath = GetSteamPathFromRegistry();
        if (!string.IsNullOrEmpty(registryPath) && Directory.Exists(registryPath))
            return registryPath;

        // 2. Попробовать из переменных окружения
        var envPath = GetSteamPathFromEnvironment();
        if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
            return envPath;

        // 3. Стандартные пути по умолчанию
        var defaultPaths = GetDefaultSteamPaths();
        foreach (var path in defaultPaths)
        {
            if (Directory.Exists(path))
                return path;
        }

        return null;
    }
    
    public void LaunchSteamGame(string appId)
    {
        string steamUri = $"steam://rungameid/{appId}";
        Process.Start(new ProcessStartInfo
        {
            FileName = steamUri,
            UseShellExecute = true
        });
    }
    
    public void SafeCloseSteam()
    {
        try
        {
            Process[] steamProcesses = Process.GetProcessesByName("steam");
        
            foreach (Process process in steamProcesses)
            {
                // Проверяем, что процесс отвечает
                if (!process.Responding)
                {
                    process.Kill();
                    continue;
                }
            
                // Пытаемся закрыть через главное окно
                if (process.CloseMainWindow())
                {
                    process.WaitForExit(3000); // Ждем 3 секунды
                }
            
                // Если процесс все еще работает, завершаем принудительно
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(1000);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
    }


    private static string GetSteamPathFromRegistry()
    {
        try
        {
            // 64-битные системы
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
            {
                var path = key?.GetValue("InstallPath") as string;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    return path;
            }

            // 32-битные системы
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
            {
                var path = key?.GetValue("InstallPath") as string;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    return path;
            }

            // Текущий пользователь
            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam"))
            {
                var path = key?.GetValue("SteamPath") as string;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    return Path.GetFullPath(path);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка чтения реестра: {ex.Message}");
        }

        return null;
    }

    private static string GetSteamPathFromEnvironment()
    {
        // Переменная окружения STEAM_PATH (пользовательская)
        var steamPath = Environment.GetEnvironmentVariable("STEAM_PATH");
        if (!string.IsNullOrEmpty(steamPath) && Directory.Exists(steamPath))
            return steamPath;

        // Program Files
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
        var paths = new System.Collections.Generic.List<string>();

        foreach (var drive in drives)
        {
            if (drive.DriveType == DriveType.Fixed)
            {
                // Обычные пути установки
                paths.Add(Path.Combine(drive.Name, "Program Files", "Steam"));
                paths.Add(Path.Combine(drive.Name, "Program Files (x86)", "Steam"));
                paths.Add(Path.Combine(drive.Name, "Games", "Steam"));
                paths.Add(Path.Combine(drive.Name, "Steam"));

                // Портативная установка
                paths.Add(Path.Combine(drive.Name, "Portable Steam", "Steam"));
            }
        }

        return paths.ToArray();
    }
}

class SteamVdfParser
{
    public OrderedDictionary<string, object> Parse(string content)
    {
        var reader = new StringReader(content);
        return ParseObject(reader);
    }

    private OrderedDictionary<string, object> ParseObject(StringReader reader)
    {
        var result = new OrderedDictionary<string, object>();
        string line;

        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();

            if (string.IsNullOrEmpty(line))
                continue;

            // Если встречаем закрывающую скобку, выходим из текущего объекта
            if (line == "}")
                break;

            // Извлекаем ключ (убираем кавычки если есть)
            line = TrimLine(line);

            // if (key == "413150")
            // {
            //     
            // }

            var keyValuePair = ExtractKeyValue(line);

            if (keyValuePair.Length == 1)
            {
                var nextLine = reader.ReadLine()?.Trim();
                if (nextLine == "{")
                {
                    // Если следующая строка открывающая скобка, значит это вложенный объект
                    var nestedObject = ParseObject(reader);
                    result[keyValuePair[0]] = nestedObject;
                    continue;
                }
                else { }
            }

            result[keyValuePair[0]] = keyValuePair[1];
        }

        return result;
    }

    private string TrimLine(string line)
    {
        // Убираем кавычки и табуляцию
        return line.Trim();
    }

    private string[] ExtractKeyValue(string line)
    {
        if (string.IsNullOrEmpty(line))
            return [];

        // Разделяем строку по табуляции чтобы извлечь значение
        var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            // Убираем кавычки из значения
            string value = parts[^1].Trim();
            return [parts[0].Trim('\"'), value];
        }

        return [parts[0].Trim('\"')];
    }

    public void ToString(StringBuilder builder, IDictionary<string, object> dict, int indent = 0)
    {
        const char indentChar = '\t';
        string indentStr = new string(indentChar, indent);

        foreach (var kvp in dict)
        {
            if (kvp.Value is IDictionary<string, object> nestedDict)
            {
                builder.AppendLine($"{indentStr}\"{kvp.Key}\"");
                builder.AppendLine($"{indentStr}{{");
                ToString(builder, nestedDict, indent + 1);
                builder.AppendLine($"{indentStr}}}");
            }
            else
            {
                builder.AppendLine($"{indentStr}\"{kvp.Key}\"{indentChar}{indentChar}{kvp.Value}");
            }
        }
    }
}