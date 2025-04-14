using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

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
            : this(level, true, Environment.ProcessorCount)
        {
        }

        /// <summary>
        /// Creates a new Deflate compression provider with the specified level and parallel processing options
        /// </summary>
        /// <param name="level">Compression level</param>
        /// <param name="useParallelProcessing">Whether to use parallel processing</param>
        /// <param name="maxThreads">Maximum number of threads to use for parallel operations</param>
        public DeflateCompressionProvider(CompressionLevel level, bool useParallelProcessing, int maxThreads)
            : base(useParallelProcessing, maxThreads)
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

            // Use parallel compression if enabled and input stream supports seeking
            if (UseParallelProcessing && input.CanSeek && input.Length > 1024 * 1024) // Only for files larger than 1MB
            {
                await CompressParallelAsync(input, output, compressionLevel, progress, cancellationToken);
            }
            else
            {
                using (DeflateStream deflateStream = new(output, compressionLevel, true))
                {
                    // If input stream supports seeking, we can report progress
                    bool canReportProgress = input.CanSeek;
                    long totalBytes = canReportProgress ? input.Length : 0;
                    long totalBytesRead = 0;
                    byte[] buffer = new byte[81920]; // 80 KB buffer

                    int bytesRead;
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
            List<(long Start, long Length, byte[] CompressedData)> compressedChunks = new();
            List<Task<(long Start, long Length, byte[] CompressedData)>> compressionTasks = new();
            
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
                        await deflateStream.WriteAsync(chunkBuffer, 0, (int)length, cancellationToken);
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
            using (BinaryWriter writer = new(output, System.Text.Encoding.UTF8, true))
            {
                writer.Write(compressedChunks.Count);
                foreach (var chunk in compressedChunks)
                {
                    writer.Write(chunk.Start);
                    writer.Write(chunk.Length);
                    writer.Write(chunk.CompressedData.Length);
                }
            }
            
            // Write compressed data from all chunks
            foreach (var chunk in compressedChunks)
            {
                await output.WriteAsync(chunk.CompressedData, 0, chunk.CompressedData.Length, cancellationToken);
            }
            
            // Restore original position
            input.Position = originalPosition;
            
            // Final progress update
            progress?.Report(1.0);
        }
    }
}