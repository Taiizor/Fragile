using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Fragile.Verification
{
    /// <summary>
    /// Abstract base class for checksum algorithm providers
    /// </summary>
    public abstract class VerificationProvider
    {
        /// <summary>
        /// The checksum algorithm used by this provider
        /// </summary>
        public abstract ChecksumAlgorithm Algorithm { get; }
        
        /// <summary>
        /// Creates a verification provider for the specified algorithm
        /// </summary>
        /// <param name="algorithm">The checksum algorithm to use</param>
        /// <returns>A suitable verification provider</returns>
        public static VerificationProvider Create(ChecksumAlgorithm algorithm)
        {
            return algorithm switch
            {
                ChecksumAlgorithm.None => new NoneVerificationProvider(),
                ChecksumAlgorithm.CRC32 => new Crc32VerificationProvider(),
                ChecksumAlgorithm.MD5 => new HashVerificationProvider(ChecksumAlgorithm.MD5),
                ChecksumAlgorithm.SHA1 => new HashVerificationProvider(ChecksumAlgorithm.SHA1),
                ChecksumAlgorithm.SHA256 => new HashVerificationProvider(ChecksumAlgorithm.SHA256),
                ChecksumAlgorithm.SHA512 => new HashVerificationProvider(ChecksumAlgorithm.SHA512),
                _ => throw new NotSupportedException($"Checksum algorithm {algorithm} is not supported")
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