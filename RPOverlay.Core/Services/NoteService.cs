using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using RPOverlay.Core.Models;

namespace RPOverlay.Core.Services;

/// <summary>
/// Manages notes stored as YAML files with metadata.
/// </summary>
public sealed class NoteService
{
    private readonly string _notesDirectory;
    private readonly string _archiveDirectory;

    public NoteService(string notesDirectory)
    {
        if (string.IsNullOrWhiteSpace(notesDirectory))
            throw new ArgumentException("Notes directory cannot be empty", nameof(notesDirectory));

        _notesDirectory = notesDirectory;
        _archiveDirectory = Path.Combine(notesDirectory, "Archive");
        Directory.CreateDirectory(_notesDirectory);
        Directory.CreateDirectory(_archiveDirectory);
    }

    /// <summary>
    /// Gets the notes directory path.
    /// </summary>
    public string NotesDirectory => _notesDirectory;
    
    /// <summary>
    /// Gets the archive directory path.
    /// </summary>
    public string ArchiveDirectory => _archiveDirectory;

    /// <summary>
    /// Loads a note by ID from disk.
    /// </summary>
    public Note? LoadNote(string noteId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(noteId))
                return null;

            var notePath = GetNotePath(noteId);
            if (!File.Exists(notePath))
                return null;

