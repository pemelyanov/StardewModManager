namespace StardewModManager.AvaloniaImpl.ViewModels;

using Core.Data;

public class RecentModPackViewModel(ModPackInfo info)
{
    public string Name { get; } = Path.GetFileNameWithoutExtension(info.Path);

    public bool IsExists { get; } = File.Exists(info.Path);

    public ModPackInfo Info => info;
}