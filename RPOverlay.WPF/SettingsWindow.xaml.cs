using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using RPOverlay.Core.Models;
using RPOverlay.Core.Services;
using RPOverlay.Core.Providers;

using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace RPOverlay.WPF
{
    public partial class SettingsWindow : Window, INotifyPropertyChanged
    {
        private readonly OverlayConfigService _configService;
        private ObservableCollection<OverlayButton> _commands;

        public SettingsWindow()
        {
            InitializeComponent();
            DataContext = this;

            _configService = new OverlayConfigService(new AppDataOverlayConfigPathProvider());
            
            // Load existing commands
            _commands = new ObservableCollection<OverlayButton>(
                _configService.Current.Buttons.Select(b => new OverlayButton 
                { 
                    Label = b.Label, 
                    Text = b.Text 
                }));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<OverlayButton> Commands
        {
            get => _commands;
            set
            {
                _commands = value;
                OnPropertyChanged();
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            Commands.Add(new OverlayButton
            {
                Label = "Ny knapp",
                Text = "/me utför en handling"
            });
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is OverlayButton command)
            {
                var result = MessageBox.Show(
                    $"Är du säker på att du vill ta bort '{command.Label}'?",
                    "Bekräfta borttagning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    Commands.Remove(command);
                }
            }
        }

        private void MoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is OverlayButton command)
            {
                var index = Commands.IndexOf(command);
                if (index > 0)
                {
                    Commands.Move(index, index - 1);
                }
            }
        }

        private void MoveDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is OverlayButton command)
            {
                var index = Commands.IndexOf(command);
                if (index < Commands.Count - 1)
                {
                    Commands.Move(index, index + 1);
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate commands
                var invalidCommands = Commands.Where(c => 
                    string.IsNullOrWhiteSpace(c.Label) || 
                    string.IsNullOrWhiteSpace(c.Text)).ToList();

                if (invalidCommands.Any())
                {
                    MessageBox.Show(
                        "Alla kommandon måste ha både en etikett och text.",
                        "Valideringsfel",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Save to config
                var config = _configService.Current;
                
                // Preserve window settings when saving button configuration
                var windowSettings = config.Window;
                config.Buttons = Commands.ToList();
                config.Window = windowSettings;
                
                _configService.Save(config);

                MessageBox.Show(
                    "Inställningar sparade!",
                    "Sparat",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ett fel uppstod vid sparning: {ex.Message}",
                    "Fel",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