            return ParseYamlNote(notePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load note '{noteId}': {ex}");
            return null;
        }
    }

    /// <summary>
    /// Saves a note to disk as YAML.
    /// </summary>
    public bool SaveNote(Note note)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(note.Id))
                throw new ArgumentException("Note ID cannot be empty", nameof(note));

            // Update last modified date
            note.LastModifiedDate = DateTime.Now;

            var notePath = GetNotePath(note.Id);
            var yaml = ConvertToYaml(note);
            File.WriteAllText(notePath, yaml, Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save note '{note.Id}': {ex}");
            return false;
        }
    }

    /// <summary>
    /// Lists all available note IDs sorted by SortOrder.
    /// </summary>
    public List<string> ListNotes()
    {
        try
        {
            if (!Directory.Exists(_notesDirectory))
                return new();

            // Load all notes and sort by SortOrder, then by CreatedDate
            var notes = Directory.GetFiles(_notesDirectory, "*.yml")
                .Select(f => ParseYamlNote(f))
                .Where(n => n != null)
                .OrderBy(n => n!.SortOrder)
                .ThenBy(n => n!.CreatedDate)
                .Select(n => n!.Id)
                .ToList();

            return notes;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to list notes: {ex}");
            return new();
        }
    }

    /// <summary>
    /// Archives a note by moving it to the Archive folder.
    /// </summary>
    public bool ArchiveNote(string noteId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(noteId))
                return false;

            var notePath = GetNotePath(noteId);
            if (!File.Exists(notePath))
                return false;

            var archivePath = Path.Combine(_archiveDirectory, $"{noteId}.yml");
            
            // If archive file already exists, append timestamp to make it unique
            if (File.Exists(archivePath))
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                archivePath = Path.Combine(_archiveDirectory, $"{noteId}_{timestamp}.yml");
            }
            
            File.Move(notePath, archivePath);
            Debug.WriteLine($"Archived note '{noteId}' to '{archivePath}'");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to archive note '{noteId}': {ex}");
            return false;
        }
    }

    /// <summary>
    /// Deletes a note file permanently (use ArchiveNote instead for safer deletion).
    /// </summary>
    public bool DeleteNote(string noteId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(noteId))
                return false;

            var notePath = GetNotePath(noteId);
            if (File.Exists(notePath))
            {
                File.Delete(notePath);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to delete note '{noteId}': {ex}");
            return false;
        }
    }

    /// <summary>
    /// Migrates old .txt note files to .yml format.
    /// </summary>
    public void MigrateOldNotesIfNeeded()
    {
        try
        {
            if (!Directory.Exists(_notesDirectory))
                return;

            var txtFiles = Directory.GetFiles(_notesDirectory, "*.txt");
            foreach (var txtFile in txtFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(txtFile);
                var ymlPath = GetNotePath(fileName);

                // Only migrate if yml file doesn't exist
                if (File.Exists(ymlPath))
                {
                    Debug.WriteLine($"Skipping migration of '{fileName}' - yml file already exists");
                    continue;
                }

                var content = File.ReadAllText(txtFile, Encoding.UTF8);
                var note = new Note
                {
                    Id = fileName,
                    Name = fileName,
                    Content = content,
                    CreatedDate = File.GetCreationTime(txtFile),
                    LastModifiedDate = File.GetLastWriteTime(txtFile),
                    HasCustomName = false, // Assume old notes used first line for name
                    ExcludeFromContext = false
                };

                if (SaveNote(note))
                {
                    Debug.WriteLine($"Migrated note '{fileName}' from .txt to .yml");
                    // Optionally delete old .txt file after successful migration
                    try
                    {
                        File.Delete(txtFile);
                        Debug.WriteLine($"Deleted old .txt file for '{fileName}'");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Could not delete old .txt file '{txtFile}': {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during note migration: {ex}");
        }
    }

    private string GetNotePath(string noteId) =>
        Path.Combine(_notesDirectory, $"{noteId}.yml");

    private Note ParseYamlNote(string filePath)
    {
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        var note = new Note();
        var contentLines = new StringBuilder();
        var inContent = false;

        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd();

            if (trimmed.StartsWith("id:"))
                note.Id = ExtractYamlValue(trimmed, "id");
            else if (trimmed.StartsWith("name:"))
                note.Name = ExtractYamlValue(trimmed, "name");
            else if (trimmed.StartsWith("created_date:"))
            {
                var dateStr = ExtractYamlValue(trimmed, "created_date");
                if (DateTime.TryParse(dateStr, out var createdDate))
                    note.CreatedDate = createdDate;
            }
            else if (trimmed.StartsWith("last_modified_date:"))
            {
                var dateStr = ExtractYamlValue(trimmed, "last_modified_date");
                if (DateTime.TryParse(dateStr, out var modifiedDate))
                    note.LastModifiedDate = modifiedDate;
            }
            else if (trimmed.StartsWith("has_custom_name:"))
            {
                var boolStr = ExtractYamlValue(trimmed, "has_custom_name");
                note.HasCustomName = bool.TryParse(boolStr, out var hasCustomName) && hasCustomName;
            }
            else if (trimmed.StartsWith("exclude_from_context:"))
            {
                var boolStr = ExtractYamlValue(trimmed, "exclude_from_context");
                note.ExcludeFromContext = bool.TryParse(boolStr, out var exclude) && exclude;
            }
            else if (trimmed.StartsWith("sort_order:"))
            {
                var orderStr = ExtractYamlValue(trimmed, "sort_order");
                if (int.TryParse(orderStr, out var sortOrder))
                    note.SortOrder = sortOrder;
            }
            else if (trimmed.StartsWith("content:"))
            {
                inContent = true;
                var contentPart = ExtractYamlValue(trimmed, "content");
                if (!string.IsNullOrEmpty(contentPart))
                    contentLines.Append(contentPart);
            }
            else if (inContent)
            {
                // Handle multi-line content
                if (trimmed.StartsWith("  "))
                {
                    var content = trimmed.Substring(2);
                    if (contentLines.Length > 0)
                        contentLines.AppendLine();
                    contentLines.Append(content);
                }
                else if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("#"))
                {
                    // Another property, stop content parsing
                    break;
                }
            }
        }

        note.Content = contentLines.ToString().Trim();
        
        // Ensure we have at least an ID
        if (string.IsNullOrEmpty(note.Id))
            note.Id = Path.GetFileNameWithoutExtension(filePath);
        
        // Ensure we have a name
        if (string.IsNullOrEmpty(note.Name))
            note.Name = note.Id;

        return note;
    }

    private string ExtractYamlValue(string line, string key)
    {
        var prefix = $"{key}:";
        if (!line.StartsWith(prefix))
            return string.Empty;

        var value = line.Substring(prefix.Length).Trim();
        // Remove quotes if present
        if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
            (value.StartsWith("'") && value.EndsWith("'")))
        {
            value = value.Substring(1, value.Length - 2);
        }
        // Handle pipe for multi-line
        if (value == "|")
            return string.Empty;
        return value;
    }

    private string ConvertToYaml(Note note)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# RPOverlay Note");
        sb.AppendLine($"# Last Modified: {note.LastModifiedDate:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine($"id: {quote(note.Id)}");
        sb.AppendLine($"name: {quote(note.Name)}");
        sb.AppendLine($"created_date: {quote(note.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss"))}");
        sb.AppendLine($"last_modified_date: {quote(note.LastModifiedDate.ToString("yyyy-MM-dd HH:mm:ss"))}");
        sb.AppendLine($"has_custom_name: {note.HasCustomName.ToString().ToLower()}");
    sb.AppendLine($"exclude_from_context: {note.ExcludeFromContext.ToString().ToLower()}");
        sb.AppendLine($"sort_order: {note.SortOrder}");
        sb.AppendLine();
        sb.AppendLine("content: |");
        
        // Indent content with 2 spaces for YAML multi-line
        foreach (var line in note.Content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
        {
            sb.AppendLine($"  {line}");
        }

        return sb.ToString();

        string quote(string text) => $"\"{text}\"";
    }
}
