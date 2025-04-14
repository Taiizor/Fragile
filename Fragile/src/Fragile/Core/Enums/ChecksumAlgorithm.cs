namespace Fragile.Core.Enums;

/// <summary>
/// Specifies the algorithm used for calculating checksums to verify data integrity.
/// </summary>
public enum ChecksumAlgorithm
{
    /// <summary>
    /// No checksum calculation.
    /// </summary>
    None = 0,

    /// <summary>
    /// Cyclic Redundancy Check (CRC) 32-bit.
    /// </summary>
    Crc32 = 1,

    /// <summary>
    /// Message Digest 5 (MD5) hash.
    /// </summary>
    /// <remarks>Note: MD5 is considered cryptographically weak and should be used with caution.</remarks>
    Md5 = 2,

    /// <summary>
    /// Secure Hash Algorithm 1 (SHA-1).
    /// </summary>
    /// <remarks>Note: SHA-1 is considered cryptographically weak and should be used with caution.</remarks>
    Sha1 = 3,

    /// <summary>
    /// Secure Hash Algorithm 2 with a 256-bit digest.
    /// </summary>
    Sha256 = 4,

    /// <summary>
    /// Secure Hash Algorithm 2 with a 512-bit digest.
    /// </summary>
    Sha512 = 5
}