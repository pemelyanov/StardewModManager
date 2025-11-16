namespace StardewModManager.Core.Data;

public record SteamUser
{
    public required string Id { get; init; }
    
    public required string NickName { get; init; }

    public override string ToString() => $"{NickName} ({Id})";
}