using Fragile.Core.Options;
using Fragile.Interfaces.Providers;

namespace Fragile.Implementations.Providers.Compression;

/// <summary>
/// Implements the 'Store' compression method (no actual compression).
/// Simply copies the source stream to the target stream.
/// </summary>
internal class StoreCompressionProvider : ICompressionProvider
{
    private readonly int _bufferSize;

    public StoreCompressionProvider(int bufferSize = 81920) // Use default buffer size from ArchiveOptions
    {
        _bufferSize = bufferSize > 0 ? bufferSize : 81920;
    }

    public async Task CompressAsync(Stream source, Stream target, CompressionOptions options, CancellationToken cancellationToken = default)
    {
        // 'Store' mode ignores compression level and other options
        await source.CopyToAsync(target, _bufferSize, cancellationToken).ConfigureAwait(false);
    }

    public async Task DecompressAsync(Stream source, Stream target, CompressionOptions options, CancellationToken cancellationToken = default)
    {
        // Decompression is the same as compression for 'Store' mode
        await source.CopyToAsync(target, _bufferSize, cancellationToken).ConfigureAwait(false);
    }

    public void Compress(Stream source, Stream target, CompressionOptions options)
    {
        source.CopyTo(target, _bufferSize);
    }

    public void Decompress(Stream source, Stream target, CompressionOptions options)
    {
        source.CopyTo(target, _bufferSize);
    }
}