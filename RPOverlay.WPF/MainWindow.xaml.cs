using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using RPOverlay.Core.Models;
using RPOverlay.Core.Services;
using RPOverlay.Core.Utilities;
using RPOverlay.Core.Providers;
using RPOverlay.WPF.Interop;
using RPOverlay.WPF.Logging;

namespace RPOverlay.WPF
{
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

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                DataContext = this;

                DebugLogger.Log("MainWindow constructor: Creating config service...");
                _configService = new OverlayConfigService(new AppDataOverlayConfigPathProvider());
                _configService.ConfigReloaded += OnConfigReloaded;

                Loaded += OnLoaded;
                Unloaded += OnUnloaded;

                ApplyConfig(_configService.Current);
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
            PositionWindow();
            RegisterHotkey();
            HideOverlay();
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
    }
}