using System;

namespace Fragile.Compression
{
    /// <summary>
    /// Defines the compression algorithms supported by the Fragile library.
    /// </summary>
    public enum CompressionAlgorithm
    {
        /// <summary>
        /// No compression, files are stored as-is.
        /// </summary>
        Store,

        /// <summary>
        /// Standard compression compatible with ZIP.
        /// </summary>
        Deflate,

        /// <summary>
        /// LZMA compression (not currently implemented).
        /// </summary>
        LZMA,

        /// <summary>
        /// BZip2 compression (not currently implemented).
        /// </summary>
        BZip2,

        /// <summary>
        /// ZStd compression (not currently implemented).
        /// </summary>
        ZStd,

        /// <summary>
        /// LZ4 compression (not currently implemented).
        /// </summary>
        LZ4
    }

    /// <summary>
    /// Defines the compression levels for the algorithms.
    /// </summary>
    public enum CompressionLevel
    {
        /// <summary>
        /// Fastest compression, lowest ratio.
        /// </summary>
        Fastest,

        /// <summary>
        /// Fast compression, moderate ratio.
        /// </summary>
        Fast,

        /// <summary>
        /// Balanced compression speed and ratio.
        /// </summary>
        Normal,

        /// <summary>
        /// High compression ratio, slower speed.
        /// </summary>
        High,

        /// <summary>
        /// Ultra compression, highest ratio, slowest speed.
        /// </summary>
        Ultra
    }

    /// <summary>
    /// Configuration options for compression operations.
    /// </summary>
    public class CompressionOptions
    {
        /// <summary>
        /// Gets or sets the compression algorithm to use.
        /// </summary>
        public CompressionAlgorithm Algorithm { get; set; } = CompressionAlgorithm.Deflate;

        /// <summary>
        /// Gets or sets the compression level.
        /// </summary>
        public CompressionLevel Level { get; set; } = CompressionLevel.Normal;

        /// <summary>
        /// Gets or sets a value indicating whether to use solid compression mode for better compression of similar files.
        /// </summary>
        public bool UseSolidMode { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to use parallel compression/decompression using multiple threads.
        /// </summary>
        public bool UseParallelProcessing { get; set; } = true;

        /// <summary>
        /// Gets or sets the number of threads to use for parallel processing.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than 1.</exception>
        public int ThreadCount
        {
            get => _threadCount;
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(ThreadCount), "Thread count must be at least 1.");
                _threadCount = value;
            }
        }

        private int _threadCount = Environment.ProcessorCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompressionOptions"/> class with default values.
        /// </summary>
        public CompressionOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompressionOptions"/> class with the specified algorithm and level.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use.</param>
        /// <param name="level">The compression level to use.</param>
        public CompressionOptions(CompressionAlgorithm algorithm, CompressionLevel level)
        {
            Algorithm = algorithm;
            Level = level;
        }
    }
} 