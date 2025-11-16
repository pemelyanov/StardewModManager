namespace StardewModManager.Core.Services.SteamManager;

using Data;

public interface ISteamManager
{
    public string SteamPath { get; }
    
    public SteamUser? CurrentUser { get; set; }

    public IReadOnlyList<SteamUser> GetLocalUsersList();

    public void SetCustomSteamPath(string? path);

    public void SetLaunchOptions(string appId, string launchOptions);

    public string? GetLaunchOptions(string appId);

    public void LaunchSteamGame(string appId);

    public void CloseSteam();
}