namespace StardewModManager.Core.Services.ModManager;

using StardewModManager.Core.Data;

public interface IModManger
{
    string StardewPath { get; }
    
    Task InstallLatestAsync(IObserver<LoadingProgress>? observer);

    void SetCustomStardewPath(string? path);
}