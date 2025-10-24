using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using RPOverlay.WPF.Interop;

namespace RPOverlay.WPF.Utilities;

internal static class MouseClickOverrideManager
{
    private static bool _useMiddleClick;
    private static bool _initialized;
    private static readonly Dictionary<int, int> _syntheticAllowance = new();

    public static void SetMode(bool useMiddleClick)
    {
        EnsureInitialized();
        _useMiddleClick = useMiddleClick;

        if (!useMiddleClick)
        {
            _syntheticAllowance.Clear();
        }
    }

    public static void Register(Window window)
    {
        if (window == null) throw new ArgumentNullException(nameof(window));
        EnsureInitialized();
    }

    private static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        ComponentDispatcher.ThreadPreprocessMessage += ThreadPreprocessMessage;
        _initialized = true;
    }

    private static void ThreadPreprocessMessage(ref MSG msg, ref bool handled)
    {
        ProcessMessage(ref msg, ref handled);
    }

    private static void ProcessMessage(ref MSG msg, ref bool handled)
    {
        if (!_useMiddleClick)
        {
            return;
        }

        switch (msg.message)
        {
            case NativeMethods.WM_LBUTTONDOWN:
            case NativeMethods.WM_LBUTTONUP:
            case NativeMethods.WM_LBUTTONDBLCLK:
            case NativeMethods.WM_NCLBUTTONDOWN:
            case NativeMethods.WM_NCLBUTTONUP:
            case NativeMethods.WM_NCLBUTTONDBLCLK:
                if (ShouldBlockMessage(msg.message))
                {
                    handled = true;
                }
                return;

            case NativeMethods.WM_MBUTTONDOWN:
                handled = true;
                SendLeftButtonEvent(isDown: true);
                return;

            case NativeMethods.WM_MBUTTONUP:
                handled = true;
                SendLeftButtonEvent(isDown: false);
                return;

            case NativeMethods.WM_MBUTTONDBLCLK:
                handled = true;
                AllowSynthetic(NativeMethods.WM_LBUTTONDBLCLK);
                NativeMethods.PostMessage(msg.hwnd, NativeMethods.WM_LBUTTONDBLCLK, AdjustWParam(msg.wParam, includeLeftFlag: true), msg.lParam);
                return;

            case NativeMethods.WM_NCMBUTTONDOWN:
                handled = true;
                AllowSynthetic(NativeMethods.WM_NCLBUTTONDOWN);
                NativeMethods.PostMessage(msg.hwnd, NativeMethods.WM_NCLBUTTONDOWN, msg.wParam, msg.lParam);
                return;

            case NativeMethods.WM_NCMBUTTONUP:
                handled = true;
                AllowSynthetic(NativeMethods.WM_NCLBUTTONUP);
                NativeMethods.PostMessage(msg.hwnd, NativeMethods.WM_NCLBUTTONUP, msg.wParam, msg.lParam);
                NativeMethods.ReleaseCapture();
                return;

            case NativeMethods.WM_NCMBUTTONDBLCLK:
                handled = true;
                AllowSynthetic(NativeMethods.WM_NCLBUTTONDBLCLK);
                NativeMethods.PostMessage(msg.hwnd, NativeMethods.WM_NCLBUTTONDBLCLK, msg.wParam, msg.lParam);
                return;

            case NativeMethods.WM_MOUSEMOVE:
                return;
        }
    }

    private static IntPtr AdjustWParam(IntPtr original, bool includeLeftFlag)
    {
        var flags = original.ToInt64();
        flags &= ~(NativeMethods.MK_MBUTTON | NativeMethods.MK_LBUTTON);

        if (includeLeftFlag)
        {
            flags |= NativeMethods.MK_LBUTTON;
        }

        return new IntPtr(flags);
    }

    private static void AllowSynthetic(int message)
    {
        if (_syntheticAllowance.TryGetValue(message, out var count))
        {
            _syntheticAllowance[message] = count + 1;
        }
        else
        {
            _syntheticAllowance[message] = 1;
        }
    }

    private static bool ShouldBlockMessage(int message)
    {
        if (_syntheticAllowance.TryGetValue(message, out var count) && count > 0)
        {
            if (count == 1)
            {
                _syntheticAllowance.Remove(message);
            }
            else
            {
                _syntheticAllowance[message] = count - 1;
            }

            return false;
        }

        return true;
    }

    private static void SendLeftButtonEvent(bool isDown)
    {
        var message = isDown ? NativeMethods.WM_LBUTTONDOWN : NativeMethods.WM_LBUTTONUP;
        AllowSynthetic(message);

        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            u = new NativeMethods.InputUnion
            {
                mi = new NativeMethods.MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = 0,
                    dwFlags = isDown ? NativeMethods.MOUSEEVENTF_LEFTDOWN : NativeMethods.MOUSEEVENTF_LEFTUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        var inputs = new[] { input };
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());

    }
}
