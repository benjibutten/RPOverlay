using System;

namespace RPOverlay.Core.Models;

/// <summary>
/// Represents a user profile with its own set of notes and commands.
/// </summary>
public sealed class Profile
{
    /// <summary>
    /// Unique identifier for the profile. Used as folder name.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name of the profile.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// When the profile was created.
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    
    /// <summary>
    /// When the profile was last used.
    /// </summary>
    public DateTime LastUsedDate { get; set; } = DateTime.Now;
}
