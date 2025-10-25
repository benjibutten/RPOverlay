using System;
using System.IO;
using RPOverlay.Core.Abstractions;

namespace RPOverlay.Core.Providers;

/// <summary>
/// Provides paths for global application settings (not profile-specific).
/// </summary>
public sealed class GlobalSettingsPathProvider : IGlobalSettingsPathProvider
{
    private readonly string _baseDirectory;

    public GlobalSettingsPathProvider()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _baseDirectory = Path.Combine(appData, "RPOverlay");
        Directory.CreateDirectory(_baseDirectory);
    }

    /// <summary>
    /// Gets the global settings file path.
    /// </summary>
    public string GetSettingsFilePath()
    {
        return Path.Combine(_baseDirectory, "settings.ini");
    }

    /// <summary>
    /// Gets the global prompts directory.
    /// </summary>
    public string GetPromptsDirectory()
    {
        var promptsDir = Path.Combine(_baseDirectory, "prompts");
        Directory.CreateDirectory(promptsDir);
        return promptsDir;
    }
}
