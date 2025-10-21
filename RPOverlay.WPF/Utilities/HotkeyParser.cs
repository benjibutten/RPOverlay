using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace RPOverlay.WPF.Utilities
{
    internal static class HotkeyParser
    {
        private static readonly Dictionary<string, ModifierKeys> ModifierLookup = new(StringComparer.OrdinalIgnoreCase)
        {
            ["CTRL"] = ModifierKeys.Control,
            ["CONTROL"] = ModifierKeys.Control,
            ["ALT"] = ModifierKeys.Alt,
            ["SHIFT"] = ModifierKeys.Shift,
            ["WIN"] = ModifierKeys.Windows,
            ["WINDOWS"] = ModifierKeys.Windows,
            ["CMD"] = ModifierKeys.Windows
        };

        public static bool TryParse(string value, out ModifierKeys modifiers, out Key key)
        {
            modifiers = ModifierKeys.None;
            key = Key.None;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var tokens = value
                .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (tokens.Count == 0)
            {
                return false;
            }

            for (var i = tokens.Count - 1; i >= 0; i--)
            {
                if (!ModifierLookup.TryGetValue(tokens[i], out var modifier))
                {
                    continue;
                }

                modifiers |= modifier;
                tokens.RemoveAt(i);
            }

            if (tokens.Count != 1)
            {
                return false;
            }

            return Enum.TryParse(tokens[0], true, out key);
        }
    }
}
