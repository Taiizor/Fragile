using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Fragile.Compression
{
    /// <summary>
    /// Compression provider that stores files without compression
    /// </summary>
    internal class StoreCompressionProvider : CompressionProvider
    {
        /// <summary>
        /// Gets the compression algorithm used by this provider
        /// </summary>
        public override CompressionAlgorithm Algorithm => CompressionAlgorithm.Store;
        
        /// <summary>
        /// "Compresses" the input stream to the output stream (actually just copies it)
        /// </summary>
        public override async Task<long> CompressAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long initialPosition = output.Position;
            
            // Simply copy the stream without compression
            byte[] buffer = new byte[81920]; // 80 KB buffer
            
            // If input stream supports seeking, we can report progress
            bool canReportProgress = input.CanSeek;
            long totalBytes = canReportProgress ? input.Length : 0;
            long totalBytesRead = 0;
            
            int bytesRead;
            while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                
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
            
            // Return the number of bytes written
            return output.Position - initialPosition;
        }
        
        /// <summary>
        /// "Decompresses" the input stream to the output stream (actually just copies it since no compression is used)
        /// </summary>
        public override async Task<long> DecompressAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            // Since Store just directly saves the data, decompression is the same as compression (simple copy)
            return await CompressAsync(input, output, progress, cancellationToken).ConfigureAwait(false);
        }
        
        /// <summary>
        /// Returns the input size since Store does not compress data
        /// </summary>
        public override long EstimateCompressedSize(long inputSize)
        {
            // Store doesn't compress, so the output size is the same as the input size
            return inputSize;
        }
    }
} 