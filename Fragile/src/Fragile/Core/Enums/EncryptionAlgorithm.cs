namespace Fragile.Core.Enums;

/// <summary>
/// Specifies the encryption algorithm used for securing archive data.
/// </summary>
public enum EncryptionAlgorithm
{
    /// <summary>
    /// No encryption is applied.
    /// </summary>
    None = 0,

    /// <summary>
    /// AES (Advanced Encryption Standard) with a 128-bit key.
    /// </summary>
    Aes128 = 1,

    /// <summary>
    /// AES (Advanced Encryption Standard) with a 256-bit key.
    /// </summary>
    Aes256 = 2,

    // --- Currently Unimplemented --- 
    // The following algorithms are defined for future extensibility but are not yet implemented.

    /// <summary>
    /// ChaCha20 stream cipher. (Not Implemented)
    /// </summary>
    ChaCha20 = 3,

    /// <summary>
    /// Twofish block cipher. (Not Implemented)
    /// </summary>
    Twofish = 4
} 