using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using System.IO;
using System.Text.Json;
using System.Text;
using RPOverlay.Core.Models;
using RPOverlay.Core.Services;
using RPOverlay.Core.Utilities;
using RPOverlay.Core.Providers;
using RPOverlay.WPF.Interop;
using RPOverlay.WPF.Logging;
using RPOverlay.Infra.Services;
using MessageDialogService = RPOverlay.WPF.Services.MessageDialogService;
using MouseClickOverrideManager = RPOverlay.WPF.Utilities.MouseClickOverrideManager;

namespace RPOverlay.WPF
{
    public class NoteTab : INotifyPropertyChanged
    {
        private string _content = string.Empty;
        private string _displayName = string.Empty;
        private bool _isContextExcluded;
        
        /// <summary>
        /// The unique ID of the note (used for file name).
        /// </summary>
        public string Id { get; set; } = string.Empty;
        
        /// <summary>
        /// The internal name (same as ID for compatibility).
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// The display name shown in the tab header.
        /// </summary>
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

        /// <summary>
        /// True if this note should be excluded from chat context sharing.
        /// </summary>
        public bool IsContextExcluded
        {
            get => _isContextExcluded;
            set
            {
                if (_isContextExcluded == value) return;
                _isContextExcluded = value;
                OnPropertyChanged();
            }
        }
        
        /// <summary>
        /// The content of the note.
        /// </summary>
        public string Content
        {
            get => _content;
            set
            {
                if (_content == value) return;
                _content = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Whether the user has manually set a custom name.
        /// </summary>
        public bool HasCustomName { get; set; } = false;
        
        /// <summary>
        /// When the note was created.
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        /// <summary>
        /// When the note was last modified.
        /// </summary>
        public DateTime LastModifiedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Sort order for display (lower numbers appear first).
        /// </summary>
        public int SortOrder { get; set; } = 0;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    
    public class ChatMessageViewModel : INotifyPropertyChanged
    {
        private string _content = string.Empty;
        private double _fontSize = 13.0;
        
        public bool IsUser { get; set; }
        
        public string Content
        {
            get => _content;
            set
            {
                if (_content == value) return;
                _content = value;
                OnPropertyChanged();
            }
        }
        
        public System.Windows.HorizontalAlignment HorizontalAlignment => IsUser ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Left;
        
        public double FontSize
        {
            get => _fontSize;
            set
            {
                if (_fontSize == value) return;
                _fontSize = value;
                OnPropertyChanged();
            }
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly OverlayConfigService _configService;
        private readonly UserSettingsService _userSettingsService;
        private readonly PromptManager _promptManager;
        private NoteService _noteService;
        private readonly ProfilePathProvider _profilePathProvider;
        private readonly GlobalSettingsPathProvider _globalSettingsPathProvider;
        private readonly ObservableCollection<OverlayButton> _buttons = new();
        private HwndSource? _hwndSource;
        private IntPtr _windowHandle;
        private bool _overlayVisible;
        private ModifierKeys _currentModifiers = ModifierKeys.None;
        private Key _currentKey = Key.F9;
        private readonly int _hotkeyId = 0x1001;
        private IntPtr _lastForegroundWindow = IntPtr.Zero;
        private string _hotkeyHint = "F9";
        private string _currentProfileName = "Standard";
        private bool _isDisposed;
        private readonly DispatcherTimer _autoSaveTimer;
        private DispatcherTimer? _saveDebounceTimer; // Debounce timer for saving on text change
        private string _notesDirectory; // Notes directory for the current profile (can change when switching profiles)
        private readonly Dictionary<string, NoteTab> _noteTabs = new();
        private static bool _debugMode = false;
        private double _noteFontSize = 16.0; // Default font size (increased from 12.0)
        private bool _isClickThrough = false; // Start in interactive mode (not click-through)
        private bool _overlayInteractive = true; // Track if overlay is interactive - start interactive
        private readonly DispatcherTimer _mouseCheckTimer;
        private int _interactivityToggleVK = NativeMethods.VK_XBUTTON2; // Virtual key code for interactivity toggle
        private bool _useMiddleClickAsPrimary = false;
        
        // Chat-related fields
        private readonly ChatService _chatService = new();
        private readonly ObservableCollection<ChatMessageViewModel> _chatMessages = new();
        private ToggleButton? _chatToggle;
        private ItemsControl? _chatMessagesControl;
        private System.Windows.Controls.TextBox? _chatInputBox;
        private ScrollViewer? _chatScrollViewer;
        private bool _isSendingMessage = false;
        private string _chatInputText = string.Empty;
        private CancellationTokenSource? _chatCancellationTokenSource;
        private int _lastSelectedTabIndex = -1; // Store the last selected tab index before opening chat
        private bool _isSettingTabIndex = false; // Flag to prevent SelectionChanged from closing chat when we programmatically change SelectedIndex
        private bool _isClosingChatFromTabClick = false; // Flag to indicate chat is being closed due to a tab click (not manual close)
        private DispatcherTimer? _loadingAnimationTimer;
        private int _loadingDotCount = 0;
        private bool _isTabContextEnabled;
        private bool _hasSentInitialContext;
        private readonly Dictionary<string, string> _lastSentContextByNoteId = new();

        private static readonly string ContextSystemPromptAppendix = string.Join(Environment.NewLine, new[]
        {
            "[Intern instruktion – brukas av assistenten, inte användaren]",
            "Applikationen injicerar extra block i användarmeddelanden för att dela anteckningsflikar.",
            "<Kontext> innehåller en fullständig ögonblicksbild av alla inkluderade flikar och ersätter tidigare kontext.",
            "<KontextAddon> innehåller endast ny text eller nya flikar sedan föregående meddelande. Saknas block betyder att ingen ny kontext har tillkommit.",
            "Varje block består av sektioner som inleds med [Flik: Namn], eventuellt följt av '(Ny flik)', och sektionerna separeras med '---'.",
            "En tom <Kontext> betyder att kontexten har rensats. Använd alltid den senaste informationen från dessa block i dina svar.",
            "Syftet med detta är att ge dig (assistenten) relevant information från användarens anteckningar för att förbättra dina svar och hjälpa till med rollspelsrelaterade frågor.",
            "Du behöver inte alltid använda informationen i anteckningarna, endast när det är relevant för användarens fråga.",
        });

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                DataContext = this;
                NotesTabControl.Loaded += NotesTabControl_Loaded;
                NotesTabControl.SelectionChanged += NotesTabControl_SelectionChanged;

                // Check for debug mode argument
                var args = Environment.GetCommandLineArgs();
                _debugMode = args.Any(arg => arg.Equals("--debug", StringComparison.OrdinalIgnoreCase) || 
                                              arg.Equals("/debug", StringComparison.OrdinalIgnoreCase));
                
                if (_debugMode)
                {
                    DebugLogger.Log("DEBUG MODE ENABLED - Will use Notepad instead of FiveM");
                }

                DebugLogger.Log("MainWindow constructor: Creating services...");
                _profilePathProvider = new ProfilePathProvider();
                _globalSettingsPathProvider = new GlobalSettingsPathProvider();
                
                _configService = new OverlayConfigService(_profilePathProvider);
                _userSettingsService = new UserSettingsService(_globalSettingsPathProvider);
                _promptManager = new PromptManager(_globalSettingsPathProvider);
                _configService.ConfigReloaded += OnConfigReloaded;

                // Set current profile name
                UpdateCurrentProfileName();

                // Load user settings
                LoadUserSettings();

                // Initialize notes directory from profile
                _notesDirectory = _profilePathProvider.GetNotesDirectory();
                Directory.CreateDirectory(_notesDirectory);
                
                // Initialize NoteService
                _noteService = new NoteService(_notesDirectory);
                
                // Migrate old .txt notes to .yml format
                _noteService.MigrateOldNotesIfNeeded();

                // Initialize auto-save timer (every 30 seconds)
                _autoSaveTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(30)
                };
                _autoSaveTimer.Tick += AutoSaveTimer_Tick;

                // Initialize mouse check timer for click-through detection
                _mouseCheckTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(50) // Check every 50ms
                };
                _mouseCheckTimer.Tick += MouseCheckTimer_Tick;

                Loaded += OnLoaded;
                Unloaded += OnUnloaded;

                ApplyConfig(_configService.Current);
                InitializeNoteTabs();
                
                DebugLogger.Log("MainWindow constructor: Complete");
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex);
                throw;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<OverlayButton> Buttons => _buttons;

        public string HotkeyHint
        {
            get => _hotkeyHint;
            private set
            {
                if (_hotkeyHint == value)
                {
                    return;
                }

                _hotkeyHint = value;
                OnPropertyChanged();
            }
        }

        public string CurrentProfileName
        {
            get => _currentProfileName;
            private set
            {
                if (_currentProfileName == value)
                {
                    return;
                }

                _currentProfileName = value;
                OnPropertyChanged();
            }
        }

        private void LoadUserSettings()
        {
            try
            {
                var settings = _userSettingsService.Load();
                
                // Apply opacity
                this.Opacity = settings.Opacity;
                
                // Apply font size
                _noteFontSize = settings.FontSize;
                
                // Apply hotkey
                if (!string.IsNullOrWhiteSpace(settings.ToggleHotkey))
                {
                    if (HotkeyParser.TryParse(settings.ToggleHotkey, out var definition) &&
                        definition != null &&
                        TryConvertKey(definition.Key, out var key))
                    {
                        _currentModifiers = ToModifierKeys(definition.Modifiers);
                        _currentKey = key;
                        HotkeyHint = BuildHotkeyHint(definition);
                    }
                }
                
                // Apply interactivity toggle
                _interactivityToggleVK = ConvertInteractivityToggleToVK(settings.InteractivityToggle);

                // Apply click interaction mode
                _useMiddleClickAsPrimary = settings.UseMiddleClickAsPrimary;
                MouseClickOverrideManager.SetMode(_useMiddleClickAsPrimary);
                
                // Apply window size
                if (settings.WindowWidth > 0) Width = settings.WindowWidth;
                if (settings.WindowHeight > 0) Height = settings.WindowHeight;
                
                // Apply window position if saved, otherwise use default positioning
                if (settings.WindowLeft >= 0 && settings.WindowTop >= 0)
                {
                    Left = settings.WindowLeft;
                    Top = settings.WindowTop;
                    
                    // Validate that window is within screen bounds
                    var workArea = SystemParameters.WorkArea;
                    if (Left < workArea.Left || Left > workArea.Right - Width)
                    {
                        Left = workArea.Right - Width - 24;
                    }
                    if (Top < workArea.Top || Top > workArea.Bottom - Height)
                    {
                        Top = workArea.Top + 24;
                    }
                }
                
                DebugLogger.Log($"Loaded user settings: Opacity={settings.Opacity}, FontSize={settings.FontSize}, Hotkey={settings.ToggleHotkey}, InteractivityToggle={settings.InteractivityToggle}, Position=({settings.WindowLeft},{settings.WindowTop})");
                
                // Ensure default prompt exists and migrate old system prompt if needed
                _promptManager.EnsureDefaultPromptExists();
                
                // Configure ChatService with API key and active prompt
                _isTabContextEnabled = settings.EnableTabContext;
                InitializeChatService(settings.OpenAiApiKey, settings.ActivePromptName, settings.EnableTabContext);
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex);
            }
        }
        
