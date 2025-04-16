using Fragile.Models;

namespace Fragile.ErrorCorrection
{
    /// <summary>
    /// Abstract base class for error correction provider algorithm
    /// </summary>
    public abstract class ErrorCorrectionProvider
    {
        /// <summary>
        /// Maximum number of threads to use for parallel operations
        /// </summary>
        public int MaxThreads { get; }

        /// <summary>
        /// Error correction level (as percentage)
        /// </summary>
        public int CorrectionLevel { get; }

        /// <summary>
        /// Whether to use parallel processing for error correction operations
        /// </summary>
        public bool UseParallelProcessing { get; }

        /// <summary>
        /// Creates a new error correction provider
        /// </summary>
        /// <param name="correctionLevel">Error correction level (as percentage)</param>
        /// <param name="useParallelProcessing">Whether to use parallel processing</param>
        /// <param name="maxThreads">Maximum number of threads to use</param>
        protected ErrorCorrectionProvider(int correctionLevel, bool useParallelProcessing = false, int maxThreads = 1)
        {
            if (correctionLevel is < 0 or > 50)
            {
                throw new ArgumentOutOfRangeException(nameof(correctionLevel), "Error correction level must be between 0-50");
            }

            CorrectionLevel = correctionLevel;
            UseParallelProcessing = useParallelProcessing;
            MaxThreads = maxThreads > 0 ? maxThreads : Environment.ProcessorCount;
        }

        /// <summary>
        /// Creates an error correction provider with the specified level
        /// </summary>
        /// <param name="correctionLevel">Error correction level (between 0-50)</param>
        /// <returns>Error correction provider</returns>
        public static ErrorCorrectionProvider Create(int correctionLevel)
        {
            return Create(new FragileOptions { ErrorCorrectionLevel = correctionLevel });
        }

        /// <summary>
        /// Creates an error correction provider with the specified options
        /// </summary>
        /// <param name="options">Options containing error correction settings</param>
        /// <returns>Error correction provider</returns>
        public static ErrorCorrectionProvider Create(FragileOptions options)
        {
#if NET48_OR_GREATER || NETSTANDARD2_0_OR_GREATER
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
#else
            ArgumentNullException.ThrowIfNull(options);
#endif

            if (options.ErrorCorrectionLevel <= 0 || !options.EnableErrorCorrection)
            {
                return new NoneErrorCorrectionProvider(options.UseParallelProcessing, options.MaxThreads);
            }

            return new ReedSolomonErrorCorrectionProvider(options.ErrorCorrectionLevel, options.UseParallelProcessing, options.MaxThreads);
        }

        /// <summary>
        /// Adds error correction codes to data
        /// </summary>
        /// <param name="input">Data stream to apply error correction</param>
        /// <param name="output">Data stream with error correction codes added</param>
        /// <param name="progress">Progress notification</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Total number of bytes written</returns>
        public abstract Task<long> AddErrorCorrectionAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Corrects data and removes error correction codes
        /// </summary>
        /// <param name="input">Error correction coded data stream</param>
        /// <param name="output">Corrected data stream</param>
        /// <param name="reportRepairs">Repair notification callback function</param>
        /// <param name="progress">Progress notification</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Value pair containing (bytes written, bytes repaired) - total number of bytes written and number of bytes corrected</returns>
        public abstract Task<(long bytesWritten, int bytesRepaired)> CorrectErrorsAsync(Stream input, Stream output, Action<long, int>? reportRepairs = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates the additional data size required for error correction
        /// </summary>
        /// <param name="dataSize">Original data size</param>
        /// <returns>Additional size required for error correction data</returns>
        public abstract long CalculateOverhead(long dataSize);
    }
}