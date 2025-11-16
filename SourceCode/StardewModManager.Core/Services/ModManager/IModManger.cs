namespace StardewModManager.Core.Services.ModManager;

using Data;

public interface IModManger
{
    string StardewPath { get; }
    
    bool IsEnabled { get; }
    
    bool IsInstalled { get; }
    
    IReadOnlyList<Mod> Mods { get; }
    
    Task InstallLatestAsync(IObserver<LoadingProgress>? observer);

    void SetCustomStardewPath(string? path);

    void ToggleIsEnabled();

    Task ExportToModPackAsync(string path);

    Task InstallModPackAsync(string path);

    void ToggleMod(Mod mod);
}