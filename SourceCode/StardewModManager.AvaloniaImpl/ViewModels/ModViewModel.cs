namespace StardewModManager.AvaloniaImpl.ViewModels;

using ReactiveUI.Fody.Helpers;

public class ModViewModel(string name) : ViewModelBase
{
    public string Name => name;
    
    [Reactive]
    public bool IsEnabled { get; set; }
}