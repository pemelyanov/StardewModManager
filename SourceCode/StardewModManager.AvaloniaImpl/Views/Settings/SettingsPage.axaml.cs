namespace StardewModManager.AvaloniaImpl.Views.Settings;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using ReactiveUI;

[SingleInstanceView]
public partial class SettingsPage : ReactiveUserControl<SettingsPageViewModel>
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (ViewModel is null) return;

        ViewModel.SelectFolderAction = SelectFolderAction;
    }

    private async Task<string?> SelectFolderAction()
    {
        var options = new FolderPickerOpenOptions
        {
            AllowMultiple = false,
        };

        var topLevel = TopLevel.GetTopLevel(this)!;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }
}