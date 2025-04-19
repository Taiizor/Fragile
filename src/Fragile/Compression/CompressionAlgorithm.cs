namespace Fragile.Compression
{
    /// <summary>
    /// Compression algorithms supported by Fragile
    /// </summary>
    public enum CompressionAlgorithm
    {
        /// <summary>
        /// No compression, store files as-is
        /// </summary>
        Store = 0,

        /// <summary>
        /// GZip compression algorithm
        /// </summary>
        GZip = 1,

        /// <summary>
        /// Brotli compression algorithm
        /// </summary>
        Brotli = 2,

        /// <summary>
        /// Standard Deflate algorithm (compatible with ZIP)
        /// </summary>
        Deflate = 3
    }
}