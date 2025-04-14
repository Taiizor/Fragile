using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Fragile.Verification
{
    /// <summary>
    /// Verification provider that doesn't perform any checksum calculation
    /// </summary>
    internal class NoneVerificationProvider : VerificationProvider
    {
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