namespace Fragile.Compression
{
    /// <summary>
    /// Compression level for balancing speed vs ratio
    /// </summary>
    public enum CompressionLevel
    {
        /// <summary>
        /// Fastest compression, lowest compression ratio
        /// </summary>
        Fastest = 0,
        
        /// <summary>
        /// Fast compression with reasonable ratio
        /// </summary>
        Fast = 1,
        
        /// <summary>
        /// Balanced compression level
        /// </summary>
        Normal = 2,
        
        /// <summary>
        /// Slower compression with good ratio
        /// </summary>
        High = 3,
        
        /// <summary>
        /// Slowest compression with best ratio
        /// </summary>
        Ultra = 4
    }
} 