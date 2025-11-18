namespace StardewModManager.Core.Services.Dialog;

public interface IDialogService
{
    Task NotifyAsync(string message, string? title = null, string? buttonText = null);
    
    Task<bool> ConfirmAsync(string message, string? title = null, string? acceptText = null, string? cancelText = null);
}