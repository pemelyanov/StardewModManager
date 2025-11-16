namespace StardewModManager.AvaloniaImpl.Views.Main;

using System.Threading.Tasks;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        
        if(ViewModel is null) return;
        
        ViewModel.OpenModPackAction = SelectModPackAction;
        ViewModel.SaveModPackAction = SaveModPackAction;
        ViewModel.SelectStardewFolderAction = SelectStardewFolderAction;
    }

    private async Task<string?> SaveModPackAction()
    {
        // Создаем фильтры для файлов
        var zipFileType = new FilePickerFileType("ZIP Archives")
        {
            Patterns = new[] { "*.zip" },
            MimeTypes = new[] { "application/zip" }
        };

        var allFilesType = new FilePickerFileType("All files")
        {
            Patterns = new[] { "*.*" }
        };

        // Настраиваем опции диалога
        var fileDialogOptions = new FilePickerSaveOptions
        {
            Title = "Выберите ZIP архив",
            FileTypeChoices = new[] { zipFileType, allFilesType }
        };

        // Открываем диалог выбора файла
        var file = await StorageProvider.SaveFilePickerAsync(fileDialogOptions);

        // Возвращаем путь к выбранному файлу или null, если файл не выбран
        return file?.Path.LocalPath;
    }

    private async Task<string?> SelectStardewFolderAction()
    {
        var options = new FolderPickerOpenOptions
        {
            AllowMultiple = false,
        };

        var folders = await StorageProvider.OpenFolderPickerAsync(options);
        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    private async Task<string?> SelectModPackAction()
    {
        // Создаем фильтры для файлов
        var zipFileType = new FilePickerFileType("ZIP Archives")
        {
            Patterns = new[] { "*.zip" },
            MimeTypes = new[] { "application/zip" }
        };

        var allFilesType = new FilePickerFileType("All files")
        {
            Patterns = new[] { "*.*" }
        };

        // Настраиваем опции диалога
        var fileDialogOptions = new FilePickerOpenOptions
        {
            Title = "Выберите ZIP архив",
            FileTypeFilter = new[] { zipFileType, allFilesType },
            AllowMultiple = false
        };

        // Открываем диалог выбора файла
        var files = await StorageProvider.OpenFilePickerAsync(fileDialogOptions);

        // Возвращаем путь к выбранному файлу или null, если файл не выбран
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }
}