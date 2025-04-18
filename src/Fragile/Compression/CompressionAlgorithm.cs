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
        /// Brotli compression algorithm
        /// </summary>
        Brotli = 1,

        /// <summary>
        /// Standard Deflate algorithm (compatible with ZIP)
        /// </summary>
        Deflate = 2
    }
}