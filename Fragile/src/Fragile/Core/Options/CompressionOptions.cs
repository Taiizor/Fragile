using Fragile.Core.Enums;

namespace Fragile.Core.Options;

/// <summary>
/// Provides configuration options for compression operations within the archive.
/// </summary>
public class CompressionOptions
{
    /// <summary>
    /// Gets or sets the compression algorithm to use.
    /// Defaults to <see cref="CompressionAlgorithm.Deflate"/>.
    /// </summary>
    public CompressionAlgorithm Algorithm { get; set; } = CompressionAlgorithm.Deflate;

    /// <summary>
    /// Gets or sets the level of compression to apply.
    /// Defaults to <see cref="CompressionLevel.Normal"/>.
    /// </summary>
    public CompressionLevel Level { get; set; } = CompressionLevel.Normal;

    /// <summary>
    /// Gets or sets a value indicating whether to use solid compression mode.
    /// Solid mode can improve compression ratios for archives containing many small, similar files,
    /// but may increase extraction time for individual files.
    /// Defaults to false.
    /// </summary>
    public bool UseSolidCompression { get; set; } = false;

    /// <summary>
    /// Gets or sets the number of threads to use for parallel compression.
    /// If set to 0 or 1, compression will be performed sequentially.
    /// If set to a value greater than 1, parallel compression will be attempted if applicable.
    /// If set to null (default), the library may automatically determine the optimal thread count based on system resources and workload.
    /// </summary>
    /// <remarks>
    /// Parallel compression effectiveness depends on the chosen algorithm and the data being compressed.
    /// </remarks>
    public int? ThreadCount { get; set; } = null; // Null means auto-detect or default behavior

    // Potential future options:
    // public long? SolidBlockSize { get; set; }
    // public Dictionary<string, string> AlgorithmSpecificParameters { get; set; } = new Dictionary<string, string>();
}