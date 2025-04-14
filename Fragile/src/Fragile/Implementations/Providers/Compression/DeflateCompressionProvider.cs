using Fragile.Core.Enums;
using Fragile.Core.Options;
using Fragile.Interfaces.Providers;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace Fragile.Implementations.Providers.Compression;

/// <summary>
/// Implements compression and decompression using the Deflate algorithm provided by .NET.
/// </summary>
internal class DeflateCompressionProvider : ICompressionProvider
{
    private readonly int _bufferSize;

    public DeflateCompressionProvider(int bufferSize = 81920)
    {
        _bufferSize = bufferSize > 0 ? bufferSize : 81920;
    }

    public async Task CompressAsync(Stream source, Stream target, CompressionOptions options, CancellationToken cancellationToken = default)
    {
        var compressionLevel = MapCompressionLevel(options.Level);
        // DeflateStream needs to be disposed to flush final blocks.
        // leaveOpen: true ensures the target stream is not closed when DeflateStream is disposed.
        using (var deflateStream = new DeflateStream(target, compressionLevel, leaveOpen: true))
        {
            await source.CopyToAsync(deflateStream, _bufferSize, cancellationToken).ConfigureAwait(false);
        }
        // No need to FlushAsync explicitly on deflateStream as Dispose handles it.
    }

    public async Task DecompressAsync(Stream source, Stream target, CompressionOptions options, CancellationToken cancellationToken = default)
    {
        // leaveOpen: true ensures the source stream is not closed when DeflateStream is disposed.
        using (var deflateStream = new DeflateStream(source, CompressionMode.Decompress, leaveOpen: true))
        {
            await deflateStream.CopyToAsync(target, _bufferSize, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Compress(Stream source, Stream target, CompressionOptions options)
    {
        var compressionLevel = MapCompressionLevel(options.Level);
        using (var deflateStream = new DeflateStream(target, compressionLevel, leaveOpen: true))
        {
            source.CopyTo(deflateStream, _bufferSize);
        }
    }

    public void Decompress(Stream source, Stream target, CompressionOptions options)
    {
        using (var deflateStream = new DeflateStream(source, CompressionMode.Decompress, leaveOpen: true))
        {
            deflateStream.CopyTo(target, _bufferSize);
        }
    }

    /// <summary>
    /// Maps the custom Fragile CompressionLevel enum to the .NET System.IO.Compression.CompressionLevel.
    /// </summary>
    private static System.IO.Compression.CompressionLevel MapCompressionLevel(Core.Enums.CompressionLevel level)
    {
        return level switch
        {
            Core.Enums.CompressionLevel.Fastest => System.IO.Compression.CompressionLevel.Fastest,
            Core.Enums.CompressionLevel.Fast => System.IO.Compression.CompressionLevel.Fastest, // Map Fast also to Fastest
            Core.Enums.CompressionLevel.Normal => System.IO.Compression.CompressionLevel.Optimal, // Default
            Core.Enums.CompressionLevel.High => System.IO.Compression.CompressionLevel.Optimal, // Map High also to Optimal
            Core.Enums.CompressionLevel.Ultra => System.IO.Compression.CompressionLevel.Optimal,
            // Case where Store algorithm uses Deflate provider shouldn't happen, but map it reasonably.
            // Also handles potential future levels if not mapped explicitly.
            _ => System.IO.Compression.CompressionLevel.Optimal,
        };
        // Note: DeflateStream doesn't have a direct equivalent for all levels, so we map to the closest.
        // System.IO.Compression.CompressionLevel.NoCompression exists but DeflateStream is specifically for compression.
    }
} 