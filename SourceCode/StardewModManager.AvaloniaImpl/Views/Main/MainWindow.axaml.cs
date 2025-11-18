namespace StardewModManager.AvaloniaImpl.Views.Main;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using Core.Services.Dialog;
using FluentAvalonia.UI.Controls;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>, IDialogService
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void InputElement_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void NavigationItem_OnTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { Tag: Type type }) return;

        ViewModel?.SelectPage(type);
    }

    public Task NotifyAsync(
        string message,
        string? title = null,
        string? buttonText = null
    ) => new ContentDialog
    {
        Title = title,
        Content = message,
        PrimaryButtonText = buttonText ?? "Ок",
        DefaultButton = ContentDialogButton.Primary
    }.ShowAsync(this);

    public async Task<bool> ConfirmAsync(
        string message,
        string? title = null,
        string? acceptText = null,
        string? cancelText = null
    ) => await new ContentDialog
    {
        Title = title,
        Content = message,
        PrimaryButtonText = acceptText ?? "Продолжить",
        CloseButtonText = cancelText ?? "Отмена",
        DefaultButton = ContentDialogButton.Primary
    }.ShowAsync(this) == ContentDialogResult.Primary;
}