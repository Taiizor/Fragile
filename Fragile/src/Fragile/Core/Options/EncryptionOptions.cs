using Fragile.Core.Enums;

namespace Fragile.Core.Options;

/// <summary>
/// Provides configuration options for encryption operations within the archive.
/// </summary>
public class EncryptionOptions
{
    /// <summary>
    /// Gets or sets the encryption algorithm to use.
    /// Defaults to <see cref="EncryptionAlgorithm.None"/> (no encryption).
    /// </summary>
    public EncryptionAlgorithm Algorithm { get; set; } = EncryptionAlgorithm.None;

    /// <summary>
    /// Gets or sets the password used for encryption.
    /// This is required if <see cref="Algorithm"/> is not <see cref="EncryptionAlgorithm.None"/>.
    /// </summary>
    /// <remarks>
    /// The library uses PBKDF2 (Password-Based Key Derivation Function 2) 
    /// with a randomly generated salt (stored in the archive) to derive the actual encryption key from this password.
    /// Ensure you use a strong password.
    /// </remarks>
    public string? Password { get; set; } = null;

    /// <summary>
    /// Gets or sets a value indicating whether encryption settings should apply to each file individually 
    /// or globally to the entire archive stream (if the archive format supports it).
    /// Defaults to true (per-file encryption). Per-file encryption is generally more flexible.
    /// </summary>
    public bool UsePerFileEncryption { get; set; } = true;

    // Potential future options:
    // public byte[]? Salt { get; set; } // Allow specifying salt? Maybe not good practice.
    // public int Pbkdf2IterationCount { get; set; } = 600000; // OWASP recommendation (as of late 2023)
    // public KeyDerivationPrf Pbkdf2Prf { get; set; } = KeyDerivationPrf.HMACSHA256; // Default PRF for PBKDF2
}