        private void InitializeChatService(string apiKey, string promptName, bool enableTabContext)
        {
            try
            {
                _isTabContextEnabled = enableTabContext;
                ResetContextTracking();

                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    // Load the prompt from disk
                    var prompt = _promptManager.LoadPrompt(promptName);
                    if (prompt == null)
                    {
                        DebugLogger.Log($"Warning: Prompt '{promptName}' not found, falling back to default");
                        prompt = _promptManager.LoadPrompt("default");
                    }
                    
                    var systemPromptContent = prompt?.Content ?? "Du är en hjälpsam assistent för rollspel.";
                    if (enableTabContext)
                    {
                        systemPromptContent = BuildContextAwareSystemPrompt(systemPromptContent);
                    }

                    _chatService.Configure(apiKey, systemPromptContent);
                    DebugLogger.Log($"ChatService configured successfully med prompt: {prompt?.DisplayName ?? promptName} (tabbkontext {(enableTabContext ? "på" : "av")})");
                }
                else
                {
                    _chatService.Configure(string.Empty, null);
                    DebugLogger.Log("ChatService not configured - no API key provided");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex);
                MessageDialogService.Show($"Fel vid konfiguration av AI-chatt: {ex.Message}", "ChatService Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private int ConvertInteractivityToggleToVK(string toggle)
        {
            // Mouse buttons
            if (toggle == "XButton1") return NativeMethods.VK_XBUTTON1;
            if (toggle == "XButton2") return NativeMethods.VK_XBUTTON2;
            if (toggle == "LeftClick") return NativeMethods.VK_LBUTTON;
            if (toggle == "RightClick") return NativeMethods.VK_RBUTTON;
            if (toggle == "MiddleClick") return NativeMethods.VK_MBUTTON;
            
            // Keyboard keys - try to convert using WPF Key enum
            try
            {
                // Remove modifiers if present (e.g., "Ctrl+F9" -> "F9")
                var parts = toggle.Split('+');
                var keyPart = parts[parts.Length - 1];
                
                if (Enum.TryParse<Key>(keyPart, true, out var key))
                {
                    return KeyInterop.VirtualKeyFromKey(key);
                }
            }
            catch
            {
                // Fall through to default
            }
            
            // Default to XButton2
            return NativeMethods.VK_XBUTTON2;
        }

        private void SaveUserSettings()
        {
            try
            {
                var settings = _userSettingsService.Current;
                settings.FontSize = _noteFontSize;
                settings.Opacity = this.Opacity;
                settings.WindowWidth = Width;
                settings.WindowHeight = Height;
                settings.WindowLeft = Left;
                settings.WindowTop = Top;
                
                // Note: We no longer save OpenTabs to settings
                // Tabs are now determined by which .yml files exist in the Notes directory
                
                _userSettingsService.Save(settings);
                DebugLogger.Log("Saved user settings");
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            DebugLogger.Log("MainWindow.OnLoaded called");
            _hwndSource = (HwndSource)PresentationSource.FromVisual(this)!;
            _hwndSource.AddHook(WndProc);
            _windowHandle = _hwndSource.Handle;

            DebugLogger.Log($"Window handle: {_windowHandle}");

            ApplyWindowStyles();
            RegisterHotkey();
            ShowOverlay(); // Start with overlay visible
            SetClickThrough(false); // Start in interactive mode (NOT click-through)
            
            // Set initial visual state - active/interactive with green indicator
            this.Cursor = System.Windows.Input.Cursors.Arrow; // Show cursor
            
            var statusIndicator = this.FindName("StatusIndicator") as System.Windows.Shapes.Ellipse;
            if (statusIndicator != null)
            {
                statusIndicator.Fill = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0, 255, 0)); // Green
            }
            
            _autoSaveTimer.Start(); // Start auto-save timer
            _mouseCheckTimer.Start(); // Start mouse check timer
            DebugLogger.Log("OnLoaded: Complete - Overlay starts in INTERACTIVE mode (green indicator, cursor visible), press Mouse Button 5 to toggle");
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Cleanup();
        }

        private void ApplyConfig(OverlayConfig config)
        {
            Buttons.Clear();

            if (config.Buttons is not null)
            {
                foreach (var button in config.Buttons)
                {
                    if (string.IsNullOrWhiteSpace(button.Label) || string.IsNullOrWhiteSpace(button.Text))
                    {
                        continue;
                    }

                    Buttons.Add(button);
                }
            }

            var configuredHotkey = string.IsNullOrWhiteSpace(config.Hotkey) ? "F9" : config.Hotkey.Trim();

            if (!HotkeyParser.TryParse(configuredHotkey, out var definition) ||
                definition is null ||
                !TryConvertKey(definition.Key, out var key))
            {
                _currentModifiers = ModifierKeys.None;
                _currentKey = Key.F9;
                HotkeyHint = "F9";
            }
            else
            {
                _currentModifiers = ToModifierKeys(definition.Modifiers);
                _currentKey = key;
                HotkeyHint = BuildHotkeyHint(definition);
            }

            // Only re-register hotkey if window handle is already set (i.e., we're not in constructor)
            if (_windowHandle != IntPtr.Zero)
            {
                RegisterHotkey();
            }
        }

        private void RegisterHotkey()
        {
            if (_windowHandle == IntPtr.Zero)
            {
                DebugLogger.Log("RegisterHotkey: _windowHandle is Zero");
                return;
            }

            NativeMethods.UnregisterHotKey(_windowHandle, _hotkeyId);

            if (_currentKey == Key.None)
            {
                DebugLogger.Log("RegisterHotkey: _currentKey is None");
                return;
            }

            try
            {
                var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(_currentKey);
                var modifiers = ConvertModifiersToNative(_currentModifiers) | NativeMethods.MOD_NOREPEAT;

                DebugLogger.Log($"RegisterHotkey: Attempting to register VK={virtualKey}, Modifiers={modifiers}, Key={_currentKey}");

                var result = NativeMethods.RegisterHotKey(_windowHandle, _hotkeyId, modifiers, virtualKey);
                DebugLogger.Log($"RegisterHotkey: Result={result}");

                if (!result)
                {
                    var error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                    DebugLogger.Log($"RegisterHotkey: Failed with Win32 error {error}");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex);
            }
        }

        private static uint ConvertModifiersToNative(ModifierKeys modifiers)
        {
            uint result = 0;

            if (modifiers.HasFlag(ModifierKeys.Alt))
            {
                result |= NativeMethods.MOD_ALT;
            }

            if (modifiers.HasFlag(ModifierKeys.Control))
            {
                result |= NativeMethods.MOD_CONTROL;
            }

            if (modifiers.HasFlag(ModifierKeys.Shift))
            {
                result |= NativeMethods.MOD_SHIFT;
            }

            if (modifiers.HasFlag(ModifierKeys.Windows))
            {
                result |= NativeMethods.MOD_WIN;
            }

            return result;
        }

        private static ModifierKeys ToModifierKeys(HotkeyModifiers modifiers)
        {
            var result = ModifierKeys.None;

            if (modifiers.HasFlag(HotkeyModifiers.Alt))
            {
                result |= ModifierKeys.Alt;
            }

            if (modifiers.HasFlag(HotkeyModifiers.Control))
            {
                result |= ModifierKeys.Control;
            }

            if (modifiers.HasFlag(HotkeyModifiers.Shift))
            {
                result |= ModifierKeys.Shift;
            }

            if (modifiers.HasFlag(HotkeyModifiers.Windows))
            {
                result |= ModifierKeys.Windows;
            }

            return result;
        }

        private static bool TryConvertKey(string keyToken, out Key key)
        {
            return Enum.TryParse(keyToken, true, out key);
        }

        private static string BuildHotkeyHint(HotkeyDefinition definition)
        {
            var parts = new List<string>();

            if (definition.Modifiers.HasFlag(HotkeyModifiers.Control))
            {
                parts.Add("Ctrl");
            }

            if (definition.Modifiers.HasFlag(HotkeyModifiers.Shift))
            {
                parts.Add("Shift");
            }

            if (definition.Modifiers.HasFlag(HotkeyModifiers.Alt))
            {
                parts.Add("Alt");
            }

            if (definition.Modifiers.HasFlag(HotkeyModifiers.Windows))
            {
                parts.Add("Win");
            }

            parts.Add(definition.Key.ToUpperInvariant());

            return string.Join("+", parts);
        }

        private void ApplyWindowStyles()
        {
            if (_windowHandle == IntPtr.Zero)
            {
                return;
            }

            var exStyle = NativeMethods.GetWindowLong(_windowHandle, NativeMethods.GWL_EXSTYLE);
            exStyle |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_TOPMOST;
            NativeMethods.SetWindowLong(_windowHandle, NativeMethods.GWL_EXSTYLE, exStyle);
        }

        private void PositionWindow()
        {
            const int margin = 24;
            var workArea = SystemParameters.WorkArea;
            var width = ActualWidth > 0 ? ActualWidth : Width;
            var height = ActualHeight > 0 ? ActualHeight : Height;

            Left = workArea.Right - width - margin;
            Top = workArea.Top + margin;
        }

        private void LoadWindowSettings()
        {
            try
            {
                var config = _configService.Current;
                if (config.Window != null)
                {
                    // Restore position and size
                    Left = config.Window.Left;
                    Top = config.Window.Top;
                    Width = config.Window.Width;
                    Height = config.Window.Height;

                    // Validate that window is within screen bounds
                    var workArea = SystemParameters.WorkArea;
                    if (Left < workArea.Left || Left > workArea.Right - Width)
                    {
                        Left = workArea.Right - Width - 24;
                    }
                    if (Top < workArea.Top || Top > workArea.Bottom - Height)
                    {
                        Top = workArea.Top + 24;
                    }

                    DebugLogger.Log($"Loaded window settings: Left={Left}, Top={Top}, Width={Width}, Height={Height}");
                }
                else
                {
                    // First run - use default positioning
                    PositionWindow();
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex);
                PositionWindow(); // Fallback to default
            }
        }

        private void SaveWindowSettings()
        {
            try
            {
                var config = _configService.Current;
                
                // Preserve existing config and only update window settings
                var newConfig = new OverlayConfig
                {
                    Hotkey = config.Hotkey,
                    Buttons = config.Buttons,
                    Window = new WindowSettings
                    {
                        Left = Left,
                        Top = Top,
                        Width = Width,
                        Height = Height
                    }
                };
                
                _configService.Save(newConfig);
                DebugLogger.Log($"Saved window settings: Left={Left}, Top={Top}, Width={Width}, Height={Height}");
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == _hotkeyId)
            {
                DebugLogger.Log("WndProc: Hotkey message received!");
                handled = true;
                ToggleOverlay();
            }

            return IntPtr.Zero;
        }

        private void ToggleOverlay()
        {
            DebugLogger.Log($"ToggleOverlay called. Current state: {_overlayVisible}");
            if (_overlayVisible)
            {
                HideOverlay();
            }
            else
            {
                ShowOverlay();
            }
        }

        public void ToggleOverlayFromTray()
        {
            Dispatcher.Invoke(ToggleOverlay);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
                // Save position after drag
                SaveUserSettings();
            }
        }

