using System.Configuration;
using System.Data;
using System.Windows;
using RPOverlay.WPF.Utilities;

namespace RPOverlay.WPF
{
    public partial class App : System.Windows.Application
    {
        private System.Windows.Forms.NotifyIcon? _notifyIcon;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            var version = VersionHelper.GetVersion();
            
            // Create system tray icon with custom medical cross icon
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = IconHelper.CreateMedicalCrossIcon(),
                Visible = true,
                Text = $"RP Overlay v{version}"
            };

            // Create context menu
            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            contextMenu.Items.Add("Visa/Dölj (F9)", null, (s, args) => 
            {
                if (MainWindow is MainWindow mw)
                {
                    mw.ToggleOverlayFromTray();
                }
            });
            contextMenu.Items.Add("Inställningar", null, (s, args) => 
            {
                Dispatcher.Invoke(() =>
                {
                    if (MainWindow is MainWindow mw)
                    {
                        mw.OpenSettingsWindow();
                    }
                });
            });
            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            contextMenu.Items.Add("Avsluta", null, (s, args) => 
            {
                Shutdown();
            });

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, args) =>
            {
                if (MainWindow is MainWindow mw)
                {
                    mw.ToggleOverlayFromTray();
                }
            };

            // MainWindow is set in App.xaml StartupUri, so it will be created automatically
            MainWindow?.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            base.OnExit(e);
        }
    }
}
