namespace StardewModManager.Core.Services.Configuration;

public interface IConfigurationService<TConfig>
{
    TConfig Config { get; }

    void UpdateConfig(TConfig config);
}