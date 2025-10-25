using System;
using System.IO;

namespace RPOverlay.Core.Abstractions;

/// <summary>
/// Provides paths for profile-specific resources.
/// </summary>
public interface IProfilePathProvider
{
    /// <summary>
    /// Gets the current active profile ID.
    /// </summary>
    string CurrentProfileId { get; }
    
    /// <summary>
    /// Gets the config file path for the current profile.
    /// </summary>
    string GetConfigFilePath();
    
    /// <summary>
    /// Gets the notes directory for the current profile.
    /// </summary>
    string GetNotesDirectory();
    
    /// <summary>
    /// Gets the base profiles directory.
    /// </summary>
    string GetProfilesDirectory();
    
    /// <summary>
    /// Sets the active profile.
    /// </summary>
    void SetActiveProfile(string profileId);
}
