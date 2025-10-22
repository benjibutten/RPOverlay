using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using RPOverlay.Core.Abstractions;
using RPOverlay.Core.Models;

namespace RPOverlay.Core.Services;

public sealed class UserSettingsService
{
    private readonly string _settingsPath;

    public UserSettingsService(IOverlayConfigPathProvider pathProvider)
    {
        if (pathProvider == null)
            throw new ArgumentNullException(nameof(pathProvider));

        var configDir = Path.GetDirectoryName(pathProvider.GetConfigFilePath());
        _settingsPath = Path.Combine(configDir!, "settings.ini");
        
        Directory.CreateDirectory(configDir!);
    }

    public UserSettings Current { get; private set; } = UserSettings.CreateDefault();

    public UserSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                Current = UserSettings.CreateDefault();
                Save(Current);
                return Current;
            }

            var settings = new UserSettings();
            var lines = File.ReadAllLines(_settingsPath, Encoding.UTF8);
            var currentSection = "";
            var tabsList = new List<string>();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                    continue;

                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    currentSection = trimmed.Substring(1, trimmed.Length - 2);
                    continue;
                }

                var parts = trimmed.Split(new[] { '=' }, 2);
                if (parts.Length != 2)
                    continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                if (currentSection == "General")
                {
                    switch (key)
                    {
                        case "FontSize":
                            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var fontSize))
                                settings.FontSize = fontSize;
                            break;
                        case "Opacity":
                            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var opacity))
                                settings.Opacity = Math.Clamp(opacity, 0.1, 1.0);
                            break;
                        case "ToggleHotkey":
                            settings.ToggleHotkey = value;
                            break;
                        case "InteractivityToggle":
                            settings.InteractivityToggle = value;
                            break;
                        case "ToggleMouseButton": // Legacy compatibility
                            settings.InteractivityToggle = value;
                            break;
                        case "OpenAiApiKey":
                            settings.OpenAiApiKey = value;
                            break;
                        case "SystemPrompt":
                            settings.SystemPrompt = value;
                            break;
                    }
                }
                else if (currentSection == "Window")
                {
                    switch (key)
                    {
                        case "Width":
                            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var width))
                                settings.WindowWidth = width;
                            break;
                        case "Height":
                            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
                                settings.WindowHeight = height;
                            break;
                        case "Left":
                            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var left))
                                settings.WindowLeft = left;
                            break;
                        case "Top":
                            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var top))
                                settings.WindowTop = top;
                            break;
                    }
                }
                else if (currentSection == "Tabs")
                {
                    if (key.StartsWith("Tab"))
                    {
                        tabsList.Add(value);
                    }
                }
            }

            if (tabsList.Count > 0)
                settings.OpenTabs = tabsList;

            Current = settings;
            return settings;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load user settings: {ex}");
            Current = UserSettings.CreateDefault();
            return Current;
        }
    }

    public void Save(UserSettings settings)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("; RPOverlay User Settings");
            sb.AppendLine("; This file is automatically generated");
            sb.AppendLine();
            
            sb.AppendLine("[General]");
            sb.AppendLine($"FontSize={settings.FontSize.ToString(CultureInfo.InvariantCulture)}");
            sb.AppendLine($"Opacity={settings.Opacity.ToString(CultureInfo.InvariantCulture)}");
            sb.AppendLine($"ToggleHotkey={settings.ToggleHotkey}");
            sb.AppendLine($"InteractivityToggle={settings.InteractivityToggle}");
            sb.AppendLine($"OpenAiApiKey={settings.OpenAiApiKey}");
            sb.AppendLine($"SystemPrompt={settings.SystemPrompt}");
            sb.AppendLine();
            
            sb.AppendLine("[Window]");
            sb.AppendLine($"Width={settings.WindowWidth.ToString(CultureInfo.InvariantCulture)}");
            sb.AppendLine($"Height={settings.WindowHeight.ToString(CultureInfo.InvariantCulture)}");
            sb.AppendLine($"Left={settings.WindowLeft.ToString(CultureInfo.InvariantCulture)}");
            sb.AppendLine($"Top={settings.WindowTop.ToString(CultureInfo.InvariantCulture)}");
            sb.AppendLine();
            
            sb.AppendLine("[Tabs]");
            for (int i = 0; i < settings.OpenTabs.Count; i++)
            {
                sb.AppendLine($"Tab{i + 1}={settings.OpenTabs[i]}");
            }

            File.WriteAllText(_settingsPath, sb.ToString(), Encoding.UTF8);
            Current = settings;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save user settings: {ex}");
        }
    }
}
