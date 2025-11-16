namespace StardewModManager.Core.Data;

public record ModManagerConfig
{
    public string? CustomSteamPath { get; init; }
    
    public string? CustomStardewPath { get; init; }
}