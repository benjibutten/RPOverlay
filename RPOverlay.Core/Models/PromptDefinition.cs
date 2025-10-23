namespace RPOverlay.Core.Models;

/// <summary>
/// Represents a system prompt definition stored in YAML format.
/// </summary>
public sealed class PromptDefinition
{
    /// <summary>
    /// Unique identifier for the prompt (filename without extension).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Display name for UI.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this prompt does.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The actual system prompt content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Version of the prompt (for tracking changes).
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// When the prompt was created or last modified.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Check if all required fields are set.
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Content);
}
