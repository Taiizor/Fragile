using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Fragile.Verification
{
    /// <summary>
    /// Verification provider using CRC32 checksum algorithm
    /// </summary>
    internal class Crc32VerificationProvider : VerificationProvider
    {
        private const uint Polynomial = 0xEDB88320;
        private static readonly uint[] CrcTable;
        
        /// <summary>
        /// Static constructor to initialize CRC table
        /// </summary>
        static Crc32VerificationProvider()
        {
            // Initialize CRC table
            CrcTable = new uint[256];
            
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                {
                    crc = (crc & 1) == 1 ? (crc >> 1) ^ Polynomial : crc >> 1;
                }
                CrcTable[i] = crc;
            }
        }
        
        /// <summary>
        /// The checksum algorithm used by this provider
        /// </summary>
        public override ChecksumAlgorithm Algorithm => ChecksumAlgorithm.CRC32;
        
        /// <summary>
        /// Calculates CRC32 checksum for the input stream
        /// </summary>
        public override async Task<byte[]> CalculateChecksumAsync(Stream input, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            // If input stream supports seeking, we can report progress
            bool canReportProgress = input.CanSeek;
            long totalBytes = canReportProgress ? input.Length : 0;
            long totalBytesRead = 0;
            
            uint crc = 0xFFFFFFFF;
            byte[] buffer = new byte[81920]; // 80 KB buffer
            
            int bytesRead;
            while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                // Update CRC for this chunk
                for (int i = 0; i < bytesRead; i++)
                {
                    crc = (crc >> 8) ^ CrcTable[(crc & 0xFF) ^ buffer[i]];
                }
                
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
            
            // Finalize CRC
            crc ^= 0xFFFFFFFF;
            
            // Convert to byte array (little-endian)
            return new byte[]
            {
                (byte)(crc & 0xFF),
                (byte)((crc >> 8) & 0xFF),
                (byte)((crc >> 16) & 0xFF),
                (byte)((crc >> 24) & 0xFF)
            };
        }
        
        /// <summary>
        /// Verifies CRC32 checksum against the input stream
        /// </summary>
        public override async Task<bool> VerifyChecksumAsync(Stream input, byte[] expectedChecksum, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (expectedChecksum == null || expectedChecksum.Length != 4)
            {
                return false; // Invalid checksum length
            }
            
            byte[] calculatedChecksum = await CalculateChecksumAsync(input, progress, cancellationToken).ConfigureAwait(false);
            
            // Compare checksums
            for (int i = 0; i < 4; i++)
            {
                if (calculatedChecksum[i] != expectedChecksum[i])
                {
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// CRC32 is 4 bytes
        /// </summary>
        public override int GetChecksumSize()
        {
            return 4; // 32 bits = 4 bytes
        }
    }
} 