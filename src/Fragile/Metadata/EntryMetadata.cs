using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Fragile.Metadata
{
    /// <summary>
    /// Represents metadata for a file or directory in an archive
    /// </summary>
    public class EntryMetadata
    {
        /// <summary>
        /// File group (on Unix-like systems)
        /// </summary>
        [JsonPropertyName("group")]
        public string? Group { get; set; }

        /// <summary>
        /// Original file owner
        /// </summary>
        [JsonPropertyName("owner")]
        public string? Owner { get; set; }

        /// <summary>
        /// Comments about the file
        /// </summary>
        [JsonPropertyName("comment")]
        public string? Comment { get; set; }

        /// <summary>
        /// MIME type of the file
        /// </summary>
        [JsonPropertyName("mime")]
        public string? MimeType { get; set; }

        /// <summary>
        /// File attributes
        /// </summary>
        [JsonPropertyName("attributes")]
        public string? Attributes { get; set; }

        /// <summary>
        /// Creation time of the file
        /// </summary>
        [JsonPropertyName("created")]
        public DateTime? CreationTime { get; set; }

        /// <summary>
        /// Tags for searching and categorization
        /// </summary>
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = [];

        /// <summary>
        /// Last access time of the file
        /// </summary>
        [JsonPropertyName("accessed")]
        public DateTime? LastAccessTime { get; set; }

        /// <summary>
        /// Custom metadata dictionary for user-defined properties
        /// </summary>
        [JsonPropertyName("custom")]
        public Dictionary<string, string> CustomProperties { get; set; } = [];

        /// <summary>
        /// Serializes metadata to JSON
        /// </summary>
        /// <returns>JSON string representation</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = false,
                TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }

        /// <summary>
        /// Adds a tag
        /// </summary>
        /// <param name="tag">Tag to add</param>
        public void AddTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return;
            }

            if (!Tags.Contains(tag))
            {
                Tags.Add(tag);
            }
        }

        /// <summary>
        /// Removes a tag
        /// </summary>
        /// <param name="tag">Tag to remove</param>
        /// <returns>True if the tag was removed</returns>
        public bool RemoveTag(string tag)
        {
            return Tags.Remove(tag);
        }

        /// <summary>
        /// Gets a custom property value
        /// </summary>
        /// <param name="key">Property name</param>
        /// <returns>Property value or null if not found</returns>
        public string? GetProperty(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            return CustomProperties.TryGetValue(key, out string? value) ? value : null;
        }

        /// <summary>
        /// Adds a custom property
        /// </summary>
        /// <param name="key">Property name</param>
        /// <param name="value">Property value</param>
        public void AddProperty(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Property key cannot be null or empty", nameof(key));
            }

            CustomProperties[key] = value;
        }

        /// <summary>
        /// Deserializes metadata from JSON
        /// </summary>
        /// <param name="json">JSON string to deserialize</param>
        /// <returns>EntryMetadata object</returns>
        public static EntryMetadata FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new EntryMetadata();
            }

            return JsonSerializer.Deserialize<EntryMetadata>(json) ?? new EntryMetadata();
        }
    }
}