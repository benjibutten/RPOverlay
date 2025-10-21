using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using System.IO;
using System.Text.Json;
using RPOverlay.Core.Models;
using RPOverlay.Core.Services;
using RPOverlay.Core.Utilities;
using RPOverlay.Core.Providers;
using RPOverlay.WPF.Interop;
using RPOverlay.WPF.Logging;

namespace RPOverlay.WPF
{
    public class NoteTab : INotifyPropertyChanged
    {
        private string _content = string.Empty;
        
        public string Name { get; set; } = string.Empty;
        
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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly OverlayConfigService _configService;
        private readonly ObservableCollection<OverlayButton> _buttons = new();
        private HwndSource? _hwndSource;
        private IntPtr _windowHandle;
        private bool _overlayVisible;
        private ModifierKeys _currentModifiers = ModifierKeys.None;
        private Key _currentKey = Key.F9;
        private readonly int _hotkeyId = 0x1001;
        private IntPtr _lastForegroundWindow = IntPtr.Zero;
        private string _hotkeyHint = "F9";
        private bool _isDisposed;
        private readonly DispatcherTimer _autoSaveTimer;
        private readonly string _notesDirectory;
        private readonly Dictionary<string, NoteTab> _noteTabs = new();

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                DataContext = this;

                DebugLogger.Log("MainWindow constructor: Creating config service...");
                _configService = new OverlayConfigService(new AppDataOverlayConfigPathProvider());
                _configService.ConfigReloaded += OnConfigReloaded;

                // Initialize notes directory
                _notesDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "RPOverlay",
                    "Notes");
                Directory.CreateDirectory(_notesDirectory);

