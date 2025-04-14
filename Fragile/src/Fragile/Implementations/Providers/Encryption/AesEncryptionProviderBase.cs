using Fragile.Core.Options;
using Fragile.Interfaces.Providers;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Fragile.Implementations.Providers.Encryption;

/// <summary>
/// Base class for AES encryption providers (AES-128, AES-256).
/// Handles common logic like key derivation using PBKDF2, salt/IV generation, and stream processing.
/// </summary>
internal abstract class AesEncryptionProviderBase : IEncryptionProvider
{
    // Default values recommended by OWASP (as of late 2023/early 2024)
    internal const int DefaultSaltSizeBytes = 16; // 128 bits
    internal const int DefaultIvSizeBytes = 16;   // 128 bits for AES block size
    protected const int DefaultPbkdf2Iterations = 600000;
    protected static readonly HashAlgorithmName DefaultPbkdf2HashAlgorithm = HashAlgorithmName.SHA256;

    private readonly int _bufferSize;

    protected AesEncryptionProviderBase(int bufferSize = 81920)
    {
        _bufferSize = bufferSize > 0 ? bufferSize : 81920;
    }

    /// <summary>
    /// Gets the required key size in bits for the specific AES implementation (e.g., 128 or 256).
    /// </summary>
    protected abstract int KeySizeBits { get; }
    protected int KeySizeBytes => KeySizeBits / 8;

    public async Task EncryptAsync(Stream source, Stream target, EncryptionOptions options, CancellationToken cancellationToken = default)
    {
        if (options.Password is null)
            throw new ArgumentNullException(nameof(options.Password), "Password is required for AES encryption.");

        byte[] salt = GenerateSalt();
        byte[] key = DeriveKeyFromPassword(options.Password, salt);
        byte[] iv = GenerateIv();

        // Write salt and IV to the beginning of the target stream so they can be read during decryption.
        await target.WriteAsync(salt, 0, salt.Length, cancellationToken).ConfigureAwait(false);
        await target.WriteAsync(iv, 0, iv.Length, cancellationToken).ConfigureAwait(false);

        using (var aes = CreateAesInstance())
        {
            aes.Key = key;
            aes.IV = iv;
            // Use CBC mode and PKCS7 padding by default (common and secure choices)
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using (var cryptoStream = new CryptoStream(target, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true))
            {
                await source.CopyToAsync(cryptoStream, _bufferSize, cancellationToken).ConfigureAwait(false);
                // Flush final block is handled by CryptoStream disposal
            }
        }
    }

    public async Task DecryptAsync(Stream source, Stream target, EncryptionOptions options, CancellationToken cancellationToken = default)
    {
        if (options.Password is null)
            throw new ArgumentNullException(nameof(options.Password), "Password is required for AES decryption.");

        // Read salt and IV from the beginning of the source stream.
        byte[] salt = await ReadBytesAsync(source, DefaultSaltSizeBytes, cancellationToken).ConfigureAwait(false);
        byte[] iv = await ReadBytesAsync(source, DefaultIvSizeBytes, cancellationToken).ConfigureAwait(false);

        byte[] key = DeriveKeyFromPassword(options.Password, salt);

        using (var aes = CreateAesInstance())
        {
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            // Important: Dispose the CryptoStream to ensure all data is written to the target stream.
            using (var cryptoStream = new CryptoStream(source, aes.CreateDecryptor(), CryptoStreamMode.Read, leaveOpen: true))
            {
                await cryptoStream.CopyToAsync(target, _bufferSize, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    // Synchronous implementations call the Async ones for simplicity here
    // In a real-world high-performance scenario, dedicated sync implementations might be preferred.
    public virtual void Encrypt(Stream source, Stream target, EncryptionOptions options)
    {
        // Basic sync-over-async (consider dedicated sync logic if performance critical)
        EncryptAsync(source, target, options).GetAwaiter().GetResult(); 
    }

    public virtual void Decrypt(Stream source, Stream target, EncryptionOptions options)
    {
        // Basic sync-over-async 
        DecryptAsync(source, target, options).GetAwaiter().GetResult();
    }

    private Aes CreateAesInstance()
    {
        var aes = Aes.Create(); // Creates a new AES instance (defaults might vary, e.g., AesCryptoServiceProvider)
        if (aes is null)
            throw new PlatformNotSupportedException("AES algorithm is not supported on this platform.");
            
        aes.KeySize = KeySizeBits;
        aes.BlockSize = DefaultIvSizeBytes * 8; // AES block size is 128 bits
        return aes;
    }

    private byte[] GenerateSalt()
    {
        return RandomNumberGenerator.GetBytes(DefaultSaltSizeBytes);
    }

    private byte[] GenerateIv()
    {
        return RandomNumberGenerator.GetBytes(DefaultIvSizeBytes);
    }

    private byte[] DeriveKeyFromPassword(string password, byte[] salt)
    {
        // Use Rfc2898DeriveBytes (PBKDF2) for key derivation
        using (var kdf = new Rfc2898DeriveBytes(password, salt, DefaultPbkdf2Iterations, DefaultPbkdf2HashAlgorithm))
        {
            return kdf.GetBytes(KeySizeBytes); // Derive key of the required size
        }
    }

    private async Task<byte[]> ReadBytesAsync(Stream stream, int count, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[count];
        int bytesRead = 0;
        while (bytesRead < count)
        {
            int read = await stream.ReadAsync(buffer, bytesRead, count - bytesRead, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("Unexpected end of stream while reading salt or IV.");
            }
            bytesRead += read;
        }
        return buffer;
    }
} 