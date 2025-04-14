using Fragile.Core.Enums;
using Fragile.Core.Options;
using Fragile.Implementations.Providers.Checksum;
using Fragile.Implementations.Providers.Compression;
using Fragile.Implementations.Providers.Encryption;
using Fragile.Implementations.Providers.ErrorCorrection;
using Fragile.Interfaces.Providers;
using System;
using System.Security.Cryptography;

namespace Fragile.Implementations;

/// <summary>
/// Factory class responsible for creating instances of algorithm providers based on options.
/// </summary>
internal static class ProviderFactory
{
    /// <summary>
    /// Gets the appropriate compression provider based on the specified algorithm.
    /// </summary>
    /// <param name="algorithm">The compression algorithm.</param>
    /// <param name="bufferSize">The buffer size to use for stream operations.</param>
    /// <returns>An instance of <see cref="ICompressionProvider"/>.</returns>
    /// <exception cref="NotSupportedException">Thrown if the algorithm is not supported.</exception>
    public static ICompressionProvider GetCompressionProvider(CompressionAlgorithm algorithm, int bufferSize)
    {
        return algorithm switch
        {
            CompressionAlgorithm.Store => new StoreCompressionProvider(bufferSize),
            CompressionAlgorithm.Deflate => new DeflateCompressionProvider(bufferSize),
            // Add cases for Lzma, BZip2, Zstd, Lz4 when implemented
            _ => throw new NotSupportedException($"Compression algorithm '{algorithm}' is not currently supported.")
        };
    }

    /// <summary>
    /// Gets the appropriate encryption provider based on the specified algorithm.
    /// </summary>
    /// <param name="algorithm">The encryption algorithm.</param>
    /// <param name="bufferSize">The buffer size to use for stream operations.</param>
    /// <returns>An instance of <see cref="IEncryptionProvider"/>.</returns>
    /// <exception cref="NotSupportedException">Thrown if the algorithm is not supported or is None.</exception>
    public static IEncryptionProvider GetEncryptionProvider(EncryptionAlgorithm algorithm, int bufferSize)
    {
        return algorithm switch
        {
            EncryptionAlgorithm.None => throw new NotSupportedException("Cannot get an encryption provider for algorithm 'None'."), // Or return a null provider? Depends on usage.
            EncryptionAlgorithm.Aes128 => new Aes128EncryptionProvider(bufferSize),
            EncryptionAlgorithm.Aes256 => new Aes256EncryptionProvider(bufferSize),
            // Add cases for ChaCha20, Twofish when implemented
            _ => throw new NotSupportedException($"Encryption algorithm '{algorithm}' is not currently supported.")
        };
    }

    /// <summary>
    /// Gets the appropriate checksum provider based on the specified algorithm.
    /// </summary>
    /// <param name="algorithm">The checksum algorithm.</param>
    /// <param name="bufferSize">The buffer size to use for stream operations (relevant for CRC32).</param>
    /// <returns>An instance of <see cref="IChecksumProvider"/>. Needs to be disposed if it's a DotNetChecksumProvider.</returns>
    /// <exception cref="NotSupportedException">Thrown if the algorithm is not supported or is None.</exception>
    /// <remarks>
    /// The caller is responsible for disposing the returned provider if it implements IDisposable (e.g., DotNetChecksumProvider).
    /// </remarks>
    public static IChecksumProvider GetChecksumProvider(ChecksumAlgorithm algorithm, int bufferSize)
    {
        return algorithm switch
        {
            ChecksumAlgorithm.None => throw new NotSupportedException("Cannot get a checksum provider for algorithm 'None'."),
            ChecksumAlgorithm.Crc32 => new Crc32ChecksumProvider(bufferSize),
            ChecksumAlgorithm.Md5 => new DotNetChecksumProvider(MD5.Create()),
            ChecksumAlgorithm.Sha1 => new DotNetChecksumProvider(SHA1.Create()),
            ChecksumAlgorithm.Sha256 => new DotNetChecksumProvider(SHA256.Create()),
            ChecksumAlgorithm.Sha512 => new DotNetChecksumProvider(SHA512.Create()),
            _ => throw new NotSupportedException($"Checksum algorithm '{algorithm}' is not currently supported.")
        };
    }

    /// <summary>
    /// Gets the appropriate error correction provider based on the specified level.
    /// </summary>
    /// <param name="level">The error correction level.</param>
    /// <returns>An instance of <see cref="IErrorCorrectionProvider"/>.</returns>
    /// <exception cref="NotSupportedException">Thrown if the level requires an unimplemented provider.</exception>
    public static IErrorCorrectionProvider GetErrorCorrectionProvider(ErrorCorrectionLevel level)
    {
        // Currently, only returns the placeholder/not-implemented provider.
        // When Reed-Solomon or others are added, logic will go here.
        if (level == ErrorCorrectionLevel.None)
        {
            // Perhaps return a specific 'Null' provider that does nothing?
            // For now, relying on caller to check options.Level.
            throw new NotSupportedException("Cannot get an error correction provider for level 'None'."); 
        }
        else
        {
            // Return the stub until a real implementation exists.
            return new NotImplementedErrorCorrectionProvider();
            // Example for future:
            // return level switch
            // {
            //     ErrorCorrectionLevel.Low => new ReedSolomonProvider(config_low),
            //     ErrorCorrectionLevel.Medium => new ReedSolomonProvider(config_medium),
            //     // ... etc ...
            //     _ => throw new NotSupportedException($"Error correction level '{level}' is not currently supported.")
            // };
        }
    }
} 