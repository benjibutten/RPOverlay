using System.Collections.Generic;

namespace RPOverlay.Core.Models;

public sealed class UserSettings
{
    public double FontSize { get; set; } = 16.0;
    public double Opacity { get; set; } = 0.8;
    public string ToggleHotkey { get; set; } = "F9";
    public string InteractivityToggle { get; set; } = "XButton2"; // Can be mouse button or key
    public bool UseMiddleClickAsPrimary { get; set; } = false;
    public double WindowWidth { get; set; } = 320;
    public double WindowHeight { get; set; } = 600;
    public double WindowLeft { get; set; } = -1; // -1 means not set
    public double WindowTop { get; set; } = -1; // -1 means not set
    public List<string> OpenTabs { get; set; } = new();
    
    // OpenAI Settings
    public string OpenAiApiKey { get; set; } = string.Empty;
    /// <summary>
    /// Name of the active prompt (filename without .yaml extension).
    /// </summary>
    public string ActivePromptName { get; set; } = "default";

    /// <summary>
    /// Enables appending note tab context to chat messages.
    /// </summary>
    public bool EnableTabContext { get; set; } = false;

    public static UserSettings CreateDefault() => new()
    {
        FontSize = 16.0,
        Opacity = 0.8,
        ToggleHotkey = "F9",
        InteractivityToggle = "XButton2",
    UseMiddleClickAsPrimary = false,
        WindowWidth = 320,
        WindowHeight = 600,
        WindowLeft = -1,
        WindowTop = -1,
        OpenTabs = new List<string> { "Anteckningar" },
        OpenAiApiKey = string.Empty,
        ActivePromptName = "default",
        EnableTabContext = false
    };
}
