using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace Fragile.Compression
{
    /// <summary>
    /// Compression provider implementation using Deflate algorithm
    /// </summary>
    internal class DeflateCompressionProvider : CompressionProvider
    {
        private readonly CompressionLevel _level;

        /// <summary>
        /// Gets the compression algorithm used by this provider
        /// </summary>
        public override CompressionAlgorithm Algorithm => CompressionAlgorithm.Deflate;

        /// <summary>
        /// Creates a new Deflate compression provider with the specified level
        /// </summary>
        /// <param name="level">Compression level</param>
        public DeflateCompressionProvider(CompressionLevel level)
        {
            _level = level;
        }

        /// <summary>
        /// Compresses the input stream to the output stream using Deflate
        /// </summary>
        public override async Task<long> CompressAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long initialPosition = output.Position;

            // Map our compression level to System.IO.Compression level
            System.IO.Compression.CompressionLevel compressionLevel = _level switch
            {
                CompressionLevel.Fastest => System.IO.Compression.CompressionLevel.Fastest,
                CompressionLevel.Fast => System.IO.Compression.CompressionLevel.Fastest,
                CompressionLevel.Normal => System.IO.Compression.CompressionLevel.Optimal,
                CompressionLevel.High => System.IO.Compression.CompressionLevel.Optimal,
                CompressionLevel.Ultra => System.IO.Compression.CompressionLevel.Optimal,
                _ => System.IO.Compression.CompressionLevel.Optimal
            };

            using (DeflateStream deflateStream = new(output, compressionLevel, true))
            {
                // If input stream supports seeking, we can report progress
                bool canReportProgress = input.CanSeek;
                long totalBytes = canReportProgress ? input.Length : 0;
                byte[] buffer = new byte[81920]; // 80 KB buffer

                int bytesRead;
                long totalBytesRead = 0;

                while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await deflateStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);

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
            }

            // Return the number of bytes written
            return output.Position - initialPosition;
        }

        /// <summary>
        /// Decompresses the input stream to the output stream using Deflate
        /// </summary>
        public override async Task<long> DecompressAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long initialPosition = output.Position;

            using (DeflateStream deflateStream = new(input, CompressionMode.Decompress, true))
            {
                byte[] buffer = new byte[81920]; // 80 KB buffer

                int bytesRead;
                while ((bytesRead = await deflateStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);

                    // We can't easily report progress for decompression without knowing the final size
                    // Progress will be reported based on the number of compressed bytes read if applicable

                    // Check for cancellation
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            // Return the number of bytes written
            return output.Position - initialPosition;
        }

        /// <summary>
        /// Estimates the compressed size based on the Deflate algorithm's average compression ratio
        /// </summary>
        public override long EstimateCompressedSize(long inputSize)
        {
            // Deflate typically achieves a compression ratio of about 2:1 to 3:1 for text
            // The ratio depends on the input data and compression level
            double ratio = _level switch
            {
                CompressionLevel.Fastest => 0.7,  // 30% reduction
                CompressionLevel.Fast => 0.6,     // 40% reduction
                CompressionLevel.Normal => 0.5,   // 50% reduction
                CompressionLevel.High => 0.4,     // 60% reduction
                CompressionLevel.Ultra => 0.35,   // 65% reduction
                _ => 0.5
            };

            return (long)(inputSize * ratio);
        }
    }
}