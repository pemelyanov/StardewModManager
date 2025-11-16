namespace StardewModManager.AvaloniaImpl.Views.Mods;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using ReactiveUI;

[SingleInstanceView]
public partial class ModsPage : ReactiveUserControl<ModsPageViewModel>
{
    public ModsPage()
    {
        InitializeComponent();
    }
    
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        
        if(ViewModel is null) return;
        
        ViewModel.OpenModPackAction = SelectModPackAction;
        ViewModel.SaveModPackAction = SaveModPackAction;
    }

    private async Task<string?> SaveModPackAction()
    {
        var zipFileType = new FilePickerFileType("ZIP Archives")
        {
            Patterns = new[] { "*.zip" },
            MimeTypes = new[] { "application/zip" }
        };

        var allFilesType = new FilePickerFileType("All files")
        {
            Patterns = new[] { "*.*" }
        };
        
        var fileDialogOptions = new FilePickerSaveOptions
        {
            Title = "Выберите ZIP архив",
            FileTypeChoices = new[] { zipFileType, allFilesType }
        };

        var topLevel = TopLevel.GetTopLevel(this)!;
        
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(fileDialogOptions);
        
        return file?.Path.LocalPath;
    }

    private async Task<string?> SelectModPackAction()
    {
        var zipFileType = new FilePickerFileType("ZIP Archives")
        {
            Patterns = new[] { "*.zip" },
            MimeTypes = new[] { "application/zip" }
        };

        var allFilesType = new FilePickerFileType("All files")
        {
            Patterns = new[] { "*.*" }
        };
        
        var fileDialogOptions = new FilePickerOpenOptions
        {
            Title = "Выберите ZIP архив",
            FileTypeFilter = new[] { zipFileType, allFilesType },
            AllowMultiple = false
        };

        var topLevel = TopLevel.GetTopLevel(this)!;
        
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(fileDialogOptions);
        
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }
}