using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using RPOverlay.Core.Models;
using RPOverlay.Core.Services;
using MessageDialogService = RPOverlay.WPF.Services.MessageDialogService;
using MouseClickOverrideManager = RPOverlay.WPF.Utilities.MouseClickOverrideManager;

namespace RPOverlay.WPF
{
    public partial class NewPromptWindow : Window, INotifyPropertyChanged
    {
        private readonly PromptManager _promptManager;
        private string _promptName = string.Empty;
        private string _displayName = string.Empty;
        private string _promptContent = string.Empty;
        private string _windowTitle = "Skapa ny systemprompten";
        private bool _isEditMode = false;
        private string? _originalPromptName = null;
        private bool _isNameFieldEnabled = true;
        public string? CreatedPromptName { get; private set; }

        public NewPromptWindow(PromptManager promptManager)
        {
            InitializeComponent();
            MouseClickOverrideManager.Register(this);
            DataContext = this;
            _promptManager = promptManager ?? throw new ArgumentNullException(nameof(promptManager));
            _isEditMode = false;
            _isNameFieldEnabled = true;
        }

        public NewPromptWindow(PromptManager promptManager, PromptDefinition existingPrompt) : this(promptManager)
        {
            _isEditMode = true;
            _originalPromptName = existingPrompt.Name;
            PromptName = existingPrompt.DisplayName;
            DisplayName = existingPrompt.DisplayName;
            PromptContent = existingPrompt.Content;
            WindowTitle = "Redigera Systemprompt";
            Title = "Redigera Systemprompt";
            
            // Disable name editing for default prompt
            if (existingPrompt.Name == "default")
            {
                IsNameFieldEnabled = false;
            }
        }

        public string WindowTitle
        {
            get => _windowTitle;
            set
            {
                if (_windowTitle == value) return;
                _windowTitle = value;
                OnPropertyChanged();
            }
        }

        public bool IsNameFieldEnabled
        {
            get => _isNameFieldEnabled;
            set
            {
                if (_isNameFieldEnabled == value) return;
                _isNameFieldEnabled = value;
                OnPropertyChanged();
            }
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
                MessageDialogService.Show(
                    "Du måste ange ett namn för prompten.",
                    "Valideringsfel",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(PromptContent))
            {
                MessageDialogService.Show(
                    "Du måste ange innehållet för prompten.",
                    "Valideringsfel",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // If editing default prompt, keep the original name
            var promptInternalName = (_isEditMode && _originalPromptName == "default") 
                ? "default" 
                : PromptName.ToLower().Replace(" ", "_");
            
            // Check if prompt already exists (only if not in edit mode or if name changed)
            if (!_isEditMode || (_isEditMode && promptInternalName != _originalPromptName))
            {
                var existing = _promptManager.LoadPrompt(promptInternalName);
                if (existing != null)
                {
                    var result = MessageDialogService.Show(
                        $"En prompt med namnet '{PromptName}' finns redan. Vill du skriva över den?",
                        "Bekräfta överskrivning",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result != MessageBoxResult.Yes)
                        return;
                }
            }

            try
            {
                // If in edit mode and name changed (and not default), delete the old prompt
                if (_isEditMode && _originalPromptName != null && _originalPromptName != "default" && promptInternalName != _originalPromptName)
                {
                    _promptManager.DeletePrompt(_originalPromptName);
                }

                var newPrompt = new PromptDefinition
                {
                    Name = promptInternalName,
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
                    MessageDialogService.Show(
                        "Ett fel uppstod när prompten skulle sparas.",
                        "Fel",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageDialogService.Show(
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
