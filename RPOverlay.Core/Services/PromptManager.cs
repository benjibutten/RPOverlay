using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using RPOverlay.Core.Abstractions;
using RPOverlay.Core.Models;

namespace RPOverlay.Core.Services;

/// <summary>
/// Manages system prompts stored as YAML files.
/// </summary>
public sealed class PromptManager
{
    private readonly string _promptsDirectory;
    private readonly string _defaultPromptPath;

    public PromptManager(IOverlayConfigPathProvider pathProvider)
    {
        if (pathProvider == null)
            throw new ArgumentNullException(nameof(pathProvider));

        var configDir = Path.GetDirectoryName(pathProvider.GetConfigFilePath());
        _promptsDirectory = Path.Combine(configDir!, "prompts");
        _defaultPromptPath = Path.Combine(_promptsDirectory, "default.yaml");

        Directory.CreateDirectory(_promptsDirectory);
    }

    /// <summary>
    /// Gets the prompts directory path.
    /// </summary>
    public string PromptsDirectory => _promptsDirectory;

    /// <summary>
    /// Loads a prompt by name from disk.
    /// </summary>
    public PromptDefinition? LoadPrompt(string promptName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(promptName))
                return null;

            var promptPath = GetPromptPath(promptName);
            if (!File.Exists(promptPath))
                return null;

            return ParseYamlPrompt(promptPath, promptName);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load prompt '{promptName}': {ex}");
            return null;
        }
    }

    /// <summary>
    /// Saves a prompt to disk as YAML.
    /// </summary>
    public bool SavePrompt(PromptDefinition prompt)
    {
        try
        {
            if (!prompt.IsValid)
                throw new ArgumentException("Prompt is not valid", nameof(prompt));

            var promptPath = GetPromptPath(prompt.Name);
            var yaml = ConvertToYaml(prompt);
            File.WriteAllText(promptPath, yaml, Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save prompt '{prompt.Name}': {ex}");
            return false;
        }
    }

    /// <summary>
    /// Lists all available prompts.
    /// </summary>
    public List<string> ListPrompts()
    {
        try
        {
            if (!Directory.Exists(_promptsDirectory))
                return new();

            return Directory.GetFiles(_promptsDirectory, "*.yaml")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .OrderBy(n => n)
                .ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to list prompts: {ex}");
            return new();
        }
    }

    /// <summary>
    /// Creates the default prompt if it doesn't exist.
    /// </summary>
    public void EnsureDefaultPromptExists()
    {
        if (File.Exists(_defaultPromptPath))
            return;

        var defaultPrompt = new PromptDefinition
        {
            Name = "default",
            DisplayName = "Standard",
            Description = "Default roleplay assistant",
            Content = "Du är en hjälpsam assistent för rollspel.",
            Version = "1.0",
            CreatedAt = DateTime.UtcNow
        };

        SavePrompt(defaultPrompt);
    }

    /// <summary>
    /// Migrates SystemPrompt from settings.ini to default.yaml.
    /// </summary>
    public void MigrateSystemPromptIfNeeded(string systemPromptContent)
    {
        if (File.Exists(_defaultPromptPath) || string.IsNullOrWhiteSpace(systemPromptContent))
            return;

        var migrationPrompt = new PromptDefinition
        {
            Name = "default",
            DisplayName = "Migrerad Prompt",
            Description = "Migrerad från settings.ini",
            Content = systemPromptContent,
            Version = "1.0",
            CreatedAt = DateTime.UtcNow
        };

        SavePrompt(migrationPrompt);
    }

    /// <summary>
    /// Deletes a prompt file. Cannot delete 'default'.
    /// </summary>
    public bool DeletePrompt(string promptName)
    {
        try
        {
            if (promptName == "default")
                return false; // Prevent deleting default

            var promptPath = GetPromptPath(promptName);
            if (File.Exists(promptPath))
            {
                File.Delete(promptPath);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to delete prompt '{promptName}': {ex}");
            return false;
        }
    }

    private string GetPromptPath(string promptName) =>
        Path.Combine(_promptsDirectory, $"{promptName}.yaml");

    private PromptDefinition ParseYamlPrompt(string filePath, string promptName)
    {
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        var prompt = new PromptDefinition { Name = promptName };
        var contentLines = new StringBuilder();
        var inContent = false;

        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd();

            if (trimmed.StartsWith("name:"))
                prompt.Name = ExtractYamlValue(trimmed, "name");
            else if (trimmed.StartsWith("displayName:"))
                prompt.DisplayName = ExtractYamlValue(trimmed, "displayName");
            else if (trimmed.StartsWith("display_name:"))
                prompt.DisplayName = ExtractYamlValue(trimmed, "display_name");
            else if (trimmed.StartsWith("description:"))
                prompt.Description = ExtractYamlValue(trimmed, "description");
            else if (trimmed.StartsWith("version:"))
                prompt.Version = ExtractYamlValue(trimmed, "version");
            else if (trimmed.StartsWith("content:"))
            {
                inContent = true;
                var contentPart = ExtractYamlValue(trimmed, "content");
                if (!string.IsNullOrEmpty(contentPart))
                    contentLines.Append(contentPart);
            }
            else if (inContent)
            {
                // Handle multi-line content - check for pipe or dash continuation
                if (trimmed.StartsWith("  ") || trimmed.StartsWith("- "))
                {
                    // Remove leading spaces/dashes for multi-line content
                    var content = trimmed.StartsWith("- ") ? trimmed.Substring(2) : trimmed.Substring(2);
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

        prompt.Content = contentLines.ToString().Trim();
        if (string.IsNullOrEmpty(prompt.DisplayName))
            prompt.DisplayName = prompt.Name;

        return prompt;
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
        // Handle pipe for multi-line (just mark that we should read more lines)
        if (value == "|")
            return string.Empty;
        return value;
    }

    private string ConvertToYaml(PromptDefinition prompt)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# RPOverlay System Prompt");
        sb.AppendLine($"# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine($"name: {quote(prompt.Name)}");
        sb.AppendLine($"display_name: {quote(prompt.DisplayName)}");
        sb.AppendLine($"description: {quote(prompt.Description)}");
        sb.AppendLine($"version: {quote(prompt.Version)}");
        sb.AppendLine($"created_at: {quote(prompt.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"))}");
        sb.AppendLine();
        sb.AppendLine("content: |");
        
        // Indent content with 2 spaces for YAML multi-line
        foreach (var line in prompt.Content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
        {
            sb.AppendLine($"  {line}");
        }

        return sb.ToString();

        string quote(string text) => $"\"{text}\"";
    }
}
