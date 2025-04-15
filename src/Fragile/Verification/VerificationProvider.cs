using Fragile.Models;

namespace Fragile.Verification
{
    /// <summary>
    /// Abstract base class for checksum algorithm providers
    /// </summary>
    public abstract class VerificationProvider
    {
        /// <summary>
        /// Whether to use parallel processing for checksum calculation
        /// </summary>
        public bool UseParallelProcessing { get; }

        /// <summary>
        /// Maximum number of threads to use for parallel operations
        /// </summary>
        public int MaxThreads { get; }

        /// <summary>
        /// The checksum algorithm used by this provider
        /// </summary>
        public abstract ChecksumAlgorithm Algorithm { get; }

        /// <summary>
        /// Constructor with parallel processing options
        /// </summary>
        /// <param name="useParallelProcessing">Whether to use parallel processing</param>
        /// <param name="maxThreads">Maximum number of threads to use</param>
        protected VerificationProvider(bool useParallelProcessing = false, int maxThreads = 1)
        {
            UseParallelProcessing = useParallelProcessing;
            MaxThreads = maxThreads;
        }

        /// <summary>
        /// Creates a verification provider for the specified algorithm
        /// </summary>
        /// <param name="algorithm">The checksum algorithm to use</param>
        /// <returns>A suitable verification provider</returns>
        public static VerificationProvider Create(ChecksumAlgorithm algorithm)
        {
            // Use default options
            return Create(new FragileOptions { ChecksumAlgorithm = algorithm, EnableChecksumVerification = true });
        }

        /// <summary>
        /// Creates a verification provider based on the provided options
        /// </summary>
        /// <param name="options">Options containing verification settings</param>
        /// <returns>A suitable verification provider</returns>
        public static VerificationProvider Create(FragileOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            // If checksum verification is disabled, use the None provider
            if (!options.EnableChecksumVerification)
            {
                return new NoneVerificationProvider(options.UseParallelProcessing, options.MaxThreads);
            }

            // Otherwise, create a provider based on the selected algorithm
            return options.ChecksumAlgorithm switch
            {
                ChecksumAlgorithm.None => new NoneVerificationProvider(options.UseParallelProcessing, options.MaxThreads),
                ChecksumAlgorithm.CRC32 => new Crc32VerificationProvider(options.UseParallelProcessing, options.MaxThreads),
                ChecksumAlgorithm.MD5 => new HashVerificationProvider(ChecksumAlgorithm.MD5, options.UseParallelProcessing, options.MaxThreads),
                ChecksumAlgorithm.SHA1 => new HashVerificationProvider(ChecksumAlgorithm.SHA1, options.UseParallelProcessing, options.MaxThreads),
                ChecksumAlgorithm.SHA256 => new HashVerificationProvider(ChecksumAlgorithm.SHA256, options.UseParallelProcessing, options.MaxThreads),
                ChecksumAlgorithm.SHA512 => new HashVerificationProvider(ChecksumAlgorithm.SHA512, options.UseParallelProcessing, options.MaxThreads),
                _ => throw new NotSupportedException($"Checksum algorithm {options.ChecksumAlgorithm} is not supported")
            };
        }

        /// <summary>
        /// Calculates the checksum for the input stream
        /// </summary>
        /// <param name="input">Stream to calculate checksum for</param>
        /// <param name="progress">Optional progress reporting</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Checksum as byte array</returns>
        public abstract Task<byte[]> CalculateChecksumAsync(Stream input, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies the checksum against the input stream
        /// </summary>
        /// <param name="input">Stream to verify</param>
        /// <param name="expectedChecksum">Expected checksum to verify against</param>
        /// <param name="progress">Optional progress reporting</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if checksum matches, false otherwise</returns>
        public abstract Task<bool> VerifyChecksumAsync(Stream input, byte[] expectedChecksum, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the size of the checksum in bytes
        /// </summary>
        /// <returns>Checksum size in bytes</returns>
        public abstract int GetChecksumSize();
    }
}