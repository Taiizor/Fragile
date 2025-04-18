namespace Fragile.Compression
{
    /// <summary>
    /// Abstract base class for compression algorithm providers
    /// </summary>
    /// <remarks>
    /// Constructor for compression provider with parallel processing options
    /// </remarks>
    /// <param name="useParallelProcessing">Whether to use parallel processing</param>
    /// <param name="maxThreads">Maximum number of threads to use</param>
    public abstract class CompressionProvider(bool useParallelProcessing = true, int maxThreads = 1)
    {
        /// <summary>
        /// Maximum number of threads to use for parallel operations
        /// </summary>
        public int MaxThreads { get; } = maxThreads;

        /// <summary>
        /// The compression algorithm used by this provider
        /// </summary>
        public abstract CompressionAlgorithm Algorithm { get; }

        /// <summary>
        /// Whether to use parallel processing for compression/decompression
        /// </summary>
        public bool UseParallelProcessing { get; } = useParallelProcessing;

        /// <summary>
        /// Creates a compression provider for the specified algorithm and level
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use</param>
        /// <param name="level">The compression level</param>
        /// <returns>A suitable compression provider</returns>
        public static CompressionProvider Create(CompressionAlgorithm algorithm, CompressionLevel level)
        {
            return Create(algorithm, level, true, Environment.ProcessorCount);
        }

        /// <summary>
        /// Creates a compression provider for the specified algorithm, level, and parallel processing options
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use</param>
        /// <param name="level">The compression level</param>
        /// <param name="useParallelProcessing">Whether to use parallel processing</param>
        /// <param name="maxThreads">Maximum number of threads to use for parallel operations</param>
        /// <returns>A suitable compression provider</returns>
        public static CompressionProvider Create(CompressionAlgorithm algorithm, CompressionLevel level, bool useParallelProcessing, int maxThreads)
        {
            return algorithm switch
            {
                CompressionAlgorithm.Store => new StoreCompressionProvider(useParallelProcessing, maxThreads),
                CompressionAlgorithm.Brotli => new BrotliCompressionProvider(level, useParallelProcessing, maxThreads),
                CompressionAlgorithm.Deflate => new DeflateCompressionProvider(level, useParallelProcessing, maxThreads),
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