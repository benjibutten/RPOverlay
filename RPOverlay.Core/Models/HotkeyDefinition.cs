using System;

namespace RPOverlay.Core.Models;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Control = 1,
    Alt = 2,
    Shift = 4,
    Windows = 8
}

public sealed class HotkeyDefinition
{
    public HotkeyModifiers Modifiers { get; init; }
    public string Key { get; init; } = string.Empty;
}
