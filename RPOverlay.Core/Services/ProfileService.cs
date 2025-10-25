using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RPOverlay.Core.Models;

namespace RPOverlay.Core.Services;

/// <summary>
/// Manages user profiles for RP Overlay.
/// Each profile has its own notes and commands stored in a separate directory.
/// </summary>
public sealed class ProfileService
{
    private readonly string _profilesDirectory;
    private readonly string _profilesMetadataFile;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public ProfileService(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
            throw new ArgumentException("Base directory cannot be empty", nameof(baseDirectory));

        _profilesDirectory = baseDirectory;
        _profilesMetadataFile = Path.Combine(_profilesDirectory, "profiles.json");
        
        Directory.CreateDirectory(_profilesDirectory);
        EnsureDefaultProfile();
    }

    /// <summary>
    /// Gets the base profiles directory.
    /// </summary>
    public string ProfilesDirectory => _profilesDirectory;

    /// <summary>
    /// Ensures that a default profile exists.
    /// </summary>
    private void EnsureDefaultProfile()
    {
        var profiles = LoadProfiles();
        if (profiles.Count == 0)
        {
            var defaultProfile = new Profile
            {
                Id = "default",
                Name = "Standard",
                CreatedDate = DateTime.Now,
                LastUsedDate = DateTime.Now
            };
            
            profiles.Add(defaultProfile);
            SaveProfiles(profiles);
            CreateProfileDirectory(defaultProfile.Id);
        }
    }

    /// <summary>
    /// Gets the directory path for a specific profile.
    /// </summary>
    public string GetProfileDirectory(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException("Profile ID cannot be empty", nameof(profileId));

        return Path.Combine(_profilesDirectory, profileId);
    }

    /// <summary>
    /// Gets the notes directory for a specific profile.
    /// </summary>
    public string GetProfileNotesDirectory(string profileId)
    {
        return Path.Combine(GetProfileDirectory(profileId), "notes");
    }

    /// <summary>
    /// Gets the config file path for a specific profile.
    /// </summary>
    public string GetProfileConfigPath(string profileId)
    {
        return Path.Combine(GetProfileDirectory(profileId), "presets.json");
    }

    /// <summary>
    /// Creates a directory structure for a profile.
    /// </summary>
    private void CreateProfileDirectory(string profileId)
    {
        var profileDir = GetProfileDirectory(profileId);
        var notesDir = GetProfileNotesDirectory(profileId);
        
        Directory.CreateDirectory(profileDir);
        Directory.CreateDirectory(notesDir);
        Directory.CreateDirectory(Path.Combine(notesDir, "archive"));
    }

    /// <summary>
    /// Loads all profiles from disk.
    /// </summary>
    public List<Profile> LoadProfiles()
    {
        try
        {
            if (!File.Exists(_profilesMetadataFile))
                return new List<Profile>();

            var json = File.ReadAllText(_profilesMetadataFile);
            var profiles = JsonSerializer.Deserialize<List<Profile>>(json, _serializerOptions);
            return profiles ?? new List<Profile>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load profiles: {ex.Message}");
            return new List<Profile>();
        }
    }

    /// <summary>
    /// Saves all profiles to disk.
    /// </summary>
    private void SaveProfiles(List<Profile> profiles)
    {
        try
        {
            var json = JsonSerializer.Serialize(profiles, _serializerOptions);
            File.WriteAllText(_profilesMetadataFile, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save profiles: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a new profile.
    /// </summary>
    public Profile CreateProfile(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Profile name cannot be empty", nameof(name));

        var profiles = LoadProfiles();
        
        // Generate a unique ID based on the name
        var baseId = GenerateProfileId(name);
        var id = baseId;
        var counter = 1;
        
        while (profiles.Any(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
        {
            id = $"{baseId}_{counter}";
            counter++;
        }

        var profile = new Profile
        {
            Id = id,
            Name = name,
            CreatedDate = DateTime.Now,
            LastUsedDate = DateTime.Now
        };

        profiles.Add(profile);
        SaveProfiles(profiles);
        CreateProfileDirectory(profile.Id);

        return profile;
    }

    /// <summary>
    /// Generates a safe profile ID from a name.
    /// </summary>
    private string GenerateProfileId(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var id = string.Join("_", name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        id = id.Replace(" ", "_").ToLowerInvariant();
        
        if (string.IsNullOrWhiteSpace(id))
            id = "profile";
            
        return id;
    }

    /// <summary>
    /// Updates a profile's metadata.
    /// </summary>
    public bool UpdateProfile(Profile profile)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));

        var profiles = LoadProfiles();
        var existingProfile = profiles.FirstOrDefault(p => p.Id == profile.Id);
        
        if (existingProfile == null)
            return false;

        existingProfile.Name = profile.Name;
        existingProfile.LastUsedDate = DateTime.Now;

        SaveProfiles(profiles);
        return true;
    }

    /// <summary>
    /// Deletes a profile and its data.
    /// </summary>
    public bool DeleteProfile(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException("Profile ID cannot be empty", nameof(profileId));

        // Don't allow deleting the default profile
        if (profileId.Equals("default", StringComparison.OrdinalIgnoreCase))
            return false;

        var profiles = LoadProfiles();
        var profile = profiles.FirstOrDefault(p => p.Id == profileId);
        
        if (profile == null)
            return false;

        profiles.Remove(profile);
        SaveProfiles(profiles);

        // Delete the profile directory
        try
        {
            var profileDir = GetProfileDirectory(profileId);
            if (Directory.Exists(profileDir))
            {
                Directory.Delete(profileDir, true);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to delete profile directory: {ex.Message}");
        }

        return true;
    }

    /// <summary>
    /// Gets a profile by ID.
    /// </summary>
    public Profile? GetProfile(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return null;

        var profiles = LoadProfiles();
        return profiles.FirstOrDefault(p => p.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Updates the last used date for a profile.
    /// </summary>
    public void UpdateLastUsedDate(string profileId)
    {
        var profiles = LoadProfiles();
        var profile = profiles.FirstOrDefault(p => p.Id == profileId);
        
        if (profile != null)
        {
            profile.LastUsedDate = DateTime.Now;
            SaveProfiles(profiles);
        }
    }
}
