using System.IO.Compression;
using System.Text;

namespace Fragile.Compression
{
    /// <summary>
    /// Compression provider implementation using Deflate algorithm
    /// </summary>
    /// <remarks>
    /// Creates a new Deflate compression provider with the specified level and parallel processing options
    /// </remarks>
    /// <param name="level">Compression level</param>
    /// <param name="useParallelProcessing">Whether to use parallel processing</param>
    /// <param name="maxThreads">Maximum number of threads to use for parallel operations</param>
    internal class DeflateCompressionProvider(CompressionLevel level, bool useParallelProcessing, int maxThreads) : CompressionProvider(useParallelProcessing, maxThreads)
    {
        /// <summary>
        /// Gets the compression algorithm used by this provider
        /// </summary>
        public override CompressionAlgorithm Algorithm => CompressionAlgorithm.Deflate;

        /// <summary>
        /// Creates a new Deflate compression provider with the specified level
        /// </summary>
        /// <param name="level">Compression level</param>
        public DeflateCompressionProvider(CompressionLevel level) : this(level, true, Environment.ProcessorCount)
        {
        }

        /// <summary>
        /// Compresses the input stream to the output stream using Deflate
        /// </summary>
        public override async Task<long> CompressAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long initialPosition = output.Position;

            // Map our compression level to System.IO.Compression level
            System.IO.Compression.CompressionLevel compressionLevel = level switch
            {
                CompressionLevel.Fastest => System.IO.Compression.CompressionLevel.NoCompression,
                CompressionLevel.Fast => System.IO.Compression.CompressionLevel.NoCompression,
                CompressionLevel.Normal => System.IO.Compression.CompressionLevel.Fastest,
                CompressionLevel.High => System.IO.Compression.CompressionLevel.Fastest,
                CompressionLevel.Ultra => System.IO.Compression.CompressionLevel.Optimal,
                _ => System.IO.Compression.CompressionLevel.Optimal
            };

