namespace StardewModManager.AvaloniaImpl.ViewModels;

using ReactiveUI;

public class ViewModelBase : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();
}