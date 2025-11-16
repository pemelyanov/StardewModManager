namespace StardewModManager.Core.Data;

public class Mod(string name)
{
    public string Name => name;
    
    public bool IsEnabled { get; set; }
}