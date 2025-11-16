namespace StardewModManager.Core.Services.SMAPIManager;

using Data;

public interface IModManger
{
    string StardewPath { get; }
    
    Task InstallLatestAsync(IObserver<LoadingProgress>? observer);

    void SetCustomStardewPath(string path);
}