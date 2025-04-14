namespace Fragile.Core.Enums;

/// <summary>
/// Specifies the compression algorithm used within the archive.
/// </summary>
public enum CompressionAlgorithm
{
    /// <summary>
    /// No compression is applied; files are stored as is.
    /// </summary>
    Store = 0,

    /// <summary>
    /// Deflate compression algorithm (compatible with ZIP).
    /// </summary>
    Deflate = 1,

    // --- Currently Unimplemented --- 
    // The following algorithms are defined for future extensibility but are not yet implemented.

    /// <summary>
    /// LZMA (Lempel–Ziv–Markov chain algorithm). (Not Implemented)
    /// </summary>
    Lzma = 2,

    /// <summary>
    /// BZip2 algorithm. (Not Implemented)
    /// </summary>
    BZip2 = 3,

    /// <summary>
    /// Zstandard algorithm. (Not Implemented)
    /// </summary>
    Zstd = 4,

    /// <summary>
    /// LZ4 algorithm. (Not Implemented)
    /// </summary>
    Lz4 = 5
}