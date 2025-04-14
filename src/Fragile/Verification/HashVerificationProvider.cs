using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Fragile.Verification
{
    /// <summary>
    /// Verification provider using cryptographic hash algorithms
    /// </summary>
    internal class HashVerificationProvider : VerificationProvider
    {
        private readonly ChecksumAlgorithm _algorithm;
        
        /// <summary>
        /// The checksum algorithm used by this provider
        /// </summary>
        public override ChecksumAlgorithm Algorithm => _algorithm;
        
        /// <summary>
        /// Creates a new hash verification provider with the specified algorithm
        /// </summary>
        /// <param name="algorithm">The hash algorithm to use</param>
        public HashVerificationProvider(ChecksumAlgorithm algorithm)
        {
            if (algorithm != ChecksumAlgorithm.MD5 && 
                algorithm != ChecksumAlgorithm.SHA1 && 
                algorithm != ChecksumAlgorithm.SHA256 && 
                algorithm != ChecksumAlgorithm.SHA512)
            {
                throw new ArgumentException($"Unsupported hash algorithm: {algorithm}", nameof(algorithm));
            }
            
            _algorithm = algorithm;
        }
        
        /// <summary>
        /// Calculates hash for the input stream
        /// </summary>
        public override async Task<byte[]> CalculateChecksumAsync(Stream input, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            using HashAlgorithm hashAlgorithm = CreateHashAlgorithm();
            
            // If input stream supports seeking, we can report progress
            bool canReportProgress = input.CanSeek;
            long totalBytes = canReportProgress ? input.Length : 0;
            long totalBytesRead = 0;
            
            byte[] buffer = new byte[81920]; // 80 KB buffer
            int bytesRead;
            
            while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                // Update hash
                hashAlgorithm.TransformBlock(buffer, 0, bytesRead, null, 0);
                
                // Report progress if possible
                if (canReportProgress && progress != null)
                {
                    totalBytesRead += bytesRead;
                    double progressValue = (double)totalBytesRead / totalBytes;
                    progress.Report(progressValue);
                }
                
                // Check for cancellation
                cancellationToken.ThrowIfCancellationRequested();
            }
            
            // Finalize hash
            hashAlgorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            
            return hashAlgorithm.Hash;
        }
        
        /// <summary>
        /// Verifies hash against the input stream
        /// </summary>
        public override async Task<bool> VerifyChecksumAsync(Stream input, byte[] expectedChecksum, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (expectedChecksum == null || expectedChecksum.Length != GetChecksumSize())
            {
                return false; // Invalid checksum length
            }
            
            byte[] calculatedChecksum = await CalculateChecksumAsync(input, progress, cancellationToken).ConfigureAwait(false);
            
            // Compare checksums
            if (calculatedChecksum.Length != expectedChecksum.Length)
            {
                return false;
            }
            
            for (int i = 0; i < calculatedChecksum.Length; i++)
            {
                if (calculatedChecksum[i] != expectedChecksum[i])
                {
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Gets the size of the hash in bytes
        /// </summary>
        public override int GetChecksumSize()
        {
            return _algorithm switch
            {
                ChecksumAlgorithm.MD5 => 16,    // 128 bits = 16 bytes
                ChecksumAlgorithm.SHA1 => 20,   // 160 bits = 20 bytes
                ChecksumAlgorithm.SHA256 => 32, // 256 bits = 32 bytes
                ChecksumAlgorithm.SHA512 => 64, // 512 bits = 64 bytes
                _ => throw new NotSupportedException($"Unsupported hash algorithm: {_algorithm}")
            };
        }
        
        /// <summary>
        /// Creates the appropriate hash algorithm instance
        /// </summary>
        private HashAlgorithm CreateHashAlgorithm()
        {
            return _algorithm switch
            {
                ChecksumAlgorithm.MD5 => MD5.Create(),
                ChecksumAlgorithm.SHA1 => SHA1.Create(),
                ChecksumAlgorithm.SHA256 => SHA256.Create(),
                ChecksumAlgorithm.SHA512 => SHA512.Create(),
                _ => throw new NotSupportedException($"Unsupported hash algorithm: {_algorithm}")
            };
        }
    }
} 