        private void HideButton_Click(object sender, RoutedEventArgs e)
        {
            HideOverlay();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageDialogService.Show(
                "Är du säker på att du vill stänga applikationen?",
                "Bekräfta stängning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            OpenSettingsWindow();
        }

        public void OpenSettingsWindow()
        {
            var settingsWindow = new SettingsWindow(
                _configService,
                _userSettingsService,
                _promptManager)
            {
                Owner = this
            };
            
            if (settingsWindow.ShowDialog() == true)
            {
                // Save old prompt name BEFORE loading new settings
                var oldPromptName = _userSettingsService.Current.ActivePromptName;
                
                // Reload user settings after they've been saved
                var settings = _userSettingsService.Load();
                
                // Check if prompt was changed
                if (oldPromptName != settings.ActivePromptName)
                {
                    // Clear chat history when prompt changes (keeps system message)
                    _chatMessages.Clear();
                    _chatService.ClearHistory();
                    DebugLogger.Log($"Chat cleared due to prompt change: {oldPromptName} -> {settings.ActivePromptName}");
                }
                
                // Reconfigure ChatService with potentially new API key and prompt
                InitializeChatService(settings.OpenAiApiKey, settings.ActivePromptName, settings.EnableTabContext);

                // Update click interaction mode in case it changed
                _useMiddleClickAsPrimary = settings.UseMiddleClickAsPrimary;
                MouseClickOverrideManager.SetMode(_useMiddleClickAsPrimary);
                
                // Reload config to update command buttons (Current is already updated by Save())
                ApplyConfig(_configService.Current);
                
                DebugLogger.Log("Settings saved, config reloaded, and ChatService reconfigured");
            }
        }

        private void ProfileManager_Click(object sender, RoutedEventArgs e)
        {
            var profileManager = new ProfileManagerWindow(_profilePathProvider)
            {
                Owner = this
            };
            
            profileManager.ProfileChanged += (s, profileId) =>
            {
                // Reload the application with the new profile
                ReloadWithNewProfile(profileId);
            };
            
            profileManager.ShowDialog();
        }

        private void ReloadWithNewProfile(string profileId)
        {
            try
            {
                DebugLogger.Log($"Switching to profile: {profileId}");
                
                // Save current state before switching
                SaveAllNotes();
                
                // Update the profile path provider
                _profilePathProvider.SetActiveProfile(profileId);

                // Reload overlay configuration so command buttons follow the active profile
                _configService.ReloadForCurrentProfile();

                // Update profile name display
                UpdateCurrentProfileName();

                // Update notes directory for the new profile
                _notesDirectory = _profilePathProvider.GetNotesDirectory();
                Directory.CreateDirectory(_notesDirectory);
                _noteService = new NoteService(_notesDirectory);
                
                // Reload user settings
                LoadUserSettings();
                
                // Clear and reload notes
                _noteTabs.Clear();
                NotesTabControl.Items.Clear();
                InitializeNoteTabs();
                
                // Clear chat history
                _chatMessages.Clear();
                _chatService.ClearHistory();
                _hasSentInitialContext = false;
                
                DebugLogger.Log($"Successfully switched to profile: {profileId}");
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex);
                System.Windows.MessageBox.Show(
                    $"Ett fel uppstod vid byte av profil: {ex.Message}",
                    "Fel",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void UpdateCurrentProfileName()
        {
            var profileService = _profilePathProvider.GetProfileService();
            var profile = profileService.GetProfile(_profilePathProvider.CurrentProfileId);
            CurrentProfileName = profile?.Name ?? "Standard";
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _noteFontSize = Math.Min(_noteFontSize + 2, 32); // Max 32
            UpdateAllNotesFontSize();
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _noteFontSize = Math.Max(_noteFontSize - 2, 8); // Min 8
            UpdateAllNotesFontSize();
        }

        private void UpdateAllNotesFontSize()
        {
            var tabControl = this.FindName("NotesTabControl") as System.Windows.Controls.TabControl;
            if (tabControl == null) return;

            foreach (TabItem tab in tabControl.Items)
            {
                // Check if content is a Grid (new structure) or TextBox (old structure)
                if (tab.Content is Grid grid && grid.Children.Count > 0 && grid.Children[0] is System.Windows.Controls.TextBox textBox)
                {
                    textBox.FontSize = _noteFontSize;
                }
                else if (tab.Content is System.Windows.Controls.TextBox directTextBox)
                {
                    directTextBox.FontSize = _noteFontSize;
                }
            }
            
            // Update chat messages font size as well
            foreach (var message in _chatMessages)
            {
                message.FontSize = _noteFontSize;
            }
        }

        private void AddNoteTab_Click(object sender, RoutedEventArgs e)
        {
            // Generate a unique default name
            int counter = 1;
            string tabName;
            do
            {
                tabName = $"Ny anteckning {counter}";
                counter++;
            } while (_noteTabs.ContainsKey(tabName));

            AddNoteTab(tabName, string.Empty, isNewTab: true, isProtectedTab: false);
            SaveUserSettings(); // Save updated tab list
        }
        
        private void NotesTabControl_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeChatTemplateParts();
        }

        private void NotesTabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Ignore changes when we're programmatically setting the index (e.g., when opening chat)
            if (_isSettingTabIndex)
            {
                return;
            }

            // Hide chat when a note tab is selected
            if (_chatToggle != null && _chatToggle.IsChecked == true)
            {
                // Set flag to indicate chat is being closed due to a tab click
                _isClosingChatFromTabClick = true;
                _chatToggle.IsChecked = false;
            }
        }

        private void InitializeChatTemplateParts()
        {
            if (NotesTabControl == null)
            {
                return;
            }

            NotesTabControl.ApplyTemplate();

            var toggle = NotesTabControl.Template?.FindName("ChatToggle", NotesTabControl) as ToggleButton;
            if (!ReferenceEquals(_chatToggle, toggle))
            {
                if (_chatToggle != null)
                {
                    _chatToggle.Checked -= ChatToggle_Checked;
                    _chatToggle.Unchecked -= ChatToggle_Unchecked;
                }

                _chatToggle = toggle;

                if (_chatToggle != null)
                {
                    _chatToggle.Checked += ChatToggle_Checked;
                    _chatToggle.Unchecked += ChatToggle_Unchecked;
                }
            }

            if (NotesTabControl.Template != null)
            {
                var messages = NotesTabControl.Template.FindName("ChatMessages", NotesTabControl) as ItemsControl;
                if (messages != null)
                {
                    _chatMessagesControl = messages;
                    if (_chatMessagesControl.ItemsSource == null)
                    {
                        _chatMessagesControl.ItemsSource = _chatMessages;
                    }
                }

                var chatInput = NotesTabControl.Template.FindName("ChatInputBox", NotesTabControl) as System.Windows.Controls.TextBox;
                if (chatInput != null)
                {
                    _chatInputBox = chatInput;
                }

                var chatScroll = NotesTabControl.Template.FindName("ChatScrollViewer", NotesTabControl) as ScrollViewer;
                if (chatScroll != null)
                {
                    _chatScrollViewer = chatScroll;
                }
            }
        }

        private void ChatToggle_Checked(object sender, RoutedEventArgs e)
        {
            // Store the currently selected tab index before deselecting
            _lastSelectedTabIndex = NotesTabControl.SelectedIndex;
            
            // Set flag to prevent SelectionChanged from closing chat
            _isSettingTabIndex = true;
            
            // Deselect the current note tab when opening chat
            NotesTabControl.SelectedIndex = -1;
            
            // Reset flag after setting
            _isSettingTabIndex = false;

            InitializeChatTemplateParts();

            if (_chatMessagesControl != null && _chatMessagesControl.ItemsSource == null)
            {
                _chatMessagesControl.ItemsSource = _chatMessages;
            }

            if (_chatMessages.Count == 0)
            {
                var initialMessage = _chatService.IsConfigured
                    ? "Hej! Hur kan jag hjälpa dig idag?"
                    : "⚠️ OpenAI API-nyckel saknas. Lägg till den i inställningar för att börja chatta.";

                _chatMessages.Add(new ChatMessageViewModel
                {
                    IsUser = false,
                    Content = initialMessage,
                    FontSize = _noteFontSize
                });
            }

            _chatInputBox?.Focus();
            ScrollChatToBottom();
        }

        private void ChatToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            // Only restore the previously selected tab if chat was closed manually (not due to a tab click)
            if (!_isClosingChatFromTabClick)
            {
                // Set flag to prevent SelectionChanged from interfering
                _isSettingTabIndex = true;
                
                // Restore the previously selected tab when closing chat
                if (_lastSelectedTabIndex >= 0 && _lastSelectedTabIndex < NotesTabControl.Items.Count)
                {
                    NotesTabControl.SelectedIndex = _lastSelectedTabIndex;
                }
                else if (NotesTabControl.Items.Count > 0)
                {
                    // If last index is invalid, select the first tab
                    NotesTabControl.SelectedIndex = 0;
                }
                
                // Reset flag
                _isSettingTabIndex = false;
            }
            
            // Reset the flag for next time
            _isClosingChatFromTabClick = false;
        }

