namespace StardewModManager.Core.Data;

public record ModPackInfo
{
    public required string Path { get; init; }
    
    public DateTime LastInstallTime { get; init; }
}