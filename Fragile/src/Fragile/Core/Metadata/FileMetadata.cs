namespace Fragile.Core.Metadata;

/// <summary>
/// Represents metadata associated with a single file within the archive.
/// </summary>
public class FileMetadata
{
    /// <summary>
    /// Gets or sets the original creation time of the file (UTC).
    /// Null if not available or not stored.
    /// </summary>
    public DateTimeOffset? CreationTimeUtc { get; set; }

    /// <summary>
    /// Gets or sets the last modification time of the file (UTC).
    /// Null if not available or not stored.
    /// </summary>
    public DateTimeOffset? LastWriteTimeUtc { get; set; }

    /// <summary>
    /// Gets or sets the last access time of the file (UTC).
    /// Null if not available or not stored.
    /// </summary>
    public DateTimeOffset? LastAccessTimeUtc { get; set; }

    /// <summary>
    /// Gets or sets the file attributes (e.g., ReadOnly, Hidden).
    /// Null if not available or not stored.
    /// </summary>
    public FileAttributes? Attributes { get; set; }

    /// <summary>
    /// Gets or sets the name or identifier of the file owner.
    /// Null if not available or not stored.
    /// </summary>
    public string? Owner { get; set; }

    /// <summary>
    /// Gets or sets the name or identifier of the file group.
    /// Null if not available or not stored.
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// Gets or sets the detected or specified MIME type of the file.
    /// Null if not available or not stored.
    /// </summary>
    public string? MimeType { get; set; }

    /// <summary>
    /// Gets or sets custom tags associated with the file.
    /// Useful for categorization or searching.
    /// </summary>
    public List<string> Tags { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets a general-purpose comment for the file.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Gets or sets a dictionary for storing custom application-specific properties for the file.
    /// Keys and values are strings.
    /// </summary>
    public Dictionary<string, string> CustomProperties { get; set; } = new Dictionary<string, string>();

    // Consider adding:
    // public long UncompressedSize { get; set; }
    // public long CompressedSize { get; set; }
    // public string FileName { get; internal set; } // Usually part of the entry itself, not just metadata
}