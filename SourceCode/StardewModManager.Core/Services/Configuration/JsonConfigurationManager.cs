namespace StardewModManager.Core.Services.Configuration;

using System.Text.Json;
using Data;
using NLog;

public class JsonModManagerConfigurationService : IConfigurationService<ModManagerConfig>
{
    // ReSharper disable once StaticMemberInGenericType
    private static readonly ILogger s_logger = LogManager.GetCurrentClassLogger();

    private readonly string m_configFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StardewModManager",
        "modmanagerconfig.json"
    );

    private readonly JsonSerializerOptions m_jsonOptions;
    private          ModManagerConfig      m_config;

    public JsonModManagerConfigurationService()
    {
        m_jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        m_config = LoadConfig();
    }

    public ModManagerConfig Config => m_config;

    public void UpdateConfig(ModManagerConfig config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        try
        {
            SaveConfig(config);
            m_config = config;
            s_logger.Info("Configuration updated successfully: {ConfigPath}", m_configFilePath);
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, "Failed to update configuration: {ConfigPath}", m_configFilePath);
            throw;
        }
    }

    private ModManagerConfig LoadConfig()
    {
        try
        {
            if (!File.Exists(m_configFilePath))
            {
                s_logger.Warn("Config file not found, creating default configuration: {ConfigPath}", m_configFilePath);
                var defaultConfig = new ModManagerConfig();
                SaveConfig(defaultConfig);
                return defaultConfig;
            }

            var json = File.ReadAllText(m_configFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                s_logger.Warn("Config file is empty, using default configuration: {ConfigPath}", m_configFilePath);
                return new ModManagerConfig();
            }

            var config = JsonSerializer.Deserialize<ModManagerConfig>(json, m_jsonOptions);
            if (config == null)
            {
                s_logger.Warn(
                    "Failed to deserialize config, using default configuration: {ConfigPath}",
                    m_configFilePath
                );
                return new ModManagerConfig();
            }

            s_logger.Info("Configuration loaded successfully: {ConfigPath}", m_configFilePath);
            return config;
        }
        catch (JsonException ex)
        {
            s_logger.Error(
                ex,
                "JSON deserialization error, using default configuration: {ConfigPath}",
                m_configFilePath
            );
            return new ModManagerConfig();
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, "Failed to load configuration: {ConfigPath}", m_configFilePath);
            return new ModManagerConfig();
        }
    }

    private void SaveConfig(ModManagerConfig config)
    {
        try
        {
            var directory = Path.GetDirectoryName(m_configFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                s_logger.Debug("Created config directory: {Directory}", directory);
            }

            var json = JsonSerializer.Serialize(config, m_jsonOptions);
            File.WriteAllText(m_configFilePath, json);
            s_logger.Debug("Configuration saved successfully: {ConfigPath}", m_configFilePath);
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, "Failed to save configuration: {ConfigPath}", m_configFilePath);
            throw;
        }
    }
}