        private void CloseChat_Click(object sender, RoutedEventArgs e)
        {
            InitializeChatTemplateParts();
            if (_chatToggle != null)
            {
                _chatToggle.IsChecked = false;
            }
        }
        
        private void ChatInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                SendChat_Click(sender, new RoutedEventArgs());
            }
        }
        
        private async void SendChat_Click(object sender, RoutedEventArgs e)
        {
            if (!CanSendMessage) return;

            InitializeChatTemplateParts();
            if (_chatMessagesControl != null && _chatMessagesControl.ItemsSource == null)
            {
                _chatMessagesControl.ItemsSource = _chatMessages;
            }
            
            var userMessage = ChatInputText.Trim();
            if (string.IsNullOrWhiteSpace(userMessage)) return;
            
            // Clear input
            ChatInputText = string.Empty;
            
            // Add user message
            var userMsgViewModel = new ChatMessageViewModel
            {
                IsUser = true,
                Content = userMessage,
                FontSize = _noteFontSize
            };
            _chatMessages.Add(userMsgViewModel);
            
            // Scroll to bottom
            ScrollChatToBottom();
            
            // Disable input while sending
            _isSendingMessage = true;
            OnPropertyChanged(nameof(CanSendMessage));
            
            // Create assistant message placeholder with loading animation
            var assistantMsgViewModel = new ChatMessageViewModel
            {
                IsUser = false,
                Content = "●●●",
                FontSize = _noteFontSize
            };
            _chatMessages.Add(assistantMsgViewModel);
            
            // Start loading animation
            _loadingDotCount = 0;
            StartLoadingAnimation(assistantMsgViewModel);
            
            var contextResult = PrepareContextForMessage(userMessage);
            var messageForService = contextResult.FinalMessage;
            var messageDelivered = false;

            try
            {
                // Cancel any existing operation
                _chatCancellationTokenSource?.Cancel();
                _chatCancellationTokenSource = new CancellationTokenSource();
                
                // Stream response
                await foreach (var chunk in _chatService.SendMessageStreamAsync(messageForService, _chatCancellationTokenSource.Token))
                {
                    // Stop loading animation and clear placeholder on first chunk
                    if (string.IsNullOrEmpty(assistantMsgViewModel.Content) || 
                        assistantMsgViewModel.Content.All(c => c == '●' || c == ' '))
                    {
                        StopLoadingAnimation();
                        assistantMsgViewModel.Content = "";
                    }
                    
                    assistantMsgViewModel.Content += chunk;
                    
                    // Scroll to bottom periodically
                    ScrollChatToBottom();
                }
                messageDelivered = true;
            }
            catch (OperationCanceledException)
            {
                StopLoadingAnimation();
                assistantMsgViewModel.Content = "[Meddelande avbrutet]";
            }
            catch (Exception ex)
            {
                StopLoadingAnimation();
                assistantMsgViewModel.Content = $"❌ Fel: {ex.Message}";
                DebugLogger.LogException(ex);
            }
            finally
            {
                StopLoadingAnimation();
                _isSendingMessage = false;
                OnPropertyChanged(nameof(CanSendMessage));
                ScrollChatToBottom();
                if (messageDelivered)
                {
                    ApplyContextResult(contextResult);
                }
            }
        }
        
        private void ScrollChatToBottom()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_chatScrollViewer == null)
                {
                    InitializeChatTemplateParts();
                }

                _chatScrollViewer?.ScrollToBottom();
            }), DispatcherPriority.Background);
        }

        private sealed class ContextBuildResult
        {
            public string FinalMessage { get; set; } = string.Empty;
            public bool SentContextBlock { get; set; }
            public bool ResetSnapshots { get; set; }
            public bool MarkInitialAsSent { get; set; }
            public Dictionary<string, string> UpdatedSnapshots { get; } = new();
        }

        private void ResetContextTracking()
        {
            _hasSentInitialContext = false;
            _lastSentContextByNoteId.Clear();
        }

        private string BuildContextAwareSystemPrompt(string basePrompt)
        {
            var trimmed = string.IsNullOrWhiteSpace(basePrompt)
                ? "Du är en hjälpsam assistent för rollspel."
                : basePrompt.Trim();

            return string.Join(Environment.NewLine + Environment.NewLine, new[]
            {
                trimmed,
                ContextSystemPromptAppendix
            });
        }

        private ContextBuildResult PrepareContextForMessage(string userMessage)
        {
            var result = new ContextBuildResult { FinalMessage = userMessage };

            if (!_isTabContextEnabled)
            {
                return result;
            }

            var includedNotes = _noteTabs.Values
                .Where(n => !n.IsContextExcluded && !string.IsNullOrWhiteSpace(n.Content))
                .OrderBy(n => n.SortOrder)
                .ThenBy(n => n.CreatedDate)
                .ToList();

            var includedIds = new HashSet<string>(includedNotes.Select(n => n.Id));
            var needsReset = !_hasSentInitialContext;

            if (!needsReset)
            {
                foreach (var previousId in _lastSentContextByNoteId.Keys)
                {
                    if (!includedIds.Contains(previousId))
                    {
                        needsReset = true;
                        break;
                    }
                }
            }

            if (!needsReset)
            {
                foreach (var note in includedNotes)
                {
                    if (_lastSentContextByNoteId.TryGetValue(note.Id, out var previous))
                    {
                        var current = note.Content;
                        if (previous.Length > current.Length ||
                            !current.StartsWith(previous, StringComparison.Ordinal))
                        {
                            needsReset = true;
                            break;
                        }
                    }
                }
            }

            if (needsReset)
            {
                var hadPreviousContext = _hasSentInitialContext && _lastSentContextByNoteId.Count > 0;
                if (includedNotes.Count == 0 && !hadPreviousContext)
                {
                    return result;
                }

                return BuildInitialContextResult(userMessage, includedNotes, hadPreviousContext);
            }

            return BuildIncrementalContextResult(userMessage, includedNotes);
        }

        private ContextBuildResult BuildInitialContextResult(string userMessage, List<NoteTab> includedNotes, bool hadPreviousContext)
        {
            var result = new ContextBuildResult
            {
                FinalMessage = userMessage,
                ResetSnapshots = true,
                MarkInitialAsSent = includedNotes.Count > 0
            };

            string contextBody;

            if (includedNotes.Count == 0)
            {
                contextBody = "Kontexten har rensats.";
            }
            else
            {
                foreach (var note in includedNotes)
                {
                    result.UpdatedSnapshots[note.Id] = note.Content;
                }

                var sections = includedNotes
                    .Select(note => BuildNoteSectionText(note, note.Content))
                    .Where(section => !string.IsNullOrWhiteSpace(section))
                    .ToList();

                contextBody = BuildNotesPayload(sections);

                if (string.IsNullOrWhiteSpace(contextBody))
                {
                    result.MarkInitialAsSent = false;
                    contextBody = hadPreviousContext ? "Kontexten har rensats." : string.Empty;
                }
            }

            if (string.IsNullOrWhiteSpace(contextBody))
            {
                return result;
            }

            result.SentContextBlock = true;
            result.FinalMessage = ComposeFinalMessage(userMessage, BuildContextBlock("Kontext", contextBody));
            return result;
        }

        private ContextBuildResult BuildIncrementalContextResult(string userMessage, List<NoteTab> includedNotes)
        {
            var result = new ContextBuildResult { FinalMessage = userMessage };
            var sections = new List<string>();

            foreach (var note in includedNotes)
            {
                var current = note.Content;

                if (!_lastSentContextByNoteId.TryGetValue(note.Id, out var previous) || string.IsNullOrEmpty(previous))
                {
                    var section = BuildNoteSectionText(note, current, markAsNew: true);
                    if (!string.IsNullOrWhiteSpace(section))
                    {
                        sections.Add(section);
                        result.UpdatedSnapshots[note.Id] = current;
                    }
                    continue;
                }

                if (string.Equals(current, previous, StringComparison.Ordinal))
                {
                    continue;
                }

                if (current.Length >= previous.Length && current.StartsWith(previous, StringComparison.Ordinal))
                {
                    var delta = current.Substring(previous.Length).TrimStart('\r', '\n');
                    if (string.IsNullOrWhiteSpace(delta))
                    {
                        result.UpdatedSnapshots[note.Id] = current;
                        continue;
                    }

                    var section = BuildNoteSectionText(note, delta);
                    if (!string.IsNullOrWhiteSpace(section))
                    {
                        sections.Add(section);
                        result.UpdatedSnapshots[note.Id] = current;
                    }
                    continue;
                }

                return BuildInitialContextResult(userMessage, includedNotes, _hasSentInitialContext || _lastSentContextByNoteId.Count > 0);
            }

            if (sections.Count == 0)
            {
                return result;
            }

            var contextBody = BuildNotesPayload(sections);
            if (string.IsNullOrWhiteSpace(contextBody))
            {
                return result;
            }

            result.SentContextBlock = true;
            result.FinalMessage = ComposeFinalMessage(userMessage, BuildContextBlock("KontextAddon", contextBody));
            return result;
        }

        private static string BuildNoteSectionText(NoteTab note, string content, bool markAsNew = false)
        {
            var sb = new StringBuilder();
            sb.Append("[Flik: ").Append(note.DisplayName).Append(']').AppendLine();
            if (markAsNew)
            {
                sb.AppendLine("(Ny flik)");
            }

            var sanitized = (content ?? string.Empty)
                .ReplaceLineEndings(Environment.NewLine)
                .TrimEnd();

            if (!string.IsNullOrWhiteSpace(sanitized))
            {
                AppendContent(sb, sanitized);
            }

            return sb.ToString().TrimEnd();
        }

        private static void AppendContent(StringBuilder sb, string content)
        {
            var lines = content.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                sb.Append(lines[i]);
                if (i < lines.Length - 1)
                {
                    sb.AppendLine();
                }
            }
        }

        private static string BuildNotesPayload(IReadOnlyList<string> sections)
        {
            if (sections.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(Environment.NewLine + "---" + Environment.NewLine, sections);
        }

        private static string BuildContextBlock(string tagName, string content)
        {
            var trimmed = content.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return $"<{tagName}></{tagName}>";
            }

            return $"<{tagName}>{Environment.NewLine}{trimmed}{Environment.NewLine}</{tagName}>";
        }

        private static string ComposeFinalMessage(string userMessage, string contextBlock)
        {
            if (string.IsNullOrWhiteSpace(contextBlock))
            {
                return userMessage;
            }

            if (string.IsNullOrEmpty(userMessage))
            {
                return contextBlock;
            }

            return $"{userMessage}{Environment.NewLine}{Environment.NewLine}{contextBlock}";
        }

        private void ApplyContextResult(ContextBuildResult result)
        {
            if (!_isTabContextEnabled)
            {
                return;
            }

            if (!result.SentContextBlock && result.UpdatedSnapshots.Count == 0 && !result.ResetSnapshots)
            {
                return;
            }

            if (result.ResetSnapshots)
            {
                _lastSentContextByNoteId.Clear();
            }

            foreach (var kvp in result.UpdatedSnapshots)
            {
                if (string.IsNullOrEmpty(kvp.Value))
                {
                    _lastSentContextByNoteId.Remove(kvp.Key);
                }
                else
                {
                    _lastSentContextByNoteId[kvp.Key] = kvp.Value;
                }
            }

            if (result.MarkInitialAsSent)
            {
                _hasSentInitialContext = true;
            }
            else if (result.ResetSnapshots)
            {
                _hasSentInitialContext = false;
            }
        }

        private void StartLoadingAnimation(ChatMessageViewModel loadingMessage)
        {
            if (_loadingAnimationTimer != null)
            {
                _loadingAnimationTimer.Stop();
            }

            _loadingAnimationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _loadingAnimationTimer.Tick += (s, e) =>
            {
                _loadingDotCount = (_loadingDotCount + 1) % 4;
                var dots = string.Concat(Enumerable.Range(0, _loadingDotCount).Select(_ => "●")) +
                          string.Concat(Enumerable.Range(0, 3 - _loadingDotCount).Select(_ => " "));
                loadingMessage.Content = dots;
            };
            _loadingAnimationTimer.Start();
        }

        private void StopLoadingAnimation()
        {
            if (_loadingAnimationTimer != null)
            {
                _loadingAnimationTimer.Stop();
                _loadingAnimationTimer = null;
            }
        }

        private void ClearChat_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageDialogService.Show(
                "Är du säker på att du vill starta om konversationen? Alla meddelanden kommer att raderas.",
                "Bekräfta omstart av konversation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _chatMessages.Clear();
                _chatService.ClearHistory();
                ResetContextTracking();
                
                // Lägg till välkomstmeddelandet igen
                var initialMessage = _chatService.IsConfigured
                    ? "Hej! Hur kan jag hjälpa dig idag?"
                    : "⚠️ OpenAI API-nyckel saknas. Lägg till den i inställningar för att börja chatta.";

                _chatMessages.Add(new ChatMessageViewModel
                {
                    IsUser = false,
                    Content = initialMessage,
                    FontSize = _noteFontSize
                });
                
                ScrollChatToBottom();
            }
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button button || button.Tag is not TabItem tabItem)
            {
                return;
            }

            var tabControl = this.FindName("NotesTabControl") as System.Windows.Controls.TabControl;
            if (tabControl == null) return;

            // Don't allow closing the last tab
            if (tabControl.Items.Count <= 1)
            {
                MessageDialogService.Show(
                    "Du måste ha minst en flik öppen.",
                    "Kan inte stänga flik",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var tabName = string.Empty;
            
            // Extract tab name from header (which is now a Grid containing TextBlock and TextBox)
            if (tabItem.Header is Grid headerGrid)
            {
                foreach (var child in headerGrid.Children)
                {
                    if (child is TextBlock textBlock && Equals(textBlock.Tag, "DisplayText"))
                    {
                        tabName = textBlock.Text;
                        break;
                    }
                }
            }
            else if (tabItem.Header is string headerStr)
            {
                // Fallback for old format
                tabName = headerStr;
            }
            
            if (string.IsNullOrEmpty(tabName)) return;

            var result = MessageDialogService.Show(
                $"Är du säker på att du vill stänga fliken '{tabName}'? Innehållet kommer att sparas.",
                "Bekräfta stängning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Save content and archive the note
                if (_noteTabs.TryGetValue(tabName, out var noteTab))
                {
                    SaveNoteContent(noteTab);
                    _noteService.ArchiveNote(noteTab.Id);
                }

                // Remove from tabs
                tabControl.Items.Remove(tabItem);
                _noteTabs.Remove(tabName);
                
                DebugLogger.Log($"Closed and archived tab: {tabName}");
            }
        }

        private void AddNoteTab(string tabName, string initialContent = "", bool isNewTab = false, bool isProtectedTab = false, string? noteId = null)
        {
            var tabControl = this.FindName("NotesTabControl") as System.Windows.Controls.TabControl;
            if (tabControl == null) return;

            var tab = new TabItem
            {
                VerticalContentAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch,
                AllowDrop = true
            };
            
            // Add drag & drop handlers for reordering tabs
            tab.PreviewMouseLeftButtonDown += Tab_PreviewMouseLeftButtonDown;
            tab.PreviewMouseMove += Tab_PreviewMouseMove;
            tab.Drop += Tab_Drop;
            
            // Create a custom header with both TextBlock (for display) and TextBox (for editing)
            var headerGrid = new Grid();
            
            var displayTextBlock = new TextBlock
            {
                Text = tabName,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.White,
                Visibility = Visibility.Visible,
                Tag = "DisplayText"
            };
            
            var editTextBox = new System.Windows.Controls.TextBox
            {
                Text = tabName,
                MinWidth = 60,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(35, 35, 35)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(47, 157, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(2),
                Visibility = Visibility.Collapsed
            };
            
            headerGrid.Children.Add(displayTextBlock);
            headerGrid.Children.Add(editTextBox);
            var contextIndicator = new System.Windows.Shapes.Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 99, 71)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -2, -2, 0),
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            System.Windows.Controls.Panel.SetZIndex(contextIndicator, 1);
            headerGrid.Children.Add(contextIndicator);
            
            tab.Header = headerGrid;

            var toggleContextMenuItem = new MenuItem();
            var tabContextMenu = new ContextMenu();
            tabContextMenu.Items.Add(toggleContextMenuItem);
            tab.ContextMenu = tabContextMenu;

            // Double-click to edit name
            displayTextBlock.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    displayTextBlock.Visibility = Visibility.Collapsed;
                    editTextBox.Visibility = Visibility.Visible;
                    editTextBox.Focus();
                    editTextBox.SelectAll();
                    e.Handled = true;
                }
            };
            
            // Finish editing on Enter or lost focus
            Action finishEditing = () =>
            {
                var newName = editTextBox.Text.Trim();
                if (!string.IsNullOrWhiteSpace(newName) && newName.Length <= 30)
                {
                    // Find the NoteTab associated with this tab
                    var oldName = displayTextBlock.Text;
                    if (_noteTabs.TryGetValue(oldName, out var noteTab))
                    {
                        // Update display name and mark as having custom name
                        noteTab.DisplayName = newName;
                        noteTab.HasCustomName = true;
                        displayTextBlock.Text = newName;
                        
                        // Update dictionary key if name changed
                        if (oldName != newName)
                        {
                            _noteTabs.Remove(oldName);
                            _noteTabs[newName] = noteTab;
                        }
                        
                        // Save immediately
                        SaveNoteContent(noteTab);
                    }
                }
                else
                {
                    // Revert to old name if invalid
                    editTextBox.Text = displayTextBlock.Text;
                }
                
                editTextBox.Visibility = Visibility.Collapsed;
                displayTextBlock.Visibility = Visibility.Visible;
            };
            
            editTextBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    finishEditing();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    editTextBox.Text = displayTextBlock.Text;
                    editTextBox.Visibility = Visibility.Collapsed;
                    displayTextBlock.Visibility = Visibility.Visible;
                    e.Handled = true;
                }
            };
            
            // Confirm editing when clicking outside the textbox (only for tab name editing)
            editTextBox.LostFocus += (s, e) =>
            {
                if (editTextBox.Visibility == Visibility.Visible)
                {
                    finishEditing();
                }
            };

            // Create a Grid to hold the TextBox and ensure it fills all space
            var grid = new Grid
            {
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
            };
            
            var textBox = new System.Windows.Controls.TextBox
            {
                Name = "NoteTextBox",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(28, 28, 28)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(47, 157, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontSize = _noteFontSize,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
            };
            
            // Apply the custom scrollbar style
            if (tabControl.Resources.Contains("CustomScrollBarStyle"))
            {
                textBox.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] = tabControl.Resources["CustomScrollBarStyle"];
            }

            grid.Children.Add(textBox);

            // Visual feedback on focus state
            textBox.GotFocus += (s, e) =>
            {
                textBox.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(35, 35, 35)); // Slightly lighter when focused
            };
            
            textBox.LostFocus += (s, e) =>
            {
                textBox.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(28, 28, 28)); // Back to normal
            };

            textBox.GotFocus += NoteTextBox_GotFocus;
            textBox.LostFocus += NoteTextBox_LostFocus;

            // Reuse the provided note ID if available; otherwise generate a new one
            var resolvedNoteId = string.IsNullOrWhiteSpace(noteId)
                ? Guid.NewGuid().ToString()
                : noteId;

            // Calculate sort order based on current tab count (for new tabs)
            var maxSortOrder = _noteTabs.Values.Any() ? _noteTabs.Values.Max(n => n.SortOrder) : -1;

            var noteTab = new NoteTab 
            { 
                Id = resolvedNoteId,
                Name = tabName, // Keep for compatibility
                DisplayName = tabName,
                Content = initialContent,
                CreatedDate = DateTime.Now,
                LastModifiedDate = DateTime.Now,
                HasCustomName = false,
                SortOrder = maxSortOrder + 1
            };
            _noteTabs[tabName] = noteTab;
            tab.Tag = noteTab;

            // Load saved content if exists (unless it's a brand new tab)
            if (!isNewTab)
            {
                LoadNoteContent(noteTab);
                // Update tab header with loaded display name
                displayTextBlock.Text = noteTab.DisplayName;
                editTextBox.Text = noteTab.DisplayName;
            }
            textBox.Text = noteTab.Content;

            void UpdateContextUi()
            {
                toggleContextMenuItem.Header = noteTab.IsContextExcluded
                    ? "Inkludera i kontext"
                    : "Exkludera från kontext";
                contextIndicator.Visibility = noteTab.IsContextExcluded
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            toggleContextMenuItem.Click += (s, e) =>
            {
                noteTab.IsContextExcluded = !noteTab.IsContextExcluded;
                UpdateContextUi();
                SaveNoteContent(noteTab);
                if (_isTabContextEnabled)
                {
                    ResetContextTracking();
                }
            };

            UpdateContextUi();

            // Bind textbox to note content and update tab name based on first line (only if not has custom name and not protected)
            textBox.TextChanged += (s, e) =>
            {
                // Find the NoteTab by searching through all tabs
                NoteTab? nt = null;
                foreach (var kvp in _noteTabs.Values)
                {
                    if (kvp.DisplayName == displayTextBlock.Text)
                    {
                        nt = kvp;
                        break;
                    }
                }
                
                if (nt == null)
                    return;

                nt.Content = textBox.Text;

                // Trigger debounced save
                DebouncedSaveNote(nt);

                // Only update tab name if it's not a protected default tab and doesn't have a custom name
                if (!isProtectedTab && !nt.HasCustomName)
                {
                    // Extract first line as tab name
                    var lines = textBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > 0)
                    {
                        var newName = lines[0].Trim();
                        if (!string.IsNullOrWhiteSpace(newName) && newName.Length <= 20) // Limit tab name length
                        {
                            // Update tab header if name changed
                            if (displayTextBlock.Text != newName)
                            {
                                nt.DisplayName = newName;
                                displayTextBlock.Text = newName;
                                editTextBox.Text = newName;
                            }
                        }
                    }
                }
            };

            tab.Content = grid;
            tabControl.Items.Add(tab);
            tabControl.SelectedItem = tab;
            
            // Focus the textbox if it's a new tab
            if (isNewTab)
            {
                textBox.Focus();
            }
        }

        // Drag & Drop support for tab reordering
        private TabItem? _draggedTab;
        private System.Windows.Point _dragStartPoint;

        private void Tab_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TabItem tab)
            {
                _dragStartPoint = e.GetPosition(null);
                _draggedTab = tab;
            }
        }

        private void Tab_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedTab != null)
            {
                System.Windows.Point currentPosition = e.GetPosition(null);
                System.Windows.Vector diff = _dragStartPoint - currentPosition;

                // Check if the drag threshold is exceeded
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    // Start the drag operation
                    DragDrop.DoDragDrop(_draggedTab, _draggedTab, System.Windows.DragDropEffects.Move);
                }
            }
        }

        private void Tab_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (sender is TabItem targetTab && e.Data.GetData(typeof(TabItem)) is TabItem sourceTab)
            {
                if (sourceTab == targetTab) return;

                var tabControl = this.FindName("NotesTabControl") as System.Windows.Controls.TabControl;
                if (tabControl == null) return;

                int sourceIndex = tabControl.Items.IndexOf(sourceTab);
                int targetIndex = tabControl.Items.IndexOf(targetTab);

                if (sourceIndex < 0 || targetIndex < 0) return;

                // Remove and reinsert the tab
                tabControl.Items.RemoveAt(sourceIndex);
                tabControl.Items.Insert(targetIndex, sourceTab);
                tabControl.SelectedItem = sourceTab;

                // Update sort orders for all tabs
                UpdateTabSortOrders(tabControl);
                
                DebugLogger.Log($"Reordered tabs: moved tab from index {sourceIndex} to {targetIndex}");
            }

            _draggedTab = null;
        }

        private void UpdateTabSortOrders(System.Windows.Controls.TabControl tabControl)
        {
            for (int i = 0; i < tabControl.Items.Count; i++)
            {
                if (tabControl.Items[i] is TabItem tabItem && tabItem.Header is Grid headerGrid)
                {
                    // Find the note tab by display name
                    foreach (var child in headerGrid.Children)
                    {
                        if (child is TextBlock textBlock && Equals(textBlock.Tag, "DisplayText"))
                        {
                            var tabName = textBlock.Text;
                            foreach (var noteTab in _noteTabs.Values)
                            {
                                if (noteTab.DisplayName == tabName)
                                {
                                    noteTab.SortOrder = i;
                                    SaveNoteContent(noteTab);
                                    break;
                                }
                            }
                            break;
                        }
                    }
                }
            }
        }

        private void ShowOverlay()
        {
            if (_windowHandle == IntPtr.Zero)
            {
                return;
            }

            _lastForegroundWindow = NativeMethods.GetForegroundWindow();
            Visibility = Visibility.Visible;

            NativeMethods.ShowWindow(_windowHandle, NativeMethods.SW_SHOWNOACTIVATE);
            NativeMethods.SetWindowPos(
                _windowHandle,
                NativeMethods.HWND_TOPMOST,
                0,
                0,
                0,
                0,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW);

            _overlayVisible = true;
        }

        private void HideOverlay()
        {
            if (_windowHandle == IntPtr.Zero)
            {
                return;
            }

            NativeMethods.ShowWindow(_windowHandle, NativeMethods.SW_HIDE);
            Visibility = Visibility.Hidden;
            _overlayVisible = false;
        }

        private void OnConfigReloaded(OverlayConfig config)
        {
            Dispatcher.Invoke(() => ApplyConfig(config));
        }

        private void PresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button button || button.Tag is not OverlayButton preset)
            {
                return;
            }

            System.Windows.Clipboard.SetText(preset.Text);

            var focusedElement = Keyboard.FocusedElement as FrameworkElement;
            var noteHasFocus = string.Equals(focusedElement?.Name, "NoteTextBox", StringComparison.Ordinal);

            if (!noteHasFocus && _lastForegroundWindow != IntPtr.Zero)
            {
                NativeMethods.SetForegroundWindow(_lastForegroundWindow);
            }
        }

        private async void FiveMButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button button || button.Tag is not OverlayButton preset)
            {
                return;
            }

            try
            {
                // Temporarily disable our window from stealing focus
                if (_windowHandle != IntPtr.Zero)
                {
                    var ourExStyle = NativeMethods.GetWindowLong(_windowHandle, NativeMethods.GWL_EXSTYLE);
                    if ((ourExStyle & NativeMethods.WS_EX_NOACTIVATE) == 0)
                    {
                        ourExStyle |= NativeMethods.WS_EX_NOACTIVATE;
                        NativeMethods.SetWindowLong(_windowHandle, NativeMethods.GWL_EXSTYLE, ourExStyle);
                        DebugLogger.Log("Ensured NOACTIVATE is set during operation");
                    }
                }
                
                // Find target window
                var targetWindow = FindFiveMWindow();
                if (targetWindow == IntPtr.Zero)
                {
                    var targetApp = _debugMode ? "Notepad" : "FiveM";
                    MessageDialogService.Show(
                        $"Kunde inte hitta {targetApp}-fönstret. Se till att {targetApp} körs.",
                        $"{targetApp} ej funnet",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                DebugLogger.Log($"Found target window: {targetWindow}");

                // Get the process ID and thread ID
                uint targetThreadId = NativeMethods.GetWindowThreadProcessId(targetWindow, out uint processId);
                uint currentThreadId = NativeMethods.GetCurrentThreadId();
                
                DebugLogger.Log($"Target thread: {targetThreadId}, Current thread: {currentThreadId}, Process ID: {processId}");
                
                // Attach our thread input to the target window thread
                // This allows us to set focus more reliably
                bool attached = false;
                if (targetThreadId != currentThreadId)
                {
                    attached = NativeMethods.AttachThreadInput(currentThreadId, targetThreadId, true);
                    DebugLogger.Log($"AttachThreadInput result: {attached}");
                }
                
                // Allow our process to set foreground
                NativeMethods.AllowSetForegroundWindow(processId);
                
                // Restore the window if minimized
                NativeMethods.ShowWindow(targetWindow, NativeMethods.SW_RESTORE);
                await Task.Delay(150);

                // Bring window to top
                NativeMethods.BringWindowToTop(targetWindow);
                await Task.Delay(100);
                
                // Set as foreground window
                NativeMethods.SetForegroundWindow(targetWindow);
                await Task.Delay(200);
                
                // Set focus to the window
                NativeMethods.SetFocus(targetWindow);
                await Task.Delay(200);
                
                // Verify the window is now foreground
                var foreground = NativeMethods.GetForegroundWindow();
                DebugLogger.Log($"Foreground window after focus attempts: {foreground} (expected: {targetWindow})");
                
                if (foreground != targetWindow)
                {
                    DebugLogger.Log("Warning: Failed to set foreground. Trying one more time...");
                    NativeMethods.SetForegroundWindow(targetWindow);
                    await Task.Delay(300);
                    NativeMethods.SetFocus(targetWindow);
                    await Task.Delay(200);
                }

                DebugLogger.Log("Window should be active now, sending T key");
                
                // Extra verification and small delay to ensure window is truly ready
                await Task.Delay(100);
                foreground = NativeMethods.GetForegroundWindow();
                if (foreground != targetWindow)
                {
                    DebugLogger.Log($"WARNING: Window lost focus again (now: {foreground}). Final attempt to focus...");
                    NativeMethods.SetForegroundWindow(targetWindow);
                    await Task.Delay(200);
                }

                // In debug mode with Notepad, use Clipboard + Ctrl+V method
                if (_debugMode)
                {
                    DebugLogger.Log("Debug mode: Using Clipboard + Ctrl+V method");
                    DebugLogger.Log($"Text to send: '{preset.Text}' (length: {preset.Text.Length})");
                    
                    // Make absolutely sure Notepad is ready
                    await Task.Delay(500);
                    
                    DebugLogger.Log("Sending T key...");
                    SendKeyPress(NativeMethods.VK_T);
                    await Task.Delay(300);
                    DebugLogger.Log("T key sent");
                    
                    // Save current clipboard content
                    string? originalClipboard = null;
                    try
                    {
                        originalClipboard = System.Windows.Clipboard.GetText();
                        DebugLogger.Log($"Original clipboard saved: '{originalClipboard?.Substring(0, Math.Min(50, originalClipboard?.Length ?? 0))}'");
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log($"Could not save original clipboard: {ex.Message}");
                    }
                    
                    try
                    {
                        // Put our text in clipboard - try multiple times to ensure it works
                        bool clipboardSet = false;
                        for (int i = 0; i < 3 && !clipboardSet; i++)
                        {
                            try
                            {
                                DebugLogger.Log($"Clipboard attempt {i + 1}: Clearing...");
                                System.Windows.Clipboard.Clear();
                                await Task.Delay(50);
                                
                                DebugLogger.Log($"Clipboard attempt {i + 1}: Setting text...");
                                System.Windows.Clipboard.SetText(preset.Text);
                                await Task.Delay(50);
                                
                                // Verify it was set
                                var verify = System.Windows.Clipboard.GetText();
                                DebugLogger.Log($"Clipboard attempt {i + 1}: Verification read: '{verify?.Substring(0, Math.Min(50, verify?.Length ?? 0))}'");
                                
                                if (verify == preset.Text)
                                {
                                    clipboardSet = true;
                                    DebugLogger.Log($"✓ Clipboard set successfully on attempt {i + 1}");
                                }
                                else
                                {
                                    DebugLogger.Log($"✗ Clipboard verification failed on attempt {i + 1}");
                                }
                            }
                            catch (Exception ex)
                            {
                                DebugLogger.Log($"✗ Clipboard attempt {i + 1} failed: {ex.Message}");
                                await Task.Delay(100);
                            }
                        }
                        
                        if (!clipboardSet)
                        {
                            DebugLogger.Log("ERROR: Failed to set clipboard after 3 attempts");
                            MessageDialogService.Show(
                                "Kunde inte kopiera texten till clipboard. Försök igen.",
                                "Clipboard-fel",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                            return;
                        }
                        
                        // Extra wait to ensure clipboard is ready
                        await Task.Delay(200);
                        
                        // Verify window still has focus
                        var currentForeground = NativeMethods.GetForegroundWindow();
                        DebugLogger.Log($"Before Ctrl+V - Foreground window: {currentForeground} (expected: {targetWindow})");
                        
                        if (currentForeground != targetWindow)
                        {
                            DebugLogger.Log("WARNING: Lost focus, re-focusing...");
                            NativeMethods.SetForegroundWindow(targetWindow);
                            await Task.Delay(200);
                        }
                        
                        DebugLogger.Log("Sending Ctrl+V...");
                        SendCtrlV();
                        await Task.Delay(300);
                        
                        DebugLogger.Log("✓ Ctrl+V sent successfully");
                        
                        // Final verification
                        currentForeground = NativeMethods.GetForegroundWindow();
                        DebugLogger.Log($"After Ctrl+V - Foreground window: {currentForeground}");
                    }
                    finally
                    {
                        // Restore original clipboard if possible
                        if (!string.IsNullOrEmpty(originalClipboard))
                        {
                            try
                            {
                                await Task.Delay(200);
                                System.Windows.Clipboard.SetText(originalClipboard);
                                DebugLogger.Log("Original clipboard restored");
                            }
                            catch (Exception ex)
                            {
                                DebugLogger.Log($"Could not restore clipboard: {ex.Message}");
                            }
                        }
                    }
                }
                else
                {
                    // For FiveM, use SendInput (original method)
                    DebugLogger.Log("FiveM mode: Sending T key to open chat");
                    SendKeyPress(NativeMethods.VK_T);
                    
                    // Wait longer for FiveM's chat window to open
                    await Task.Delay(500);
                    DebugLogger.Log("T key sent, waiting for chat to open");
                    
                    // Verify focus again
                    foreground = NativeMethods.GetForegroundWindow();
                    DebugLogger.Log($"After T key - Foreground window: {foreground} (expected: {targetWindow})");
                    
                    if (foreground != targetWindow)
                    {
                        DebugLogger.Log("WARNING: Lost focus after T key, attempting to regain focus...");
                        NativeMethods.SetForegroundWindow(targetWindow);
                        await Task.Delay(200);
                    }
                    
                    DebugLogger.Log($"Sending text: {preset.Text}");
                    SendText(preset.Text);
                    
                    // Wait for text to be sent before detaching
                    await Task.Delay(200);
                }
                
                DebugLogger.Log("Text sent successfully");
                
                // Detach thread input AFTER everything is done
                if (attached)
                {
                    NativeMethods.AttachThreadInput(currentThreadId, targetThreadId, false);
                    DebugLogger.Log("Detached thread input");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex);
                MessageDialogService.Show(
                    $"Ett fel uppstod: {ex.Message}",
                    "Fel",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private IntPtr FindFiveMWindow()
        {
            IntPtr foundWindow = IntPtr.Zero;

            NativeMethods.EnumWindows((hWnd, lParam) =>
            {
                if (!NativeMethods.IsWindowVisible(hWnd))
                    return true;

                int length = NativeMethods.GetWindowTextLength(hWnd);
                if (length == 0)
                    return true;

                var builder = new System.Text.StringBuilder(length + 1);
                NativeMethods.GetWindowText(hWnd, builder, builder.Capacity);
                var title = builder.ToString();

                // In debug mode, look for Notepad instead of FiveM
                if (_debugMode)
                {
                    if (title.Contains("Notepad", StringComparison.OrdinalIgnoreCase) ||
                        title.Contains("Anteckningar", StringComparison.OrdinalIgnoreCase))
                    {
                        foundWindow = hWnd;
                        DebugLogger.Log($"Debug mode: Found Notepad window: {title}");
                        return false; // Stop enumeration
                    }
                }
                else
                {
                    // FiveM windows typically contain "FiveM" or "GTA" in their title
                    if (title.Contains("FiveM", StringComparison.OrdinalIgnoreCase) ||
                        title.Contains("Grand Theft Auto V", StringComparison.OrdinalIgnoreCase))
                    {
                        foundWindow = hWnd;
                        return false; // Stop enumeration
                    }
                }

                return true; // Continue enumeration
            }, IntPtr.Zero);

            return foundWindow;
        }

        private IntPtr FindNotepadEditControl(IntPtr notepadWindow)
        {
            DebugLogger.Log("Searching for Notepad Edit control...");
            
            // Notepad has a simple structure: Main window -> Edit control (class "Edit")
            IntPtr editControl = NativeMethods.FindWindowEx(notepadWindow, IntPtr.Zero, "Edit", null);
            
            if (editControl == IntPtr.Zero)
            {
                DebugLogger.Log("Edit control not found directly, searching deeper...");
                // Sometimes there might be intermediate windows, search all children
                IntPtr child = NativeMethods.FindWindowEx(notepadWindow, IntPtr.Zero, null, null);
                while (child != IntPtr.Zero && editControl == IntPtr.Zero)
                {
                    editControl = NativeMethods.FindWindowEx(child, IntPtr.Zero, "Edit", null);
                    if (editControl != IntPtr.Zero)
                    {
                        DebugLogger.Log($"Found Edit control in child window: {editControl}");
                        break;
                    }
                    child = NativeMethods.FindWindowEx(notepadWindow, child, null, null);
                }
            }
            else
            {
                DebugLogger.Log($"Found Edit control directly: {editControl}");
            }
            
            return editControl;
        }

        private void SendKeyPress(byte virtualKeyCode)
        {
            DebugLogger.Log($"SendKeyPress: Using keybd_event for VK {virtualKeyCode}");
            
            // Send key down
            NativeMethods.keybd_event(virtualKeyCode, 0, 0, UIntPtr.Zero);
            System.Threading.Thread.Sleep(10);
            
            // Send key up
            NativeMethods.keybd_event(virtualKeyCode, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
            
            DebugLogger.Log($"SendKeyPress: Sent key press for VK {virtualKeyCode}");
        }

        private void SendCtrlV()
        {
            DebugLogger.Log("SendCtrlV: Using keybd_event for Ctrl+V");
            
            // Press Ctrl down
            NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, 0, UIntPtr.Zero);
            System.Threading.Thread.Sleep(10);
            
            // Press V down
            NativeMethods.keybd_event(NativeMethods.VK_V, 0, 0, UIntPtr.Zero);
            System.Threading.Thread.Sleep(10);
            
            // Release V
            NativeMethods.keybd_event(NativeMethods.VK_V, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
            System.Threading.Thread.Sleep(10);
            
            // Release Ctrl
            NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
            
            DebugLogger.Log("SendCtrlV: Sent Ctrl+V key presses");
        }

        private void SendKey(char key)
        {
            // Send key down
            var input = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                u = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = key,
                        dwFlags = NativeMethods.KEYEVENTF_UNICODE,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            // Send key up
            var inputUp = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                u = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = key,
                        dwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            var inputs = new[] { input, inputUp };
            NativeMethods.SendInput((uint)inputs.Length, inputs, System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.INPUT)));
        }

        private void SendText(string text)
        {
            DebugLogger.Log($"SendText: Starting to send {text.Length} characters");
            foreach (char c in text)
            {
                SendKey(c);
                System.Threading.Thread.Sleep(15); // Slightly longer delay for reliability
            }
            DebugLogger.Log("SendText: Completed");
        }

        private void SendMessageKey(IntPtr targetWindow, char key)
        {
            DebugLogger.Log($"SendMessageKey: Sending key '{key}' to window {targetWindow}");
            
            // Send WM_KEYDOWN
            NativeMethods.SendMessage(targetWindow, NativeMethods.WM_KEYDOWN, (IntPtr)key, IntPtr.Zero);
            System.Threading.Thread.Sleep(50);
            
            // Send WM_CHAR
            NativeMethods.SendMessage(targetWindow, NativeMethods.WM_CHAR, (IntPtr)key, IntPtr.Zero);
            System.Threading.Thread.Sleep(50);
            
            // Send WM_KEYUP
            NativeMethods.SendMessage(targetWindow, NativeMethods.WM_KEYUP, (IntPtr)key, IntPtr.Zero);
            
            DebugLogger.Log($"SendMessageKey: Completed for '{key}'");
        }

        private void SendTextViaMessage(IntPtr targetWindow, string text)
        {
            DebugLogger.Log($"SendTextViaMessage: Starting to send {text.Length} characters to window {targetWindow}");
            
            foreach (char c in text)
            {
                DebugLogger.Log($"SendTextViaMessage: Sending '{c}'");
                
                // For most text input, WM_CHAR is sufficient
                NativeMethods.SendMessage(targetWindow, NativeMethods.WM_CHAR, (IntPtr)c, IntPtr.Zero);
                System.Threading.Thread.Sleep(20); // Small delay between characters
            }
            
            DebugLogger.Log("SendTextViaMessage: Completed");
        }

        private void NoteTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // When textbox gets focus, temporarily allow window activation
            if (_windowHandle == IntPtr.Zero)
            {
                return;
            }

            DebugLogger.Log("NoteTextBox got focus, enabling window activation");

            var exStyle = NativeMethods.GetWindowLong(_windowHandle, NativeMethods.GWL_EXSTYLE);
            exStyle &= ~NativeMethods.WS_EX_NOACTIVATE;
            NativeMethods.SetWindowLong(_windowHandle, NativeMethods.GWL_EXSTYLE, exStyle);
            
            // Activate the window so keyboard input works
            NativeMethods.SetForegroundWindow(_windowHandle);
            
            // Make sure the textbox is actually ready for input
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                textBox.IsReadOnly = false;
                DebugLogger.Log("TextBox set to not readonly");
            }
        }

        private void NoteTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            DebugLogger.Log("NoteTextBox lost focus");
            
            // Don't immediately restore no-activate style
            // Let it stay activatable for a moment to allow re-focusing
            Task.Delay(100).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    // Only restore if no textbox currently has focus
                    var focusedElement = Keyboard.FocusedElement as FrameworkElement;
                    var noteHasFocus = string.Equals(focusedElement?.Name, "NoteTextBox", StringComparison.Ordinal);
                    
                    if (!noteHasFocus && _windowHandle != IntPtr.Zero)
                    {
                        var exStyle = NativeMethods.GetWindowLong(_windowHandle, NativeMethods.GWL_EXSTYLE);
                        exStyle |= NativeMethods.WS_EX_NOACTIVATE;
                        NativeMethods.SetWindowLong(_windowHandle, NativeMethods.GWL_EXSTYLE, exStyle);
                        DebugLogger.Log("Restored no-activate style");
                    }
                });
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            Cleanup();
            base.OnClosed(e);
        }

        private void Cleanup()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            // Stop and save before cleanup
            _autoSaveTimer.Stop();
            _mouseCheckTimer.Stop();
            SaveAllNotes();
            SaveWindowSettings(); // Save window position and size
            SaveUserSettings(); // Save user settings including tab states

            if (_windowHandle != IntPtr.Zero)
            {
                NativeMethods.UnregisterHotKey(_windowHandle, _hotkeyId);
            }

            if (_hwndSource is not null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
            }

            _configService.ConfigReloaded -= OnConfigReloaded;
            _configService.Dispose();

            NotesTabControl.Loaded -= NotesTabControl_Loaded;
            NotesTabControl.SelectionChanged -= NotesTabControl_SelectionChanged;
            
            // Cancel any ongoing chat operations
            _chatCancellationTokenSource?.Cancel();
            _chatCancellationTokenSource?.Dispose();
        }
        
        // Chat Properties
        public string ChatInputText
        {
            get => _chatInputText;
            set
            {
                if (_chatInputText == value) return;
                _chatInputText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSendMessage));
            }
        }
        
        public bool CanSendMessage => !_isSendingMessage && !string.IsNullOrWhiteSpace(_chatInputText) && _chatService.IsConfigured;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void InitializeNoteTabs()
        {
            // Find the TabControl by name
            var tabControl = this.FindName("NotesTabControl") as System.Windows.Controls.TabControl;
            if (tabControl == null)
            {
                DebugLogger.Log("NotesTabControl not found in XAML");
                return;
            }

            // Load all notes from the Notes directory (excluding Archive folder)
            var noteIds = _noteService.ListNotes();
            
            if (noteIds.Count > 0)
            {
                DebugLogger.Log($"Loading {noteIds.Count} notes from disk");
                foreach (var noteId in noteIds)
                {
                    var note = _noteService.LoadNote(noteId);
                    if (note != null)
                    {
                        AddNoteTab(note.Name, note.Content, isNewTab: false, isProtectedTab: false, noteId: note.Id);
                    }
                }
            }
            else
            {
                // Create default tab if no notes exist
                DebugLogger.Log("No saved notes found, creating default tab");
                AddNoteTab("Anteckningar", string.Empty, false, isProtectedTab: false);
            }

            if (tabControl.Items.Count > 0)
            {
                tabControl.SelectedIndex = 0;
            }
        }

        private void LoadNoteContent(NoteTab noteTab)
        {
            try
            {
                // First try to load by ID
                var note = _noteService.LoadNote(noteTab.Id);
                
                // If not found by ID, try loading by the tab name (for backward compatibility)
                if (note == null)
                {
                    note = _noteService.LoadNote(noteTab.Name);
                }
                
                if (note != null)
                {
                    noteTab.Id = note.Id;
                    noteTab.Content = note.Content;
                    noteTab.DisplayName = note.Name;
                    noteTab.HasCustomName = note.HasCustomName;
                    noteTab.CreatedDate = note.CreatedDate;
                    noteTab.LastModifiedDate = note.LastModifiedDate;
                    noteTab.SortOrder = note.SortOrder;
                    noteTab.IsContextExcluded = note.ExcludeFromContext;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex);
            }
        }

        private void SaveNoteContent(NoteTab noteTab)
        {
            try
            {
                var note = new Note
                {
                    Id = noteTab.Id,
                    Name = noteTab.DisplayName,
                    Content = noteTab.Content,
                    CreatedDate = noteTab.CreatedDate,
                    LastModifiedDate = DateTime.Now,
                    HasCustomName = noteTab.HasCustomName,
                    SortOrder = noteTab.SortOrder,
                    ExcludeFromContext = noteTab.IsContextExcluded
                };
                _noteService.SaveNote(note);
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex);
            }
        }

        private void SaveAllNotes()
        {
            foreach (var noteTab in _noteTabs.Values)
            {
                SaveNoteContent(noteTab);
            }
        }

        private void DebouncedSaveNote(NoteTab noteTab)
        {
            // Stop existing timer if any
            _saveDebounceTimer?.Stop();
            
            // Create new timer that will save after 2 seconds of inactivity
            _saveDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            
            _saveDebounceTimer.Tick += (s, e) =>
            {
                _saveDebounceTimer.Stop();
                SaveNoteContent(noteTab);
                DebugLogger.Log($"Debounced save for note: {noteTab.DisplayName}");
            };
            
            _saveDebounceTimer.Start();
        }

        private void AutoSaveTimer_Tick(object? sender, EventArgs e)
        {
            SaveAllNotes();
            DebugLogger.Log("Auto-saved all notes");
        }

        private void MouseCheckTimer_Tick(object? sender, EventArgs e)
        {
            // Check if toggle button is pressed
            short buttonState = NativeMethods.GetAsyncKeyState(_interactivityToggleVK);
            bool buttonPressed = (buttonState & 0x8000) != 0;
            
            if (buttonPressed)
            {
                // Toggle interactive mode
                _overlayInteractive = !_overlayInteractive;
                
                // Find the status indicator ellipse
                var statusIndicator = this.FindName("StatusIndicator") as System.Windows.Shapes.Ellipse;
                
                if (_overlayInteractive)
                {
                    // Make overlay interactive
                    SetClickThrough(false);
                    
                    // Visual feedback: Green indicator
                    if (statusIndicator != null)
                    {
                        statusIndicator.Fill = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(0, 255, 0)); // Bright green
                    }
                    
                    // Show cursor
                    this.Cursor = System.Windows.Input.Cursors.Arrow;
                    
                    DebugLogger.Log("🟢 Overlay ACTIVATED - interactive mode ON (green indicator, cursor visible)");
                }
                else
                {
                    // Make overlay click-through
                    SetClickThrough(true);
                    
                    // Visual feedback: Blue indicator
                    if (statusIndicator != null)
                    {
                        statusIndicator.Fill = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(47, 157, 255)); // Blue
                    }
                    
                    // Hide cursor over window
                    this.Cursor = System.Windows.Input.Cursors.None;
                    
                    DebugLogger.Log("🔵 Overlay DEACTIVATED - click-through mode ON (blue indicator, cursor hidden)");
                }
                
                // Wait for button release to avoid multiple toggles
                while ((NativeMethods.GetAsyncKeyState(_interactivityToggleVK) & 0x8000) != 0)
                {
                    System.Threading.Thread.Sleep(10);
                }
            }
            
            // If not in interactive mode, always be click-through
            if (!_overlayInteractive && !_isClickThrough)
            {
                SetClickThrough(true);
            }
        }

        private void SetClickThrough(bool enabled)
        {
            if (_windowHandle == IntPtr.Zero)
                return;

            _isClickThrough = enabled;
            var exStyle = NativeMethods.GetWindowLong(_windowHandle, NativeMethods.GWL_EXSTYLE);
            
            if (enabled)
            {
                // Make window click-through
                exStyle |= NativeMethods.WS_EX_TRANSPARENT;
                DebugLogger.Log("Window set to click-through");
            }
            else
            {
                // Remove click-through
                exStyle &= ~NativeMethods.WS_EX_TRANSPARENT;
                DebugLogger.Log("Window set to normal (not click-through)");
            }
            
            NativeMethods.SetWindowLong(_windowHandle, NativeMethods.GWL_EXSTYLE, exStyle);
        }

        private void ResizeThumbLeft_DragDelta(object sender, DragDeltaEventArgs e)
        {
            // Resize from bottom-left corner (inverted horizontal)
            double newWidth = Width - e.HorizontalChange;
            double newHeight = Height + e.VerticalChange;

            // Apply minimum constraints
            if (newWidth >= MinWidth)
            {
                Width = newWidth;
                // Adjust position to keep the right edge fixed
                Left += e.HorizontalChange;
            }

            if (newHeight >= MinHeight)
            {
                Height = newHeight;
            }
        }

        private void ResizeThumbRight_DragDelta(object sender, DragDeltaEventArgs e)
        {
            // Resize from bottom-right corner
            double newWidth = Width + e.HorizontalChange;
            double newHeight = Height + e.VerticalChange;

            // Apply minimum constraints
            if (newWidth >= MinWidth)
            {
                Width = newWidth;
            }

            if (newHeight >= MinHeight)
            {
                Height = newHeight;
            }
        }
    }
}
