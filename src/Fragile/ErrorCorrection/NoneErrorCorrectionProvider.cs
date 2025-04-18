namespace Fragile.ErrorCorrection
{
    /// <summary>
    /// Empty provider that does not implement error correction
    /// </summary>
    internal class NoneErrorCorrectionProvider : ErrorCorrectionProvider
    {
        /// <summary>
        /// Creates a new empty error correction provider
        /// </summary>
        public NoneErrorCorrectionProvider() : base(0, false, 1)
        { }

        /// <summary>
        /// Creates a new empty error correction provider with parallel processing options
        /// </summary>
        /// <param name="useParallelProcessing">Whether to use parallel processing</param>
        /// <param name="maxThreads">Maximum number of threads to use</param>
        public NoneErrorCorrectionProvider(bool useParallelProcessing, int maxThreads) : base(0, useParallelProcessing, maxThreads)
        { }

        /// <summary>
        /// Copies data without modification (no error correction)
        /// </summary>
        public override async Task<long> AddErrorCorrectionAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long initialPosition = output.Position;

            // Copy directly without error correction
            await CopyStreamAsync(input, output, progress, cancellationToken);

            return output.Position - initialPosition;
        }

        /// <summary>
        /// Copies data without modification (no error correction)
        /// </summary>
        public override async Task<(long bytesWritten, int bytesRepaired)> CorrectErrorsAsync(Stream input, Stream output, Action<long, int>? reportRepairs = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long initialPosition = output.Position;

            // Copy directly without error correction
            await CopyStreamAsync(input, output, progress, cancellationToken);

            return (output.Position - initialPosition, 0);
        }

        /// <summary>
        /// Returns error correction overhead size (no overhead)
        /// </summary>
        public override long CalculateOverhead(long dataSize)
        {
            return 0; // No error correction, no additional data
        }

        /// <summary>
        /// Stream copying helper method
        /// </summary>
        private async Task CopyStreamAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (UseParallelProcessing && input.CanSeek && input.Length > 10 * 1024 * 1024) // 10MB threshold for parallel copy
            {
                await CopyStreamParallelAsync(input, output, progress, cancellationToken);
            }
            else
            {
                await CopyStreamSequentialAsync(input, output, progress, cancellationToken);
            }
        }

        /// <summary>
        /// Sequential stream copying
        /// </summary>
        private static async Task CopyStreamSequentialAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            byte[] buffer = new byte[81920]; // 80 KB buffer

            // If input stream is seekable, we can report progress
            bool canReportProgress = input.CanSeek;
            long totalBytes = canReportProgress ? input.Length : 0;
            long totalBytesRead = 0;

            int bytesRead;

#if NET48_OR_GREATER || NETSTANDARD2_0
            while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
#else
            while ((bytesRead = await input.ReadAsync(buffer, cancellationToken)) > 0)
#endif
            {
#if NET48_OR_GREATER || NETSTANDARD2_0
                await output.WriteAsync(buffer, 0, bytesRead, cancellationToken);
#else
                await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
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

        /// <summary>
        /// Parallel stream copying
        /// </summary>
        private async Task CopyStreamParallelAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long streamLength = input.Length;
            int threadCount = Math.Min(MaxThreads, Environment.ProcessorCount);
            long chunkSize = streamLength / threadCount;

            // Create at least 1MB chunks, but no more than 100MB chunks
            chunkSize = Math.Max(1024 * 1024, Math.Min(chunkSize, 100 * 1024 * 1024));

            int chunkCount = (int)Math.Ceiling((double)streamLength / chunkSize);

            // Adjust thread count if there are fewer chunks than threads
            threadCount = Math.Min(threadCount, chunkCount);

            // Initialize progress tracking
            double[] chunkProgress = new double[chunkCount];
            object progressLock = new();

            // Use semaphore to limit concurrent tasks
            using SemaphoreSlim semaphore = new(threadCount);
            using SemaphoreSlim inputLock = new(1, 1);
            using SemaphoreSlim outputLock = new(1, 1);
            List<Task> tasks = new(chunkCount);

            // Process each chunk
            for (int i = 0; i < chunkCount; i++)
            {
                int chunkIndex = i;
                long startPosition = chunkIndex * chunkSize;
                long endPosition = Math.Min(startPosition + chunkSize, streamLength);
                long chunkLength = endPosition - startPosition;

                await semaphore.WaitAsync(cancellationToken);

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        byte[] buffer = new byte[chunkLength];

                        // Read chunk
                        await inputLock.WaitAsync(cancellationToken);
                        try
                        {
                            input.Position = startPosition;

#if NET48_OR_GREATER || NETSTANDARD2_0
                            await input.ReadAsync(buffer, 0, (int)chunkLength, cancellationToken);
#else
                            await input.ReadAsync(buffer.AsMemory(0, (int)chunkLength), cancellationToken);
#endif
                        }
                        finally
                        {
                            inputLock.Release();
                        }

                        // Write chunk
                        await outputLock.WaitAsync(cancellationToken);
                        try
                        {
                            output.Position = startPosition;

#if NET48_OR_GREATER || NETSTANDARD2_0
                            await output.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
#else
                            await output.WriteAsync(buffer, cancellationToken);
#endif
                        }
                        finally
                        {
                            outputLock.Release();
                        }

                        // Update progress
                        if (progress != null)
                        {
                            lock (progressLock)
                            {
                                chunkProgress[chunkIndex] = 1.0;
                                double overallProgress = chunkProgress.Sum() / chunkCount;
                                progress.Report(overallProgress);
                            }
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            // Ensure we report 100% completion
            progress?.Report(1.0);
        }
    }
}