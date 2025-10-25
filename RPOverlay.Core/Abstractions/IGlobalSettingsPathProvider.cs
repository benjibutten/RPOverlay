using System;
using System.IO;

namespace RPOverlay.Core.Abstractions;

/// <summary>
/// Provides paths for global application settings (not profile-specific).
/// </summary>
public interface IGlobalSettingsPathProvider
{
    /// <summary>
    /// Gets the global settings file path.
    /// </summary>
    string GetSettingsFilePath();
    
    /// <summary>
    /// Gets the global prompts directory.
    /// </summary>
    string GetPromptsDirectory();
}
