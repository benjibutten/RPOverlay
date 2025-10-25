using System;
using System.IO;
using RPOverlay.Core.Abstractions;
using RPOverlay.Core.Services;

namespace RPOverlay.Core.Providers;

/// <summary>
/// Provides profile-aware paths for overlay configuration and notes.
/// </summary>
public sealed class ProfilePathProvider : IProfilePathProvider, IOverlayConfigPathProvider
{
    private readonly ProfileService _profileService;
    private string _currentProfileId;

    public ProfilePathProvider()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var baseDirectory = Path.Combine(appData, "RPOverlay", "Profiles");
        
        // Migrate old data BEFORE creating ProfileService
        MigrateOldDataIfNeeded(appData, baseDirectory);
        
        _profileService = new ProfileService(baseDirectory);
        
        // Load the last used profile or default
        _currentProfileId = LoadLastUsedProfile();
    }

    /// <summary>
    /// Gets the current active profile ID.
    /// </summary>
    public string CurrentProfileId => _currentProfileId;

    /// <summary>
    /// Gets the config file path for the current profile.
    /// </summary>
    public string GetConfigFilePath()
    {
        return _profileService.GetProfileConfigPath(_currentProfileId);
    }

    /// <summary>
    /// Gets the notes directory for the current profile.
    /// </summary>
    public string GetNotesDirectory()
    {
        return _profileService.GetProfileNotesDirectory(_currentProfileId);
    }

    /// <summary>
    /// Gets the base profiles directory.
    /// </summary>
    public string GetProfilesDirectory()
    {
        return _profileService.ProfilesDirectory;
    }

    /// <summary>
    /// Sets the active profile and saves it as the last used.
    /// </summary>
    public void SetActiveProfile(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException("Profile ID cannot be empty", nameof(profileId));

        var profile = _profileService.GetProfile(profileId);
        if (profile == null)
            throw new InvalidOperationException($"Profile '{profileId}' not found");

        _currentProfileId = profileId;
        SaveLastUsedProfile(profileId);
        _profileService.UpdateLastUsedDate(profileId);
    }

    /// <summary>
    /// Gets the ProfileService instance.
    /// </summary>
    public ProfileService GetProfileService()
    {
        return _profileService;
    }

    /// <summary>
    /// Loads the last used profile from settings or returns default.
    /// </summary>
    private string LoadLastUsedProfile()
    {
        try
        {
            var settingsFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RPOverlay",
                "app-settings.json"
            );

            if (File.Exists(settingsFile))
            {
                var json = File.ReadAllText(settingsFile);
                var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);
                
                if (!string.IsNullOrWhiteSpace(settings?.LastUsedProfile))
                {
                    // Verify the profile exists
                    var profile = _profileService.GetProfile(settings.LastUsedProfile);
                    if (profile != null)
                        return settings.LastUsedProfile;
                }
            }
        }
        catch
        {
            // Fall back to default
        }

        return "default";
    }

    /// <summary>
    /// Saves the last used profile to settings.
    /// </summary>
    private void SaveLastUsedProfile(string profileId)
    {
        try
        {
            var settingsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RPOverlay"
            );
            Directory.CreateDirectory(settingsDir);

            var settingsFile = Path.Combine(settingsDir, "app-settings.json");
            
            AppSettings? settings = null;
            if (File.Exists(settingsFile))
            {
                var json = File.ReadAllText(settingsFile);
                settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);
            }

            settings ??= new AppSettings();
            settings.LastUsedProfile = profileId;

            var newJson = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            File.WriteAllText(settingsFile, newJson);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save last used profile: {ex.Message}");
        }
    }

    /// <summary>
    /// Migrates old data structure to profile-based structure.
    /// </summary>
    private void MigrateOldDataIfNeeded(string appDataRoot, string profilesBaseDir)
    {
        try
        {
            var oldRPOverlayDir = Path.Combine(appDataRoot, "RPOverlay");
            var oldNotesDir = Path.Combine(oldRPOverlayDir, "Notes");
            var oldPresetsFile = Path.Combine(oldRPOverlayDir, "presets.json");
            
            // If Profiles directory already exists, migration has been done
            if (Directory.Exists(profilesBaseDir))
                return;

            System.Diagnostics.Debug.WriteLine("Starting migration of old data to profile structure...");

            // Create the profiles directory structure manually for default profile
            var defaultProfileDir = Path.Combine(profilesBaseDir, "default");
            var newNotesDir = Path.Combine(defaultProfileDir, "notes");
            var newPresetsFile = Path.Combine(defaultProfileDir, "presets.json");
            
            Directory.CreateDirectory(profilesBaseDir);
            Directory.CreateDirectory(defaultProfileDir);
            Directory.CreateDirectory(newNotesDir);
            Directory.CreateDirectory(Path.Combine(newNotesDir, "archive"));

            // Migrate Notes directory
            if (Directory.Exists(oldNotesDir))
            {
                System.Diagnostics.Debug.WriteLine("Migrating old Notes directory to default profile...");
                
                // Copy all files from old Notes to new profile Notes
                foreach (var file in Directory.GetFiles(oldNotesDir, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(oldNotesDir, file);
                    var newFilePath = Path.Combine(newNotesDir, relativePath);
                    
                    var newFileDir = Path.GetDirectoryName(newFilePath);
                    if (!string.IsNullOrEmpty(newFileDir))
                    {
                        Directory.CreateDirectory(newFileDir);
                    }
                    
                    File.Copy(file, newFilePath, overwrite: false);
                    System.Diagnostics.Debug.WriteLine($"Migrated: {relativePath}");
                }
                
                // Rename old Notes directory to Notes_backup
                try
                {
                    var backupPath = Path.Combine(oldRPOverlayDir, "Notes_backup");
                    if (!Directory.Exists(backupPath))
                    {
                        Directory.Move(oldNotesDir, backupPath);
                        System.Diagnostics.Debug.WriteLine("Renamed old Notes to Notes_backup");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not rename old Notes directory: {ex.Message}");
                }
            }

            // Migrate presets.json
            if (File.Exists(oldPresetsFile))
            {
                System.Diagnostics.Debug.WriteLine("Migrating old presets.json to default profile...");
                File.Copy(oldPresetsFile, newPresetsFile, overwrite: false);
                
                // Rename old presets.json to presets_backup.json
                try
                {
                    var backupPath = Path.Combine(oldRPOverlayDir, "presets_backup.json");
                    if (!File.Exists(backupPath))
                    {
                        File.Move(oldPresetsFile, backupPath);
                        System.Diagnostics.Debug.WriteLine("Renamed old presets.json to presets_backup.json");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not rename old presets.json: {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine("Migration completed successfully.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Migration failed (non-critical): {ex.Message}");
        }
    }

    private class AppSettings
    {
        public string LastUsedProfile { get; set; } = "default";
    }
}
