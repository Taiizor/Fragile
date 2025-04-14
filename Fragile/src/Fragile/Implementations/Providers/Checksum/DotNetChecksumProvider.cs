using Fragile.Core.Options;
using Fragile.Interfaces.Providers;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Fragile.Implementations.Providers.Checksum;

/// <summary>
/// Generic checksum provider using .NET's HashAlgorithm implementations (MD5, SHA1, SHA256, SHA512).
/// </summary>
internal class DotNetChecksumProvider : IChecksumProvider, IDisposable
{
    private readonly HashAlgorithm _hashAlgorithm;
    private bool _disposed = false;

    /// <summary>
    /// Gets the length of the hash produced by the underlying algorithm, in bytes.
    /// </summary>
    public int ChecksumLengthBytes => _hashAlgorithm.HashSize / 8;

    /// <summary>
    /// Initializes a new instance of the <see cref="DotNetChecksumProvider"/> class.
    /// </summary>
    /// <param name="hashAlgorithm">The specific HashAlgorithm instance (e.g., SHA256.Create()). The provider takes ownership and will dispose it.</param>
    public DotNetChecksumProvider(HashAlgorithm hashAlgorithm)
    {
        _hashAlgorithm = hashAlgorithm ?? throw new ArgumentNullException(nameof(hashAlgorithm));
    }

    public async Task<byte[]> ComputeChecksumAsync(Stream source, ChecksumOptions options, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DotNetChecksumProvider));

        // Store initial position and ensure stream is seekable if we need to reset
        long initialPosition = -1;
        bool canSeek = source.CanSeek;
        if (canSeek) initialPosition = source.Position;

        try
        {
            // HashAlgorithm.ComputeHashAsync is available in .NET Standard 2.1+
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
            byte[] hash = await _hashAlgorithm.ComputeHashAsync(source, cancellationToken).ConfigureAwait(false);
            return hash;
#else
            // Fallback for environments where ComputeHashAsync isn't directly available (e.g., might need manual chunking)
            // For simplicity, calling the sync version here, but a true async implementation would buffer.
            return await Task.Run(() => ComputeChecksum(source, options), cancellationToken);
#endif
        }
        finally
        {
            // Reset stream position if possible
            if (canSeek && initialPosition != -1)
            {
                try { source.Position = initialPosition; }
                catch (ObjectDisposedException) { /* Ignore if stream got disposed elsewhere */ }
            }
        }
    }

    public byte[] ComputeChecksum(Stream source, ChecksumOptions options)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DotNetChecksumProvider));

        long initialPosition = -1;
        bool canSeek = source.CanSeek;
        if (canSeek) initialPosition = source.Position;

        try
        {
            byte[] hash = _hashAlgorithm.ComputeHash(source);
            return hash;
        }
        finally
        {
            if (canSeek && initialPosition != -1)
            {
                try { source.Position = initialPosition; }
                catch (ObjectDisposedException) { /* Ignore */ }
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _hashAlgorithm?.Dispose();
            }
            _disposed = true;
        }
    }
} 