namespace StardewModManager.AvaloniaImpl.Views.Main;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.ReactiveUI;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
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
        if(sender is not Control { Tag: Type type }) return;
        
        ViewModel?.SelectPage(type);
    }
}