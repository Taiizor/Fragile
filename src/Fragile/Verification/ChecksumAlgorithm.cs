namespace Fragile.Verification
{
    /// <summary>
    /// Checksum algorithms supported by Fragile
    /// </summary>
    public enum ChecksumAlgorithm
    {
        /// <summary>
        /// No checksum
        /// </summary>
        None = 0,
        
        /// <summary>
        /// CRC32 checksum
        /// </summary>
        CRC32 = 1,
        
        /// <summary>
        /// MD5 hash
        /// </summary>
        MD5 = 2,
        
        /// <summary>
        /// SHA-1 hash
        /// </summary>
        SHA1 = 3,
        
        /// <summary>
        /// SHA-256 hash
        /// </summary>
        SHA256 = 4,
        
        /// <summary>
        /// SHA-512 hash
        /// </summary>
        SHA512 = 5
    }
} 