namespace Fragile.Verification
{
    /// <summary>
    /// Verification provider that doesn't perform any checksum calculation
    /// </summary>
    internal class NoneVerificationProvider : VerificationProvider
    {
        /// <summary>
        /// Creates a new NoneVerificationProvider with default settings
        /// </summary>
        public NoneVerificationProvider()
            : base(false, 1)
        {
        }

        /// <summary>
        /// Creates a new NoneVerificationProvider with the specified parallel processing options
        /// </summary>
        /// <param name="useParallelProcessing">Whether to use parallel processing</param>
        /// <param name="maxThreads">Maximum number of threads to use</param>
        public NoneVerificationProvider(bool useParallelProcessing, int maxThreads)
            : base(useParallelProcessing, maxThreads)
        {
        }

        /// <summary>
        /// The checksum algorithm used by this provider
        /// </summary>
        public override ChecksumAlgorithm Algorithm => ChecksumAlgorithm.None;

        /// <summary>
        /// Returns an empty byte array since no checksum is calculated
        /// </summary>
        public override Task<byte[]> CalculateChecksumAsync(Stream input, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            // Report 100% progress immediately
            progress?.Report(1.0);

            // Return empty array as checksum
            return Task.FromResult(Array.Empty<byte>());
        }

        /// <summary>
        /// Always returns true since no verification is performed
        /// </summary>
        public override Task<bool> VerifyChecksumAsync(Stream input, byte[] expectedChecksum, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            // Report 100% progress immediately
            progress?.Report(1.0);

            // Always return true (verification success)
            return Task.FromResult(true);
        }

        /// <summary>
        /// Returns 0 since no checksum is used
        /// </summary>
        public override int GetChecksumSize()
        {
            return 0;
        }
    }
}