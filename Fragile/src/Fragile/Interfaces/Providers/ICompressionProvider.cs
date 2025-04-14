using Fragile.Core.Options;

namespace Fragile.Interfaces.Providers;

/// <summary>
/// Defines the interface for a compression algorithm provider.
/// Implementations of this interface handle the actual compression and decompression logic.
/// </summary>
public interface ICompressionProvider
{
    /// <summary>
    /// Compresses the source stream into the target stream asynchronously.
    /// </summary>
    /// <param name="source">The stream containing the data to compress.</param>
    /// <param name="target">The stream to write the compressed data to.</param>
    /// <param name="options">Compression options specific to this operation (e.g., level).</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous compression operation.</returns>
    Task CompressAsync(Stream source, Stream target, CompressionOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decompresses the source stream into the target stream asynchronously.
    /// </summary>
    /// <param name="source">The stream containing the compressed data.</param>
    /// <param name="target">The stream to write the decompressed data to.</param>
    /// <param name="options">Options potentially needed for decompression (though often fewer than compression).</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous decompression operation.</returns>
    Task DecompressAsync(Stream source, Stream target, CompressionOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compresses the source stream into the target stream.
    /// </summary>
    /// <param name="source">The stream containing the data to compress.</param>
    /// <param name="target">The stream to write the compressed data to.</param>
    /// <param name="options">Compression options specific to this operation (e.g., level).</param>
    void Compress(Stream source, Stream target, CompressionOptions options);

    /// <summary>
    /// Decompresses the source stream into the target stream.
    /// </summary>
    /// <param name="source">The stream containing the compressed data.</param>
    /// <param name="target">The stream to write the decompressed data to.</param>
    /// <param name="options">Options potentially needed for decompression.</param>
    void Decompress(Stream source, Stream target, CompressionOptions options);

    // Optional: A property to indicate if parallel processing is supported by this provider
    // bool SupportsParallelProcessing { get; }
}