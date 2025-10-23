using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using RPOverlay.Core.Models;
using RPOverlay.Core.Services;

namespace RPOverlay.WPF
{
    public partial class NewPromptWindow : Window, INotifyPropertyChanged
    {
        private readonly PromptManager _promptManager;
        private string _promptName = string.Empty;
        private string _displayName = string.Empty;
        private string _promptContent = string.Empty;
        public string? CreatedPromptName { get; private set; }

        public NewPromptWindow(PromptManager promptManager)
        {
            InitializeComponent();
            DataContext = this;
            _promptManager = promptManager ?? throw new ArgumentNullException(nameof(promptManager));
        }

        public string PromptName
        {
            get => _promptName;
            set
            {
                if (_promptName == value) return;
                _promptName = value;
                OnPropertyChanged();
            }
        }

        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (_displayName == value) return;
                _displayName = value;
                OnPropertyChanged();
            }
        }

        public string PromptContent
        {
            get => _promptContent;
            set
            {
                if (_promptContent == value) return;
                _promptContent = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PromptName))
            {
                System.Windows.MessageBox.Show(
                    "Du måste ange ett namn för prompten.",
                    "Valideringsfel",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(PromptContent))
            {
                System.Windows.MessageBox.Show(
                    "Du måste ange innehållet för prompten.",
                    "Valideringsfel",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Check if prompt already exists
            var existing = _promptManager.LoadPrompt(PromptName);
            if (existing != null)
            {
                var result = System.Windows.MessageBox.Show(
                    $"En prompt med namnet '{PromptName}' finns redan. Vill du skriva över den?",
                    "Bekräfta överskrivning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result != MessageBoxResult.Yes)
                    return;
            }

            try
            {
                var newPrompt = new PromptDefinition
                {
                    Name = PromptName.ToLower().Replace(" ", "_"),
                    DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? PromptName : DisplayName,
                    Description = string.Empty,
                    Content = PromptContent,
                    Version = "1.0",
                    CreatedAt = DateTime.UtcNow
                };

                if (_promptManager.SavePrompt(newPrompt))
                {
                    CreatedPromptName = newPrompt.Name;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        "Ett fel uppstod när prompten skulle sparas.",
                        "Fel",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Ett fel uppstod: {ex.Message}",
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

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
