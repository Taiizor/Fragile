using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Fragile.Compression
{
    /// <summary>
    /// Abstract base class for compression algorithm providers
    /// </summary>
    public abstract class CompressionProvider
    {
        /// <summary>
        /// The compression algorithm used by this provider
        /// </summary>
        public abstract CompressionAlgorithm Algorithm { get; }
        
        /// <summary>
        /// Creates a compression provider for the specified algorithm and level
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use</param>
        /// <param name="level">The compression level</param>
        /// <returns>A suitable compression provider</returns>
        public static CompressionProvider Create(CompressionAlgorithm algorithm, CompressionLevel level)
        {
            return algorithm switch
            {
                CompressionAlgorithm.Store => new StoreCompressionProvider(),
                CompressionAlgorithm.Deflate => new DeflateCompressionProvider(level),
                // These would be implemented with additional libraries
                // CompressionAlgorithm.LZMA => new LZMACompressionProvider(level),
                // CompressionAlgorithm.BZip2 => new BZip2CompressionProvider(level),
                // CompressionAlgorithm.ZStd => new ZStdCompressionProvider(level),
                // CompressionAlgorithm.LZ4 => new LZ4CompressionProvider(level),
                _ => throw new NotSupportedException($"Compression algorithm {algorithm} is not supported")
            };
        }
        
        /// <summary>
        /// Compresses the input stream to the output stream
        /// </summary>
        /// <param name="input">Source stream to compress</param>
        /// <param name="output">Destination stream for compressed data</param>
        /// <param name="progress">Optional progress reporting</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The number of bytes written to the output stream</returns>
        public abstract Task<long> CompressAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Decompresses the input stream to the output stream
        /// </summary>
        /// <param name="input">Source stream with compressed data</param>
        /// <param name="output">Destination stream for decompressed data</param>
        /// <param name="progress">Optional progress reporting</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The number of bytes written to the output stream</returns>
        public abstract Task<long> DecompressAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets the estimated compressed size for the given input size based on the algorithm and compression level
        /// </summary>
        /// <param name="inputSize">The size of the input data in bytes</param>
        /// <returns>Estimated compressed size in bytes</returns>
        public abstract long EstimateCompressedSize(long inputSize);
    }
} 