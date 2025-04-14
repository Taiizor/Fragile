using Fragile.Core.Options;

namespace Fragile.Interfaces.Providers;

/// <summary>
/// Defines the interface for an encryption algorithm provider.
/// Implementations handle the logic for encrypting and decrypting data streams.
/// </summary>
public interface IEncryptionProvider
{
    /// <summary>
    /// Encrypts the source stream into the target stream asynchronously.
    /// </summary>
    /// <param name="source">The plaintext stream to encrypt.</param>
    /// <param name="target">The stream to write the ciphertext to.</param>
    /// <param name="options">Encryption options, including algorithm and password/key information.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous encryption operation.</returns>
    /// <remarks>
    /// Implementations are responsible for handling key derivation (e.g., PBKDF2) from the password,
    /// generating salts and IVs, and potentially storing necessary metadata (like salt/IV) alongside the ciphertext.
    /// </remarks>
    Task EncryptAsync(Stream source, Stream target, EncryptionOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts the source stream into the target stream asynchronously.
    /// </summary>
    /// <param name="source">The ciphertext stream to decrypt.</param>
    /// <param name="target">The stream to write the plaintext to.</param>
    /// <param name="options">Encryption options, including algorithm and password/key information.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous decryption operation.</returns>
    /// <remarks>
    /// Implementations need to read any necessary metadata (like salt/IV) potentially stored with the ciphertext
    /// and use the provided password to derive the key for decryption.
    /// </remarks>
    Task DecryptAsync(Stream source, Stream target, EncryptionOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Encrypts the source stream into the target stream.
    /// </summary>
    /// <param name="source">The plaintext stream to encrypt.</param>
    /// <param name="target">The stream to write the ciphertext to.</param>
    /// <param name="options">Encryption options, including algorithm and password/key information.</param>
    void Encrypt(Stream source, Stream target, EncryptionOptions options);

    /// <summary>
    /// Decrypts the source stream into the target stream.
    /// </summary>
    /// <param name="source">The ciphertext stream to decrypt.</param>
    /// <param name="target">The stream to write the plaintext to.</param>
    /// <param name="options">Encryption options, including algorithm and password/key information.</param>
    void Decrypt(Stream source, Stream target, EncryptionOptions options);

    // Optional: Properties to indicate required key sizes or block sizes if needed
    // int RequiredKeySizeBits { get; }
    // int BlockSizeBits { get; } 
}