using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RPOverlay.Core.Models;
using RPOverlay.Core.Services;
using RPOverlay.Core.Providers;
using MessageDialogService = RPOverlay.WPF.Services.MessageDialogService;
using MouseClickOverrideManager = RPOverlay.WPF.Utilities.MouseClickOverrideManager;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace RPOverlay.WPF
{
    public partial class SettingsWindow : Window, INotifyPropertyChanged
    {
        private readonly OverlayConfigService _configService;
        private readonly UserSettingsService _userSettingsService;
        private readonly PromptManager _promptManager;
        private ObservableCollection<OverlayButton> _commands;
        private ObservableCollection<PromptDefinition> _availablePrompts;
        private PromptDefinition? _selectedPrompt;
        private double _windowOpacity;
        private string _toggleHotkey = "F9";
        private string _interactivityToggle = "XButton2";
        private readonly bool _initialUseMiddleClick;
        private bool _useMiddleClickAsPrimary;

        public SettingsWindow()
        {
            InitializeComponent();
            MouseClickOverrideManager.Register(this);
            DataContext = this;

            var pathProvider = new AppDataOverlayConfigPathProvider();
            _configService = new OverlayConfigService(pathProvider);
            _userSettingsService = new UserSettingsService(pathProvider);
            _promptManager = new PromptManager(pathProvider);
            _availablePrompts = new ObservableCollection<PromptDefinition>();
            
            // Load existing commands
            _commands = new ObservableCollection<OverlayButton>(
                _configService.Current.Buttons.Select(b => new OverlayButton 
                { 
                    Label = b.Label, 
                    Text = b.Text 
                }));

            // Load available prompts
            _promptManager.EnsureDefaultPromptExists();
            LoadAvailablePrompts();

            // Load user settings
            var userSettings = _userSettingsService.Load();
            _windowOpacity = userSettings.Opacity;
            _toggleHotkey = userSettings.ToggleHotkey;
            _interactivityToggle = userSettings.InteractivityToggle;
            _initialUseMiddleClick = userSettings.UseMiddleClickAsPrimary;
            _useMiddleClickAsPrimary = _initialUseMiddleClick;
            MouseClickOverrideManager.SetMode(_useMiddleClickAsPrimary);
            OnPropertyChanged(nameof(UseMiddleClickAsPrimary));
            
            // Load the active prompt from the current collection so bindings select the correct item instance
            if (!string.IsNullOrWhiteSpace(userSettings.ActivePromptName))
            {
                _selectedPrompt = _availablePrompts.FirstOrDefault(p =>
                    string.Equals(p.Name, userSettings.ActivePromptName, StringComparison.OrdinalIgnoreCase));
            }

            if (_selectedPrompt == null)
            {
                _selectedPrompt = _availablePrompts.FirstOrDefault();
            }
            
            // Set API key in PasswordBox (PasswordBox.Password can't be bound)
            if (!string.IsNullOrWhiteSpace(userSettings.OpenAiApiKey))
            {
                ApiKeyPasswordBox.Password = userSettings.OpenAiApiKey;
            }
        }

        private void LoadAvailablePrompts()
        {
            _availablePrompts.Clear();
            var promptNames = _promptManager.ListPrompts();
            foreach (var name in promptNames)
            {
                var prompt = _promptManager.LoadPrompt(name);
                if (prompt != null)
                {
                    _availablePrompts.Add(prompt);
                }
            }
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

        public double WindowOpacity
        {
            get => _windowOpacity;
            set
            {
                if (Math.Abs(_windowOpacity - value) < 0.001) return;
                _windowOpacity = value;
                OnPropertyChanged();
                
                // Apply opacity to MainWindow immediately
                if (Owner is MainWindow mainWindow)
                {
                    mainWindow.Opacity = value;
                }
            }
        }

        public string ToggleHotkey
        {
            get => _toggleHotkey;
            set
            {
                if (_toggleHotkey == value) return;
                _toggleHotkey = value;
                OnPropertyChanged();
            }
        }

        public string InteractivityToggle
        {
            get => _interactivityToggle;
            set
            {
                if (_interactivityToggle == value) return;
                _interactivityToggle = value;
                OnPropertyChanged();
            }
        }

        public bool UseMiddleClickAsPrimary
        {
            get => _useMiddleClickAsPrimary;
            set
            {
                if (_useMiddleClickAsPrimary == value) return;
                _useMiddleClickAsPrimary = value;
                OnPropertyChanged();
                MouseClickOverrideManager.SetMode(value);
            }
        }
        
        public ObservableCollection<PromptDefinition> AvailablePrompts
        {
            get => _availablePrompts;
            set
            {
                _availablePrompts = value;
                OnPropertyChanged();
            }
        }
        
        public PromptDefinition? SelectedPrompt
        {
            get => _selectedPrompt;
            set
            {
                if (_selectedPrompt == value) return;
                
                // Warn user if prompt is being changed and there's active chat
                if (_selectedPrompt != null && value != null && _selectedPrompt.Name != value.Name)
                {
                    var result = MessageDialogService.Show(
                        "Du håller på att byta systemprompten. Detta kommer att rensa chatthistoriken.\n\nVill du fortsätta?",
                        "Byt systemprompten",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result != MessageBoxResult.Yes)
                        return;
                }
                
                _selectedPrompt = value;
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
                var result = MessageDialogService.Show(
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
                    MessageDialogService.Show(
                        "Alla kommandon måste ha både en etikett och text.",
                        "Valideringsfel",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Save overlay commands to config
                var config = _configService.Current;
                
                // Preserve window settings when saving button configuration
                var windowSettings = config.Window;
                config.Buttons = Commands.ToList();
                config.Window = windowSettings;
                
                _configService.Save(config);

                // Save user settings
                var userSettings = _userSettingsService.Load(); // Load fresh to get all current values
                userSettings.Opacity = WindowOpacity;
                userSettings.ToggleHotkey = ToggleHotkey;
                userSettings.InteractivityToggle = InteractivityToggle;
                userSettings.UseMiddleClickAsPrimary = UseMiddleClickAsPrimary;
                userSettings.OpenAiApiKey = ApiKeyPasswordBox.Password;
                userSettings.ActivePromptName = SelectedPrompt?.Name ?? "default";
                _userSettingsService.Save(userSettings);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageDialogService.Show(
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

        private bool _isCapturingHotkey = false;

        private void HotkeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isCapturingHotkey) return;
            
            _isCapturingHotkey = true;
            var button = sender as System.Windows.Controls.Button;
            
            if (button != null)
            {
                button.Content = "Tryck på en tangent...";
                button.Focus();
                
                // Capture the next key press
                System.Windows.Input.KeyEventHandler? keyHandler = null;
                keyHandler = (s, ev) =>
                {
                    ev.Handled = true;
                    
                    // Build hotkey string
                    var modifiers = new System.Collections.Generic.List<string>();
                    
                    if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
                        modifiers.Add("Ctrl");
                    if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                        modifiers.Add("Shift");
                    if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
                        modifiers.Add("Alt");
                    if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0)
                        modifiers.Add("Win");
                    
                    // Get the key
                    var key = ev.Key == Key.System ? ev.SystemKey : ev.Key;
                    
                    // Skip modifier keys themselves
                    if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                        key == Key.LeftShift || key == Key.RightShift ||
                        key == Key.LeftAlt || key == Key.RightAlt ||
                        key == Key.LWin || key == Key.RWin)
                    {
                        return;
                    }
                    
                    modifiers.Add(key.ToString());
                    
                    var hotkeyString = string.Join("+", modifiers);
                    ToggleHotkey = hotkeyString;
                    button.Content = hotkeyString;
                    
                    // Unsubscribe
                    button.PreviewKeyDown -= keyHandler;
                    _isCapturingHotkey = false;
                };
                
                button.PreviewKeyDown += keyHandler;
            }
        }

        private bool _isCapturingInteractivity = false;

        private void InteractivityButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isCapturingInteractivity) return;
            
            _isCapturingInteractivity = true;
            var button = sender as System.Windows.Controls.Button;
            
            if (button != null)
            {
                button.Content = "Tryck på tangent eller musknapp...";
                button.Focus();
                
                // Declare handlers first
                System.Windows.Input.KeyEventHandler? keyHandler = null;
                System.Windows.Input.MouseButtonEventHandler? mouseHandler = null;
                
                // Capture keyboard
                keyHandler = (s, ev) =>
                {
                    ev.Handled = true;
                    
                    var key = ev.Key == Key.System ? ev.SystemKey : ev.Key;
                    
                    // Skip modifier keys themselves
                    if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                        key == Key.LeftShift || key == Key.RightShift ||
                        key == Key.LeftAlt || key == Key.RightAlt ||
                        key == Key.LWin || key == Key.RWin)
                    {
                        return;
                    }
                    
                    // Build key string with modifiers
                    var modifiers = new System.Collections.Generic.List<string>();
                    
                    if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
                        modifiers.Add("Ctrl");
                    if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                        modifiers.Add("Shift");
                    if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
                        modifiers.Add("Alt");
                    if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0)
                        modifiers.Add("Win");
                    
                    modifiers.Add(key.ToString());
                    
                    var keyString = string.Join("+", modifiers);
                    InteractivityToggle = keyString;
                    button.Content = keyString;
                    
                    // Cleanup
                    button.PreviewKeyDown -= keyHandler;
                    button.PreviewMouseDown -= mouseHandler;
                    _isCapturingInteractivity = false;
                };
                
                // Capture mouse buttons
                mouseHandler = (s, ev) =>
                {
                    ev.Handled = true;
                    
                    string mouseButtonName = ev.ChangedButton switch
                    {
                        MouseButton.Left => "LeftClick",
                        MouseButton.Right => "RightClick",
                        MouseButton.Middle => "MiddleClick",
                        MouseButton.XButton1 => "XButton1",
                        MouseButton.XButton2 => "XButton2",
                        _ => "UnknownButton"
                    };
                    
                    InteractivityToggle = mouseButtonName;
                    button.Content = mouseButtonName;
                    
                    // Cleanup
                    button.PreviewKeyDown -= keyHandler;
                    button.PreviewMouseDown -= mouseHandler;
                    _isCapturingInteractivity = false;
                };
                
                button.PreviewKeyDown += keyHandler;
                button.PreviewMouseDown += mouseHandler;
            }
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

        private void NewPromptButton_Click(object sender, RoutedEventArgs e)
        {
            var newPromptWindow = new NewPromptWindow(_promptManager)
            {
                Owner = this
            };

            if (newPromptWindow.ShowDialog() == true)
            {
                // Reload available prompts
                LoadAvailablePrompts();
                
                // Select the newly created prompt
                var newPrompt = _availablePrompts.FirstOrDefault(p => p.Name == newPromptWindow.CreatedPromptName);
                if (newPrompt != null)
                {
                    SelectedPrompt = newPrompt;
                }
            }
        }

        private void EditPromptButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPrompt == null)
            {
                MessageDialogService.Show(
                    "Välj en prompt att redigera.",
                    "Ingen prompt vald",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var editPromptWindow = new NewPromptWindow(_promptManager, SelectedPrompt)
            {
                Owner = this
            };

            if (editPromptWindow.ShowDialog() == true)
            {
                // Reload available prompts
                LoadAvailablePrompts();
                
                // Re-select the edited prompt
                var editedPrompt = _availablePrompts.FirstOrDefault(p => p.Name == SelectedPrompt.Name);
                if (editedPrompt != null)
                {
                    SelectedPrompt = editedPrompt;
                }
            }
        }

        private void DeletePromptButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPrompt == null)
            {
                MessageDialogService.Show(
                    "Välj en prompt att ta bort.",
                    "Ingen prompt vald",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // Don't allow deleting the default prompt
            if (SelectedPrompt.Name == "default")
            {
                MessageDialogService.Show(
                    "Standard-prompten kan inte tas bort.",
                    "Kan inte ta bort",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var result = MessageDialogService.Show(
                $"Är du säker på att du vill ta bort prompten '{SelectedPrompt.DisplayName}'?",
                "Bekräfta borttagning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _promptManager.DeletePrompt(SelectedPrompt.Name);
                    
                    // Reload available prompts
                    LoadAvailablePrompts();
                    
                    // Select the default prompt
                    SelectedPrompt = _availablePrompts.FirstOrDefault(p => p.Name == "default");
                }
                catch (Exception ex)
                {
                    MessageDialogService.Show(
                        $"Fel vid borttagning av prompt: {ex.Message}",
                        "Fel",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected override void OnClosed(EventArgs e)
        {
            if (DialogResult != true)
            {
                MouseClickOverrideManager.SetMode(_initialUseMiddleClick);
            }

            base.OnClosed(e);
        }
    }
}
