namespace RPOverlay.Core.Models;

/// <summary>
/// Represents a note with metadata.
/// </summary>
public class Note
{
    /// <summary>
    /// Unique identifier for the note.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the note.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The actual content of the note.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// When the note was created.
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    /// <summary>
    /// When the note was last modified.
    /// </summary>
    public DateTime LastModifiedDate { get; set; } = DateTime.Now;

    /// <summary>
    /// True if the user has manually set a custom name, false if name should be derived from first line.
    /// </summary>
    public bool HasCustomName { get; set; } = false;
}
