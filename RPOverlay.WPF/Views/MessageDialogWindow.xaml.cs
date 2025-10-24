using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using MouseClickOverrideManager = RPOverlay.WPF.Utilities.MouseClickOverrideManager;

namespace RPOverlay.WPF.Views;

public partial class MessageDialogWindow : Window
{
    private readonly MessageBoxButton _buttons;

    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

    public MessageDialogWindow(string message, string title, MessageBoxButton buttons, MessageBoxImage icon)
    {
        InitializeComponent();
        MouseClickOverrideManager.Register(this);

        _buttons = buttons;
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;

        ConfigureIcon(icon);
        ConfigureButtons(buttons);
    }

    private void ConfigureIcon(MessageBoxImage icon)
    {
        (string? glyph, SolidColorBrush? brush) = icon switch
        {
            MessageBoxImage.Information => ("\u2139", new SolidColorBrush(Color.FromRgb(47, 157, 255))),
            MessageBoxImage.Warning => ("\u26A0", new SolidColorBrush(Color.FromRgb(255, 191, 0))),
            MessageBoxImage.Error => ("\u26D4", new SolidColorBrush(Color.FromRgb(255, 85, 85))),
            MessageBoxImage.Question => ("?", new SolidColorBrush(Color.FromRgb(47, 157, 255))),
            _ => (null, null)
        };

        if (glyph == null)
        {
            IconText.Visibility = Visibility.Collapsed;
            return;
        }

        IconText.Text = glyph;
        IconText.Visibility = Visibility.Visible;
        if (brush != null)
        {
            IconText.Foreground = brush;
        }
    }

    private void ConfigureButtons(MessageBoxButton buttons)
    {
        ButtonsPanel.Children.Clear();

        switch (buttons)
        {
            case MessageBoxButton.OK:
                AddButton("OK", MessageBoxResult.OK, isDefault: true, isCancel: true);
                break;
            case MessageBoxButton.OKCancel:
                AddButton("OK", MessageBoxResult.OK, isDefault: true);
                AddButton("Avbryt", MessageBoxResult.Cancel, isCancel: true);
                break;
            case MessageBoxButton.YesNo:
                AddButton("Ja", MessageBoxResult.Yes, isDefault: true);
                AddButton("Nej", MessageBoxResult.No, isCancel: true);
                break;
            case MessageBoxButton.YesNoCancel:
                AddButton("Ja", MessageBoxResult.Yes, isDefault: true);
                AddButton("Nej", MessageBoxResult.No);
                AddButton("Avbryt", MessageBoxResult.Cancel, isCancel: true);
                break;
            default:
                AddButton("OK", MessageBoxResult.OK, isDefault: true, isCancel: true);
                break;
        }

        var defaultButton = ButtonsPanel.Children.OfType<System.Windows.Controls.Button>().FirstOrDefault(b => b.IsDefault)
                           ?? ButtonsPanel.Children.OfType<System.Windows.Controls.Button>().FirstOrDefault();
        defaultButton?.Focus();
    }

    private void AddButton(string content, MessageBoxResult result, bool isDefault = false, bool isCancel = false)
    {
        var button = new System.Windows.Controls.Button
        {
            Content = content,
            MinWidth = 80,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(12, 6, 12, 6),
            IsDefault = isDefault,
            IsCancel = isCancel
        };

        button.Click += (_, _) => CloseWithResult(result);
        ButtonsPanel.Children.Add(button);
    }

    private void CloseWithResult(MessageBoxResult result)
    {
        Result = result;
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            DragMove();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (Result == MessageBoxResult.None)
        {
            Result = _buttons switch
            {
                MessageBoxButton.OK => MessageBoxResult.OK,
                MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
                MessageBoxButton.YesNo => MessageBoxResult.No,
                MessageBoxButton.YesNoCancel => MessageBoxResult.Cancel,
                _ => MessageBoxResult.None
            };
        }

        base.OnClosing(e);
    }
}
