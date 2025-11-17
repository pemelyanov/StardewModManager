namespace StardewModManager.Core.Services.ModManager;

using Data;

public interface IModManger
{
    string StardewPath { get; }
    
    IObservable<bool> IsEnabled { get; }
    
    IObservable<bool>  IsInstalled { get; }
    
    // TODO: Сделать реактивным
    IReadOnlyList<Mod> Mods { get; }
    
    // TODO: Сделать реактивным
    IReadOnlyList<ModPackInfo> RecentModPacks { get; }
    
    Task InstallLatestAsync(IObserver<LoadingProgress>? observer);

    void SetCustomStardewPath(string? path);

    void ToggleIsEnabled();

    Task ExportToModPackAsync(string path);

    Task InstallModPackAsync(string path);

    void ToggleMod(Mod mod);

    void DeleteRecentMod(ModPackInfo modPackInfo);
}