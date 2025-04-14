namespace Fragile.Core.Metadata;

/// <summary>
/// Represents metadata associated with the entire archive.
/// </summary>
public class ArchiveMetadata
{
    /// <summary>
    /// Gets or sets the creation timestamp of the archive (UTC).
    /// </summary>
    public DateTimeOffset CreationTimeUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the last modification timestamp of the archive (UTC).
    /// Null if the archive has not been modified since creation.
    /// </summary>
    public DateTimeOffset? LastModificationTimeUtc { get; set; }

    /// <summary>
    /// Gets or sets the name or identifier of the author or creator of the archive.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Gets or sets the title of the archive.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets a description for the archive content.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the version of the application that created the archive, or a user-defined version.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets custom tags associated with the archive.
    /// Useful for categorization or searching.
    /// </summary>
    public List<string> Tags { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets a dictionary for storing custom application-specific data related to the archive.
    /// Keys and values are strings. Data is typically serialized (e.g., to JSON) before storing.
    /// </summary>
    public Dictionary<string, string> CustomApplicationData { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the name of the application that created or last modified the archive.
    /// Can be used for tracking compatibility or specific features.
    /// </summary>
    public string? ApplicationName { get; set; } = "Fragile Library"; // Default value

    // Internal/Format Specific - Usually not directly set by user but stored in archive header
    // internal int FormatVersion { get; set; }
    // internal Guid ArchiveId { get; set; }
} 