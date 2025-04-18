using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Fragile.Metadata
{
    /// <summary>
    /// Represents metadata for the entire archive
    /// </summary>
    public class ArchiveMetadata
    {
        /// <summary>
        /// Archive title
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /// <summary>
        /// Archive author or owner
        /// </summary>
        [JsonPropertyName("author")]
        public string? Author { get; set; }

        /// <summary>
        /// Archive version
        /// </summary>
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        /// <summary>
        /// Archive description
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Archive categories or tags
        /// </summary>
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = [];

        /// <summary>
        /// Archive creator name or application
        /// </summary>
        [JsonPropertyName("creator")]
        public string? Creator { get; set; } = "Fragile Library";

        /// <summary>
        /// Archive creation date
        /// </summary>
        [JsonPropertyName("created")]
        public DateTime CreationTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Archive last modified date
        /// </summary>
        [JsonPropertyName("modified")]
        public DateTime LastModifiedTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Fragile library version used to create the archive
        /// </summary>
        [JsonPropertyName("library_version")]
        public string LibraryVersion { get; set; } = GetLibraryVersion();

        /// <summary>
        /// Application-specific data
        /// </summary>
        [JsonPropertyName("app_data")]
        public Dictionary<string, string> ApplicationData { get; set; } = [];

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
                WriteIndented = true,
                TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
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
        /// <returns>ArchiveMetadata object</returns>
        public static ArchiveMetadata FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new ArchiveMetadata();
            }

            return JsonSerializer.Deserialize<ArchiveMetadata>(json) ?? new ArchiveMetadata();
        }

        /// <summary>
        /// Adds an application-specific data item
        /// </summary>
        /// <param name="key">Data key</param>
        /// <param name="value">Data value</param>
        public void AddApplicationData(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Application data key cannot be null or empty", nameof(key));
            }

            ApplicationData[key] = value;
        }

        /// <summary>
        /// Gets the current Fragile library version
        /// </summary>
        /// <returns>Version string</returns>
        private static string GetLibraryVersion()
        {
            return typeof(ArchiveMetadata).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        }
    }
}