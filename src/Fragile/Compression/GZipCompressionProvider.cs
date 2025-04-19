using System.IO.Compression;
using System.Text;

namespace Fragile.Compression
{
    /// <summary>
    /// Compression provider implementation using GZip algorithm
    /// </summary>
    /// <remarks>
    /// Creates a new GZip compression provider with the specified level and parallel processing options
    /// </remarks>
    /// <param name="level">Compression level</param>
    /// <param name="useParallelProcessing">Whether to use parallel processing</param>
    /// <param name="maxThreads">Maximum number of threads to use for parallel operations</param>
    internal class GZipCompressionProvider(CompressionLevel level, bool useParallelProcessing, int maxThreads) : CompressionProvider(useParallelProcessing, maxThreads)
    {
        /// <summary>
        /// Gets the compression algorithm used by this provider
        /// </summary>
        public override CompressionAlgorithm Algorithm => CompressionAlgorithm.GZip;

        /// <summary>
        /// Creates a new GZip compression provider with the specified level
        /// </summary>
        /// <param name="level">Compression level</param>
        public GZipCompressionProvider(CompressionLevel level) : this(level, true, Environment.ProcessorCount)
        {
        }

        /// <summary>
        /// Compresses the input stream to the output stream using GZip
        /// </summary>
        public override async Task<long> CompressAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long initialPosition = output.Position;

            // Map our compression level to System.IO.Compression level
            System.IO.Compression.CompressionLevel compressionLevel = level switch
            {
#if NET6_0_OR_GREATER
                CompressionLevel.Fastest => System.IO.Compression.CompressionLevel.Fastest,
                CompressionLevel.Fast => System.IO.Compression.CompressionLevel.Fastest,
                CompressionLevel.Normal => System.IO.Compression.CompressionLevel.Optimal,
                CompressionLevel.High => System.IO.Compression.CompressionLevel.SmallestSize,
                CompressionLevel.Ultra => System.IO.Compression.CompressionLevel.SmallestSize,
#else
                CompressionLevel.Fastest => System.IO.Compression.CompressionLevel.NoCompression,
                CompressionLevel.Fast => System.IO.Compression.CompressionLevel.NoCompression,
                CompressionLevel.Normal => System.IO.Compression.CompressionLevel.Fastest,
                CompressionLevel.High => System.IO.Compression.CompressionLevel.Optimal,
                CompressionLevel.Ultra => System.IO.Compression.CompressionLevel.Optimal,
#endif
                _ => System.IO.Compression.CompressionLevel.Optimal
            };

            // Use parallel compression if enabled and input stream supports seeking
            if (UseParallelProcessing && input.CanSeek && input.Length > 1024 * 1024) // Only for files larger than 1MB
            {
                await CompressParallelAsync(input, output, compressionLevel, progress, cancellationToken);
            }
            else
            {
                using GZipStream gzipStream = new(output, compressionLevel, true);
                // If input stream supports seeking, we can report progress
                bool canReportProgress = input.CanSeek;
                long totalBytes = canReportProgress ? input.Length : 0;
                long totalBytesRead = 0;
                byte[] buffer = new byte[81920]; // 80 KB buffer

                int bytesRead;

#if NET48_OR_GREATER || NETSTANDARD2_0
                while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
#else
                while ((bytesRead = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
#endif
                {
#if NET48_OR_GREATER || NETSTANDARD2_0
                    await gzipStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
#else
                    await gzipStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
#endif

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
        /// Decompresses the input stream to the output stream using GZip
        /// </summary>
        public override async Task<long> DecompressAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long initialPosition = output.Position;
            long initialInputPosition = input.Position;

            try
            {
                // First, try to detect if this is a parallel compressed file by reading the first few bytes
                if (input.CanSeek && input.Length > 4)
                {
                    using (BinaryReader reader = new(input, Encoding.UTF8, true))
                    {
                        try
                        {
                            // Read the number of chunks as an indicator that this is a parallel compressed file
                            int chunkCount = reader.ReadInt32();

                            // If the chunk count seems reasonable, assume this is a parallel compressed file
                            if (chunkCount is > 0 and < 1000) // Sanity check
                            {
                                // Read chunk information
                                List<(long Start, long Length, int CompressedLength)> chunks = [];
                                for (int i = 0; i < chunkCount; i++)
                                {
                                    long start = reader.ReadInt64();
                                    long length = reader.ReadInt64();
                                    int compressedLength = reader.ReadInt32();
                                    chunks.Add((start, length, compressedLength));
                                }

                                // Calculate total uncompressed size for progress reporting
                                long totalUncompressedSize = chunks.Sum(c => c.Length);
                                long totalProcessed = 0;

                                // Create a buffer large enough to hold all uncompressed data
                                using MemoryStream fullDecompressedData = new((int)totalUncompressedSize);

                                // Process each chunk
                                foreach ((long start, long length, int compressedLength) in chunks)
                                {
                                    // Read compressed data for this chunk
                                    byte[] compressedData = reader.ReadBytes(compressedLength);

                                    // Decompress chunk
                                    using (MemoryStream compressedStream = new(compressedData))
                                    using (GZipStream gzipStream = new(compressedStream, CompressionMode.Decompress))
                                    {
                                        // Position in full buffer where this chunk should go
                                        fullDecompressedData.Position = start;

                                        // Use fixed size buffer for reading
                                        byte[] buffer = new byte[81920]; // 80 KB buffer
                                        int bytesRead;
                                        long totalBytesRead = 0;

#if NET48_OR_GREATER || NETSTANDARD2_0
                                        while (totalBytesRead < length && (bytesRead = await gzipStream.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, length - totalBytesRead), cancellationToken)) > 0)
#else
                                        while (totalBytesRead < length && (bytesRead = await gzipStream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, length - totalBytesRead)), cancellationToken)) > 0)
#endif
                                        {
#if NET48_OR_GREATER || NETSTANDARD2_0
                                            await fullDecompressedData.WriteAsync(buffer, 0, bytesRead, cancellationToken);
#else
                                            await fullDecompressedData.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
#endif
                                            totalBytesRead += bytesRead;
                                        }
                                    }

                                    // Update progress
                                    totalProcessed += length;
                                    progress?.Report((double)totalProcessed / totalUncompressedSize);

                                    // Check for cancellation
                                    cancellationToken.ThrowIfCancellationRequested();
                                }

                                // Write full uncompressed data to output
                                fullDecompressedData.Position = 0;
                                await fullDecompressedData.CopyToAsync(output, 81920, cancellationToken);

                                // Final progress update
                                progress?.Report(1.0);

                                // Return the number of bytes written
                                return output.Position - initialPosition;
                            }
                        }
                        catch (EndOfStreamException)
                        {
                            // Not in the parallel compression format, reset and try standard decompression
                        }
                        catch (Exception ex)
                        {
                            // Try standard decompression if parallel decompression fails
                            throw new Exception($"Parallel decompression failed, trying standard method: {ex.Message}", ex);
                        }
                    }

                    // Reset input position to try standard decompression
                    input.Position = initialInputPosition;
                }

