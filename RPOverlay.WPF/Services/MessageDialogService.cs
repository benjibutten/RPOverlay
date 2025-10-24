using System.Linq;
using System.Windows;
using RPOverlay.WPF.Views;
using Application = System.Windows.Application;

namespace RPOverlay.WPF.Services;

internal static class MessageDialogService
{
    public static MessageBoxResult Show(string message) =>
        Show(message, "RP Overlay", MessageBoxButton.OK, MessageBoxImage.None);

    public static MessageBoxResult Show(string message, string caption) =>
        Show(message, caption, MessageBoxButton.OK, MessageBoxImage.None);

    public static MessageBoxResult Show(string message, string caption, MessageBoxButton buttons) =>
        Show(message, caption, buttons, MessageBoxImage.None);

    public static MessageBoxResult Show(string message, string caption, MessageBoxButton buttons, MessageBoxImage image)
    {
        var dialog = new MessageDialogWindow(message, caption, buttons, image);
        var owner = GetActiveOwner();

        if (owner != null)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        dialog.ShowDialog();
        return dialog.Result;
    }

    private static Window? GetActiveOwner()
    {
        if (Application.Current == null)
        {
            return null;
        }

        var activeWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        if (activeWindow != null)
        {
            return activeWindow;
        }

        return Application.Current.MainWindow;
    }
}
