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
        Deflate = 2,

        /// <summary>
        /// LZMA compression algorithm
        /// </summary>
        LZMA = 3,

        /// <summary>
        /// BZIP2 compression algorithm
        /// </summary>
        BZip2 = 4,

        /// <summary>
        /// Zstandard compression algorithm
        /// </summary>
        ZStd = 5,

        /// <summary>
        /// LZ4 fast compression algorithm
        /// </summary>
        LZ4 = 6
    }
}