            // Use parallel compression if enabled and input stream supports seeking
            if (UseParallelProcessing && input.CanSeek && input.Length > 1024 * 1024) // Only for files larger than 1MB
            {
                await CompressParallelAsync(input, output, compressionLevel, progress, cancellationToken);
            }
            else
            {
                using DeflateStream deflateStream = new(output, compressionLevel, true);
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
                    await deflateStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
#else
                    await deflateStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
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
        /// Decompresses the input stream to the output stream using Deflate
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
                    using (BinaryReader reader = new(input, System.Text.Encoding.UTF8, true))
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
                                    using (DeflateStream deflateStream = new(compressedStream, CompressionMode.Decompress))
                                    {
                                        // Position in full buffer where this chunk should go
                                        fullDecompressedData.Position = start;

                                        // Use fixed size buffer for reading
                                        byte[] buffer = new byte[81920]; // 80 KB buffer
                                        int bytesRead;
                                        long totalBytesRead = 0;

#if NET48_OR_GREATER || NETSTANDARD2_0
                                        while (totalBytesRead < length && (bytesRead = await deflateStream.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, length - totalBytesRead), cancellationToken)) > 0)
#else
                                        while (totalBytesRead < length && (bytesRead = await deflateStream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, length - totalBytesRead)), cancellationToken)) > 0)
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

                    // Reset input stream position to try standard decompression
                    input.Position = initialInputPosition;
                }

                // If not a parallel compressed file or the detection failed, try standard decompression
                using MemoryStream tempBuffer = new();
                // First, decompress to a memory buffer
                using (DeflateStream deflateStream = new(input, CompressionMode.Decompress, true))
                {
                    byte[] buffer = new byte[81920]; // 80 KB buffer
                    long totalBytes = 0;
                    int bytesRead;

#if NET48_OR_GREATER || NETSTANDARD2_0
                    while ((bytesRead = await deflateStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
#else
                    while ((bytesRead = await deflateStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
#endif
                    {
#if NET48_OR_GREATER || NETSTANDARD2_0
                        await tempBuffer.WriteAsync(buffer, 0, bytesRead, cancellationToken);
#else
                        await tempBuffer.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
#endif
                        totalBytes += bytesRead;

                        // Report approximate progress if possible
                        if (input.CanSeek && progress != null)
                        {
                            double progressValue = Math.Min(0.95, (double)input.Position / input.Length);
                            progress.Report(progressValue);
                        }

                        // Check for cancellation
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }

                // Then write the complete buffer to the output
                tempBuffer.Position = 0;
                await tempBuffer.CopyToAsync(output, 81920, cancellationToken);

                // Final progress update
                progress?.Report(1.0);
            }
            catch (Exception ex)
            {
                // Add more context to the exception
                throw new InvalidDataException($"Failed to decompress using Deflate algorithm: {ex.Message}", ex);
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
            double ratio = level switch
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

        /// <summary>
        /// Compresses the input stream to the output stream using parallel processing
        /// </summary>
        private async Task CompressParallelAsync(Stream input, Stream output, System.IO.Compression.CompressionLevel compressionLevel,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long fileLength = input.Length;
            long originalPosition = input.Position;

            // Calculate chunk size based on file size and number of threads
            int threadCount = Math.Min(MaxThreads, Environment.ProcessorCount);
            int chunkCount = threadCount * 2; // Use more chunks than threads for better load balancing
            long chunkSize = Math.Max(1024 * 1024, fileLength / chunkCount); // At least 1MB per chunk

            // Prepare tasks and results
            List<(long Start, long Length, byte[] CompressedData)> compressedChunks = [];
            List<Task<(long Start, long Length, byte[] CompressedData)>> compressionTasks = [];

            // Process each chunk in parallel
            for (long position = 0; position < fileLength; position += chunkSize)
            {
                long start = position;
                long length = Math.Min(chunkSize, fileLength - position);

                compressionTasks.Add(Task.Run(async () =>
                {
                    // Create a buffer for the chunk
                    byte[] chunkBuffer = new byte[length];

                    // Read chunk from input stream
                    lock (input)
                    {
                        input.Position = start;
                        input.Read(chunkBuffer, 0, (int)length);
                    }

                    // Compress the chunk
                    using MemoryStream chunkOutput = new();
                    using (DeflateStream deflateStream = new(chunkOutput, compressionLevel, true))
                    {
#if NET48_OR_GREATER || NETSTANDARD2_0
                        await deflateStream.WriteAsync(chunkBuffer, 0, (int)length, cancellationToken);
#else
                        await deflateStream.WriteAsync(chunkBuffer.AsMemory(0, (int)length), cancellationToken);
#endif
                    }

                    return (start, length, chunkOutput.ToArray());
                }, cancellationToken));

                // Periodically wait for some tasks to complete to control memory usage
                if (compressionTasks.Count >= threadCount * 2)
                {
                    Task<(long, long, byte[])> completedTask = await Task.WhenAny(compressionTasks);
                    compressedChunks.Add(await completedTask);
                    compressionTasks.Remove(completedTask);

                    // Report progress
                    if (progress != null)
                    {
                        double progressValue = (double)compressedChunks.Sum(c => c.Length) / fileLength;
                        progress.Report(progressValue);
                    }
                }
            }

            // Wait for remaining tasks
            while (compressionTasks.Count > 0)
            {
                Task<(long, long, byte[])> completedTask = await Task.WhenAny(compressionTasks);
                compressedChunks.Add(await completedTask);
                compressionTasks.Remove(completedTask);

                // Report progress
                if (progress != null)
                {
                    double progressValue = (double)compressedChunks.Sum(c => c.Length) / fileLength;
                    progress.Report(progressValue);
                }
            }

            // Sort chunks by original position
            compressedChunks.Sort((a, b) => a.Start.CompareTo(b.Start));

            // Write compression header - format information about chunks
            using (BinaryWriter writer = new(output, Encoding.UTF8, true))
            {
                writer.Write(compressedChunks.Count);
                foreach ((long Start, long Length, byte[] CompressedData) in compressedChunks)
                {
                    writer.Write(Start);
                    writer.Write(Length);
                    writer.Write(CompressedData.Length);
                }
            }

            // Write compressed data from all chunks
            foreach ((long Start, long Length, byte[] CompressedData) in compressedChunks)
            {
#if NET48_OR_GREATER || NETSTANDARD2_0
                await output.WriteAsync(CompressedData, 0, CompressedData.Length, cancellationToken);
#else
                await output.WriteAsync(CompressedData, cancellationToken);
#endif
            }

            // Restore original position
            input.Position = originalPosition;

            // Final progress update
            progress?.Report(1.0);
        }
    }
}