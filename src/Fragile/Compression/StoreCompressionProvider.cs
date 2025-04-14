using System;
using System.Collections.Generic;
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
        /// Creates a new store compression provider
        /// </summary>
        public StoreCompressionProvider()
            : this(true, Environment.ProcessorCount)
        {
        }

        /// <summary>
        /// Creates a new store compression provider with specified parallel processing options
        /// </summary>
        /// <param name="useParallelProcessing">Whether to use parallel processing</param>
        /// <param name="maxThreads">Maximum number of threads to use for parallel operations</param>
        public StoreCompressionProvider(bool useParallelProcessing, int maxThreads)
            : base(useParallelProcessing, maxThreads)
        {
        }

        /// <summary>
        /// "Compresses" the input stream to the output stream (actually just copies it)
        /// </summary>
        public override async Task<long> CompressAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long initialPosition = output.Position;

            // Use parallel copying if enabled and input stream supports seeking
            if (UseParallelProcessing && input.CanSeek && input.Length > 10 * 1024 * 1024) // Only for files larger than 10MB
            {
                await CopyParallelAsync(input, output, progress, cancellationToken);
            }
            else
            {
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
            }

            // Return the number of bytes written
            return output.Position - initialPosition;
        }

        /// <summary>
        /// "Decompresses" the input stream to the output stream (actually just copies it)
        /// </summary>
        public override async Task<long> DecompressAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            // No compression, so decompression is the same as compression (copying)
            return await CompressAsync(input, output, progress, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Returns the original size with no compression
        /// </summary>
        public override long EstimateCompressedSize(long inputSize)
        {
            return inputSize; // No compression
        }

        /// <summary>
        /// Copies the input stream to the output stream using parallel processing
        /// </summary>
        private async Task CopyParallelAsync(Stream input, Stream output, IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            long fileLength = input.Length;
            long originalPosition = input.Position;

            // Calculate chunks
            int threadCount = Math.Min(MaxThreads, Environment.ProcessorCount);
            int chunkCount = threadCount * 2; // Use more chunks than threads for better load balancing
            long chunkSize = Math.Max(1024 * 1024, fileLength / chunkCount); // At least 1MB per chunk

            // Prepare buffers
            List<(long Start, byte[] Data)> chunks = new();
            List<Task<(long Start, byte[] Data)>> copyTasks = new();

            // Process each chunk in parallel
            for (long position = 0; position < fileLength; position += chunkSize)
            {
                long start = position;
                long length = Math.Min(chunkSize, fileLength - position);

                copyTasks.Add(Task.Run(() =>
                {
                    // Create a buffer for the chunk
                    byte[] chunkBuffer = new byte[length];

                    // Read chunk from input stream
                    lock (input)
                    {
                        input.Position = start;
                        input.Read(chunkBuffer, 0, (int)length);
                    }

                    return (start, chunkBuffer);
                }, cancellationToken));

                // Periodically wait for some tasks to complete to control memory usage
                if (copyTasks.Count >= threadCount * 2)
                {
                    Task<(long, byte[])> completedTask = await Task.WhenAny(copyTasks);
                    chunks.Add(await completedTask);
                    copyTasks.Remove(completedTask);

                    // Report progress
                    if (progress != null)
                    {
                        double progressValue = (double)chunks.Count / chunkCount;
                        progress.Report(progressValue);
                    }
                }
            }

            // Wait for remaining tasks
            while (copyTasks.Count > 0)
            {
                Task<(long, byte[])> completedTask = await Task.WhenAny(copyTasks);
                chunks.Add(await completedTask);
                copyTasks.Remove(completedTask);

                // Report progress
                if (progress != null)
                {
                    double progressValue = (double)chunks.Count / chunkCount;
                    progress.Report(progressValue);
                }
            }

            // Sort chunks by original position
            chunks.Sort((a, b) => a.Start.CompareTo(b.Start));

            // Write all chunks to the output stream
            foreach ((long Start, byte[] Data) in chunks)
            {
                await output.WriteAsync(Data, 0, Data.Length, cancellationToken);
            }

            // Restore original position
            input.Position = originalPosition;

            // Final progress update
            progress?.Report(1.0);
        }
    }
}