                // Initialize auto-save timer (every 30 seconds)
                _autoSaveTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(30)
                };
                _autoSaveTimer.Tick += AutoSaveTimer_Tick;

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

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            DebugLogger.Log("MainWindow.OnLoaded called");
            _hwndSource = (HwndSource)PresentationSource.FromVisual(this)!;
            _hwndSource.AddHook(WndProc);
            _windowHandle = _hwndSource.Handle;

            DebugLogger.Log($"Window handle: {_windowHandle}");

            ApplyWindowStyles();
            LoadWindowSettings(); // Load saved position and size
            RegisterHotkey();
            HideOverlay();
            _autoSaveTimer.Start(); // Start auto-save timer
            Dispatcher.BeginInvoke(new Action(PositionWindow), DispatcherPriority.Background);
            DebugLogger.Log("OnLoaded: Complete");
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
            }
        }

        private void HideButton_Click(object sender, RoutedEventArgs e)
        {
            HideOverlay();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
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
            var settingsWindow = new SettingsWindow
            {
                Owner = this
            };
            settingsWindow.ShowDialog();
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

            AddNoteTab(tabName, string.Empty, true);
        }

        private void AddNoteTab(string tabName, string initialContent = "", bool isNewTab = false)
        {
            var tabControl = this.FindName("NotesTabControl") as System.Windows.Controls.TabControl;
            if (tabControl == null) return;

            var tab = new TabItem
            {
                Header = tabName
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
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            textBox.GotFocus += NoteTextBox_GotFocus;
            textBox.LostFocus += NoteTextBox_LostFocus;

            var noteTab = new NoteTab { Name = tabName, Content = initialContent };
            _noteTabs[tabName] = noteTab;

            // Load saved content if exists (unless it's a brand new tab)
            if (!isNewTab)
            {
                LoadNoteContent(noteTab);
            }
            textBox.Text = noteTab.Content;

            // Bind textbox to note content and update tab name based on first line
            textBox.TextChanged += (s, e) =>
            {
                if (!_noteTabs.TryGetValue(tabName, out var nt))
                    return;

                nt.Content = textBox.Text;

                // Extract first line as tab name
                var lines = textBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 0)
                {
                    var newName = lines[0].Trim();
                    if (!string.IsNullOrWhiteSpace(newName) && newName.Length <= 20) // Limit tab name length
                    {
                        // Update tab header if name changed
                        if (tab.Header.ToString() != newName && !_noteTabs.ContainsKey(newName))
                        {
                            // Remove old entry and add with new name
                            _noteTabs.Remove(tabName);
                            tabName = newName;
                            _noteTabs[tabName] = nt;
                            nt.Name = newName;
                            tab.Header = newName;
                        }
                    }
                }
            };

            tab.Content = textBox;
            tabControl.Items.Add(tab);
            tabControl.SelectedItem = tab;
            
            // Focus the textbox if it's a new tab
            if (isNewTab)
            {
                textBox.Focus();
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
                // Find FiveM window
                var fivemWindow = FindFiveMWindow();
                if (fivemWindow == IntPtr.Zero)
                {
                    System.Windows.MessageBox.Show(
                        "Kunde inte hitta FiveM-fönstret. Se till att FiveM körs.",
                        "FiveM ej funnet",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Switch to FiveM
                NativeMethods.SetForegroundWindow(fivemWindow);
                await Task.Delay(300); // Wait for window to focus

                // Press T to open chat
                SendKey('T');
                await Task.Delay(150); // Wait for chat to open

                // Type the command
                SendText(preset.Text);
                
                // Optionally press Enter to send the command automatically
                // Uncomment the line below if you want the command to be sent automatically:
                // SendKey(Keys.Enter);
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex);
                System.Windows.MessageBox.Show(
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

                // FiveM windows typically contain "FiveM" or "GTA" in their title
                if (title.Contains("FiveM", StringComparison.OrdinalIgnoreCase) ||
                    title.Contains("Grand Theft Auto V", StringComparison.OrdinalIgnoreCase))
                {
                    foundWindow = hWnd;
                    return false; // Stop enumeration
                }

                return true; // Continue enumeration
            }, IntPtr.Zero);

            return foundWindow;
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

            NativeMethods.SendInput(1, new[] { input }, System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.INPUT)));
            NativeMethods.SendInput(1, new[] { inputUp }, System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.INPUT)));
        }

        private void SendText(string text)
        {
            foreach (char c in text)
            {
                SendKey(c);
                System.Threading.Thread.Sleep(10); // Small delay between characters
            }
        }

        private void NoteTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // When textbox gets focus, temporarily allow window activation
            if (_windowHandle == IntPtr.Zero)
            {
                return;
            }

            var exStyle = NativeMethods.GetWindowLong(_windowHandle, NativeMethods.GWL_EXSTYLE);
            exStyle &= ~NativeMethods.WS_EX_NOACTIVATE;
            NativeMethods.SetWindowLong(_windowHandle, NativeMethods.GWL_EXSTYLE, exStyle);
            
            // Activate the window so keyboard input works
            NativeMethods.SetForegroundWindow(_windowHandle);
        }

        private void NoteTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // When textbox loses focus, restore no-activate style
            if (_windowHandle == IntPtr.Zero)
            {
                return;
            }

            var exStyle = NativeMethods.GetWindowLong(_windowHandle, NativeMethods.GWL_EXSTYLE);
            exStyle |= NativeMethods.WS_EX_NOACTIVATE;
            NativeMethods.SetWindowLong(_windowHandle, NativeMethods.GWL_EXSTYLE, exStyle);
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
            SaveAllNotes();
            SaveWindowSettings(); // Save window position and size

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
        }

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

            // Create default tabs
            var defaultTabs = new[] { "Noteringar", "Patienter", "Händelser" };
            
            foreach (var tabName in defaultTabs)
            {
                AddNoteTab(tabName, string.Empty, false);
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
                var filePath = Path.Combine(_notesDirectory, $"{noteTab.Name}.txt");
                if (File.Exists(filePath))
                {
                    noteTab.Content = File.ReadAllText(filePath);
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
                var filePath = Path.Combine(_notesDirectory, $"{noteTab.Name}.txt");
                File.WriteAllText(filePath, noteTab.Content);
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

        private void AutoSaveTimer_Tick(object? sender, EventArgs e)
        {
            SaveAllNotes();
            DebugLogger.Log("Auto-saved all notes");
        }

        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
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
    }
}