                // Standard decompression
                using (GZipStream gzipStream = new(input, CompressionMode.Decompress, true))
                {
                    // If we can seek the input, we might get the uncompressed size from the footer
                    bool canReportProgress = false;
                    long totalBytes = 0;

                    byte[] buffer = new byte[81920]; // 80 KB buffer
                    long totalBytesRead = 0;
                    int bytesRead;

#if NET48_OR_GREATER || NETSTANDARD2_0
                    while ((bytesRead = await gzipStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await output.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
#else
                    while ((bytesRead = await gzipStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
#endif

                        totalBytesRead += bytesRead;

                        // Report progress if possible
                        if (canReportProgress && progress != null && totalBytes > 0)
                        {
                            double progressValue = (double)totalBytesRead / totalBytes;
                            progress.Report(progressValue);
                        }

                        // Check for cancellation
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    // Final progress update
                    progress?.Report(1.0);
                }

                return output.Position - initialPosition;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to decompress using GZip: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the estimated compressed size for the given input size
        /// </summary>
        public override long EstimateCompressedSize(long inputSize)
        {
            // GZip typically achieves a compression ratio between 2:1 and 10:1
            // Adjust based on compression level
            double compressionRatio = level switch
            {
                CompressionLevel.Fastest => 1.2, // Minimal compression
                CompressionLevel.Fast => 2.0,
                CompressionLevel.Normal => 2.5,
                CompressionLevel.High => 3.0,
                CompressionLevel.Ultra => 3.5,
                _ => 2.5
            };

            // Add GZip overhead (about 18 bytes for header/footer plus up to 5 bytes per 32KB block)
            long overhead = 18 + (inputSize / 32768 * 5);
            return (long)(inputSize / compressionRatio) + overhead;
        }

        private async Task CompressParallelAsync(Stream input, Stream output, System.IO.Compression.CompressionLevel compressionLevel, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            // Get input size and calculate optimal chunk size
            long inputLength = input.Length;
            int processorCount = Math.Min(MaxThreads, Environment.ProcessorCount);
            long chunkSize = Math.Max(1024 * 1024, inputLength / processorCount); // Min 1MB chunks

            // Calculate the number of chunks
            int chunkCount = (int)Math.Ceiling((double)inputLength / chunkSize);

            // Collect chunk information before writing to output
            List<(long Start, long Length, byte[] CompressedData)> compressedChunks = [];

            // Report setup progress
            progress?.Report(0.0);

            // Process each chunk
            long totalBytesProcessed = 0;
            for (int i = 0; i < chunkCount; i++)
            {
                // Calculate chunk boundaries
                long start = i * chunkSize;
                long length = Math.Min(chunkSize, inputLength - start);

                // Read chunk data
                input.Position = start;
                byte[] buffer = new byte[length];


#if NET48_OR_GREATER || NETSTANDARD2_0
                int bytesRead = await input.ReadAsync(buffer, 0, (int)length, cancellationToken);
#else
                int bytesRead = await input.ReadAsync(buffer.AsMemory(0, (int)length), cancellationToken);
#endif

                // Compress the chunk
                using MemoryStream compressedStream = new();
                using (GZipStream gzipStream = new(compressedStream, compressionLevel))
                {
#if NET48_OR_GREATER || NETSTANDARD2_0
                    await gzipStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
#else
                    await gzipStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
#endif
                }

                // Add compressed chunk to collection
                compressedChunks.Add((start, length, compressedStream.ToArray()));

                // Update progress
                totalBytesProcessed += length;
                progress?.Report((double)totalBytesProcessed / inputLength * 0.9); // Use 90% for compression, 10% for writing

                // Check for cancellation
                cancellationToken.ThrowIfCancellationRequested();
            }

            // Write the parallel compression header
            using (BinaryWriter writer = new(output, Encoding.UTF8, true))
            {
                // Write the number of chunks
                writer.Write(chunkCount);

                // Write chunk metadata - each chunk has start position, length and compressed length
                foreach ((long start, long length, byte[] compressedData) in compressedChunks)
                {
                    writer.Write(start);
                    writer.Write(length);
                    writer.Write(compressedData.Length);
                }

                // Write the compressed data for each chunk
                foreach ((_, _, byte[] compressedData) in compressedChunks)
                {
                    writer.Write(compressedData);
                }
            }

            // Final progress update
            progress?.Report(1.0);
        }
    }
}