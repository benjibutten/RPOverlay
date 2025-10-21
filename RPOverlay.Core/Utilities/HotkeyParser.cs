using System;
using System.Collections.Generic;
using System.Linq;
using RPOverlay.Core.Models;

namespace RPOverlay.Core.Utilities;

public static class HotkeyParser
{
    private static readonly Dictionary<string, HotkeyModifiers> ModifierLookup = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CTRL"] = HotkeyModifiers.Control,
        ["CONTROL"] = HotkeyModifiers.Control,
        ["ALT"] = HotkeyModifiers.Alt,
        ["SHIFT"] = HotkeyModifiers.Shift,
        ["WIN"] = HotkeyModifiers.Windows,
        ["WINDOWS"] = HotkeyModifiers.Windows,
        ["CMD"] = HotkeyModifiers.Windows
    };

    public static bool TryParse(string value, out HotkeyDefinition? definition)
    {
        definition = null;

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

        var modifiers = HotkeyModifiers.None;

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

        var keyToken = tokens[0].Trim();

        if (string.IsNullOrWhiteSpace(keyToken))
        {
            return false;
        }

        definition = new HotkeyDefinition
        {
            Modifiers = modifiers,
            Key = keyToken.ToUpperInvariant()
        };

        return true;
    }
}
