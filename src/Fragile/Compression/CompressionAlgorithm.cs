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
        /// Standard Deflate algorithm (compatible with ZIP)
        /// </summary>
        Deflate = 1,

        /// <summary>
        /// LZMA compression algorithm
        /// </summary>
        LZMA = 2,

        /// <summary>
        /// BZIP2 compression algorithm
        /// </summary>
        BZip2 = 3,

        /// <summary>
        /// Zstandard compression algorithm
        /// </summary>
        ZStd = 4,

        /// <summary>
        /// LZ4 fast compression algorithm
        /// </summary>
        LZ4 = 5
    }
}