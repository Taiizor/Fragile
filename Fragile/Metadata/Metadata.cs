using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fragile.Metadata
{
    /// <summary>
    /// Represents metadata for an archive file.
    /// </summary>
    public class ArchiveMetadata
    {
        /// <summary>
        /// Gets or sets the creation timestamp of the archive.
        /// </summary>
        [JsonPropertyName("creationTime")]
        public DateTimeOffset CreationTime { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets the last modification timestamp of the archive.
        /// </summary>
        [JsonPropertyName("modificationTime")]
        public DateTimeOffset ModificationTime { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets the author of the archive.
        /// </summary>
        [JsonPropertyName("author")]
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the title of the archive.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the archive.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the version of the archive format.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Gets or sets the custom tags associated with the archive.
        /// </summary>
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the application-specific data for the archive.
        /// </summary>
        [JsonPropertyName("applicationData")]
        public Dictionary<string, string> ApplicationData { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Represents metadata for a file within an archive.
    /// </summary>
    public class FileMetadata
    {
        /// <summary>
        /// Gets or sets the original creation timestamp of the file.
        /// </summary>
        [JsonPropertyName("creationTime")]
        public DateTimeOffset CreationTime { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets the last access timestamp of the file.
        /// </summary>
        [JsonPropertyName("lastAccessTime")]
        public DateTimeOffset LastAccessTime { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets the last modification timestamp of the file.
        /// </summary>
        [JsonPropertyName("lastWriteTime")]
        public DateTimeOffset LastWriteTime { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets the file attributes.
        /// </summary>
        [JsonPropertyName("attributes")]
        public string Attributes { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the owner of the file.
        /// </summary>
        [JsonPropertyName("owner")]
        public string Owner { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the group of the file.
        /// </summary>
        [JsonPropertyName("group")]
        public string Group { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the MIME type of the file.
        /// </summary>
        [JsonPropertyName("mimeType")]
        public string MimeType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the custom tags associated with the file.
        /// </summary>
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the comments for the file.
        /// </summary>
        [JsonPropertyName("comments")]
        public string Comments { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the custom properties for application-specific needs.
        /// </summary>
        [JsonPropertyName("customProperties")]
        public Dictionary<string, string> CustomProperties { get; set; } = new Dictionary<string, string>();
    }
} 