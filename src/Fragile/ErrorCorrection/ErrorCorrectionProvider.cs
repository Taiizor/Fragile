using Fragile.Models;

namespace Fragile.ErrorCorrection
{
    /// <summary>
    /// Abstract base class for error correction provider algorithm
    /// </summary>
    public abstract class ErrorCorrectionProvider
    {
        /// <summary>
        /// Error correction level (as percentage)
        /// </summary>
        public int CorrectionLevel { get; }

        /// <summary>
        /// Whether to use parallel processing for error correction operations
        /// </summary>
        public bool UseParallelProcessing { get; }

        /// <summary>
        /// Maximum number of threads to use for parallel operations
        /// </summary>
        public int MaxThreads { get; }

        /// <summary>
        /// Creates a new error correction provider
        /// </summary>
        /// <param name="correctionLevel">Error correction level (as percentage)</param>
        /// <param name="useParallelProcessing">Whether to use parallel processing</param>
        /// <param name="maxThreads">Maximum number of threads to use</param>
        protected ErrorCorrectionProvider(int correctionLevel, bool useParallelProcessing = false, int maxThreads = 1)
        {
            if (correctionLevel is < 0 or > 50)
            {
                throw new ArgumentOutOfRangeException(nameof(correctionLevel), "Error correction level must be between 0-50");
            }

            CorrectionLevel = correctionLevel;
            UseParallelProcessing = useParallelProcessing;
            MaxThreads = maxThreads > 0 ? maxThreads : Environment.ProcessorCount;
        }

        /// <summary>
        /// Creates an error correction provider with the specified level
        /// </summary>
        /// <param name="correctionLevel">Error correction level (between 0-50)</param>
        /// <returns>Error correction provider</returns>
        public static ErrorCorrectionProvider Create(int correctionLevel)
        {
            return Create(new FragileOptions { ErrorCorrectionLevel = correctionLevel });
        }

        /// <summary>
        /// Creates an error correction provider with the specified options
        /// </summary>
        /// <param name="options">Options containing error correction settings</param>
        /// <returns>Error correction provider</returns>
        public static ErrorCorrectionProvider Create(FragileOptions options)
        {
#if NET48_OR_GREATER || NETSTANDARD2_0_OR_GREATER
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
#else
            ArgumentNullException.ThrowIfNull(options);
#endif

            if (options.ErrorCorrectionLevel <= 0 || !options.EnableErrorCorrection)
            {
                return new NoneErrorCorrectionProvider(options.UseParallelProcessing, options.MaxThreads);
            }

            return new ReedSolomonErrorCorrectionProvider(
                options.ErrorCorrectionLevel,
                options.UseParallelProcessing,
                options.MaxThreads);
        }

        /// <summary>
        /// Adds error correction codes to data
        /// </summary>
        /// <param name="input">Data stream to apply error correction</param>
        /// <param name="output">Data stream with error correction codes added</param>
        /// <param name="progress">Progress notification</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Total number of bytes written</returns>
        public abstract Task<long> AddErrorCorrectionAsync(Stream input, Stream output,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Corrects data and removes error correction codes
        /// </summary>
        /// <param name="input">Error correction coded data stream</param>
        /// <param name="output">Corrected data stream</param>
        /// <param name="reportRepairs">Repair notification callback function</param>
        /// <param name="progress">Progress notification</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Value pair containing (bytes written, bytes repaired) - total number of bytes written and number of bytes corrected</returns>
        public abstract Task<(long bytesWritten, int bytesRepaired)> CorrectErrorsAsync(Stream input, Stream output,
            Action<long, int>? reportRepairs = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates the additional data size required for error correction
        /// </summary>
        /// <param name="dataSize">Original data size</param>
        /// <returns>Additional size required for error correction data</returns>
        public abstract long CalculateOverhead(long dataSize);
    }

    /// <summary>
    /// Empty provider that does not implement error correction
    /// </summary>
    internal class NoneErrorCorrectionProvider : ErrorCorrectionProvider
    {
        /// <summary>
        /// Creates a new empty error correction provider
        /// </summary>
        public NoneErrorCorrectionProvider()
            : base(0, false, 1)
        { }

        /// <summary>
        /// Creates a new empty error correction provider with parallel processing options
        /// </summary>
        /// <param name="useParallelProcessing">Whether to use parallel processing</param>
        /// <param name="maxThreads">Maximum number of threads to use</param>
        public NoneErrorCorrectionProvider(bool useParallelProcessing, int maxThreads)
            : base(0, useParallelProcessing, maxThreads)
        { }

        /// <summary>
        /// Copies data without modification (no error correction)
        /// </summary>
        public override async Task<long> AddErrorCorrectionAsync(Stream input, Stream output,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long initialPosition = output.Position;

            // Copy directly without error correction
            await CopyStreamAsync(input, output, progress, cancellationToken);

            return output.Position - initialPosition;
        }

        /// <summary>
        /// Copies data without modification (no error correction)
        /// </summary>
        public override async Task<(long bytesWritten, int bytesRepaired)> CorrectErrorsAsync(Stream input, Stream output,
            Action<long, int>? reportRepairs = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
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
        private async Task CopyStreamAsync(Stream input, Stream output,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
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
        private static async Task CopyStreamSequentialAsync(Stream input, Stream output,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
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
        private async Task CopyStreamParallelAsync(Stream input, Stream output,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
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

    /// <summary>
    /// Error correction provider using Reed-Solomon algorithm
    /// </summary>
    internal class ReedSolomonErrorCorrectionProvider : ErrorCorrectionProvider
    {
        // Block size limited by Reed-Solomon Galois field size
        private const int MaxBlockSize = 255;

        // Maximum error percentage that Reed-Solomon algorithm can correct
        private const double MaxCorrectableErrorPercentage = 0.5;

        // Default error correction sizes
        private const int DefaultECSize = 32;    // Standard RS(255,223)
        private const int DefaultDataSize = 223; // Standard RS(255,223)

        /// <summary>
        /// Creates a new Reed-Solomon error correction provider
        /// </summary>
        /// <param name="correctionLevel">Error correction level (between 1-50)</param>
        public ReedSolomonErrorCorrectionProvider(int correctionLevel)
            : base(correctionLevel, false, 1)
        { }

        /// <summary>
        /// Creates a new Reed-Solomon error correction provider with parallel processing options
        /// </summary>
        /// <param name="correctionLevel">Error correction level (between 1-50)</param>
        /// <param name="useParallelProcessing">Whether to use parallel processing</param>
        /// <param name="maxThreads">Maximum number of threads to use</param>
        public ReedSolomonErrorCorrectionProvider(int correctionLevel, bool useParallelProcessing, int maxThreads)
            : base(correctionLevel, useParallelProcessing, maxThreads)
        { }

        /// <summary>
        /// Adds Reed-Solomon error correction codes to data
        /// </summary>
        public override async Task<long> AddErrorCorrectionAsync(Stream input, Stream output,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            // If input stream is empty, return without writing anything to output stream
            if (input.Length == 0)
            {
                return 0;
            }

            // Calculate optimal data and error correction sizes
            (int dataSize, int ecSize) = CalculateOptimalBlockSizes();

            // Create RS algorithm
            ReedSolomonAlgorithm rs = new(dataSize, ecSize);

            // Write header
            await WriteHeaderAsync(output, dataSize, ecSize, cancellationToken);

            // For large files, use parallel processing if enabled
            if (UseParallelProcessing && input.CanSeek && input.Length > 10 * 1024 * 1024) // 10MB threshold
            {
                return await AddErrorCorrectionParallelAsync(input, output, rs, dataSize, ecSize, progress, cancellationToken);
            }
            else
            {
                return await AddErrorCorrectionSequentialAsync(input, output, rs, dataSize, ecSize, progress, cancellationToken);
            }
        }

        /// <summary>
        /// Sequentially adds error correction to data
        /// </summary>
        private async Task<long> AddErrorCorrectionSequentialAsync(Stream input, Stream output,
            ReedSolomonAlgorithm rs, int dataSize, int ecSize,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            // Process data in blocks
            byte[] buffer = new byte[dataSize];
            long totalBytesRead = 0;
            long totalBytesWritten = 0;
            long inputLength = input.Length;

            while (true)
            {
#if NET48_OR_GREATER || NETSTANDARD2_0
                int bytesRead = await input.ReadAsync(buffer, 0, dataSize, cancellationToken);
#else
                int bytesRead = await input.ReadAsync(buffer.AsMemory(0, dataSize), cancellationToken);
#endif

                if (bytesRead == 0)
                {
                    break;
                }

                // If block is not completely filled, zero out the remaining portion
                if (bytesRead < dataSize)
                {
                    Array.Clear(buffer, bytesRead, dataSize - bytesRead);
                }

                try
                {
                    // Add error correction codes
                    byte[] encoded = rs.Encode(buffer);

                    // Write encoded data
#if NET48_OR_GREATER || NETSTANDARD2_0
                    await output.WriteAsync(encoded, 0, encoded.Length, cancellationToken);
#else
                    await output.WriteAsync(encoded, cancellationToken);
#endif

                    totalBytesRead += bytesRead;
                    totalBytesWritten += encoded.Length;

                    // Progress notification
                    if (progress != null && inputLength > 0)
                    {
                        progress.Report((double)totalBytesRead / inputLength);
                    }
                }
                catch (Exception ex)
                {
                    throw new IOException($"Error occurred during Reed-Solomon encoding: {ex.Message}", ex);
                }

                // If this is the last block and it's not completely filled, end the process
                if (bytesRead < dataSize)
                {
                    break;
                }
            }

            return totalBytesWritten;
        }

        /// <summary>
        /// Adds error correction to data using parallel processing
        /// </summary>
        private async Task<long> AddErrorCorrectionParallelAsync(Stream input, Stream output,
            ReedSolomonAlgorithm rs, int dataSize, int ecSize,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long fileLength = input.Length;

            // Calculate number of blocks
            int totalBlocks = (int)Math.Ceiling((double)fileLength / dataSize);

            // Determine thread count
            int threadCount = Math.Min(MaxThreads, Environment.ProcessorCount);
            threadCount = Math.Min(threadCount, totalBlocks);

            // Progress tracking
            double[] blockProgress = new double[totalBlocks];
            object progressLock = new();

            // Output tracking
            long totalBytesWritten = 8; // header size

            // Use semaphore to limit concurrent operations
            using SemaphoreSlim semaphore = new(threadCount);
            using SemaphoreSlim inputLock = new(1, 1);
            using SemaphoreSlim outputLock = new(1, 1);

            // Process blocks in batches to minimize memory usage
            int batchSize = Math.Min(threadCount * 4, totalBlocks);
            int completedBlocks = 0;

            while (completedBlocks < totalBlocks)
            {
                int currentBatchSize = Math.Min(batchSize, totalBlocks - completedBlocks);
                List<Task<byte[]>> batchTasks = new(currentBatchSize);

                // Process batch
                for (int i = 0; i < currentBatchSize; i++)
                {
                    int blockIndex = completedBlocks + i;
                    long startPosition = blockIndex * dataSize;
                    long bytesToRead = Math.Min(dataSize, fileLength - startPosition);

                    await semaphore.WaitAsync(cancellationToken);

                    batchTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            // Create buffer for this block
                            byte[] blockBuffer = new byte[dataSize];

                            // Read block data
                            await inputLock.WaitAsync(cancellationToken);
                            try
                            {
                                input.Position = startPosition;

#if NET48_OR_GREATER || NETSTANDARD2_0
                                await input.ReadAsync(blockBuffer, 0, (int)bytesToRead, cancellationToken);
#else
                                await input.ReadAsync(blockBuffer.AsMemory(0, (int)bytesToRead), cancellationToken);
#endif
                            }
                            finally
                            {
                                inputLock.Release();
                            }

                            // If block is not completely filled, zero out the remainder
                            if (bytesToRead < dataSize)
                            {
                                Array.Clear(blockBuffer, (int)bytesToRead, dataSize - (int)bytesToRead);
                            }

                            // Encode with error correction
                            byte[] encoded = rs.Encode(blockBuffer);

                            // Update progress
                            lock (progressLock)
                            {
                                blockProgress[blockIndex] = 1.0;
                                if (progress != null)
                                {
                                    double overallProgress = blockProgress.Sum() / totalBlocks;
                                    progress.Report(overallProgress);
                                }
                            }

                            return encoded;
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, cancellationToken));
                }

                // Process results for this batch
                byte[][] batchResults = await Task.WhenAll(batchTasks);

                // Write batch results to output
                await outputLock.WaitAsync(cancellationToken);
                try
                {
                    foreach (byte[] encoded in batchResults)
                    {
#if NET48_OR_GREATER || NETSTANDARD2_0
                        await output.WriteAsync(encoded, 0, encoded.Length, cancellationToken);
#else
                        await output.WriteAsync(encoded, cancellationToken);
#endif
                        totalBytesWritten += encoded.Length;
                    }
                }
                finally
                {
                    outputLock.Release();
                }

                completedBlocks += currentBatchSize;
            }

            // Final progress update
            progress?.Report(1.0);

            return totalBytesWritten;
        }

        /// <summary>
        /// Corrects data using Reed-Solomon error correction codes
        /// </summary>
        public override async Task<(long bytesWritten, int bytesRepaired)> CorrectErrorsAsync(Stream input, Stream output,
            Action<long, int>? reportRepairs = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            // If input stream is empty, return without writing anything to output stream
            if (input.Length == 0)
            {
                return (0, 0);
            }

            // Read header
            (int dataSize, int ecSize) = await ReadHeaderAsync(input, cancellationToken);

            // Create RS algorithm
            ReedSolomonAlgorithm rs = new(dataSize, ecSize);

            // For large files, use parallel processing if enabled
            if (UseParallelProcessing && input.CanSeek && input.Length > 10 * 1024 * 1024) // 10MB threshold
            {
                return await CorrectErrorsParallelAsync(input, output, rs, dataSize, ecSize, reportRepairs, progress, cancellationToken);
            }
            else
            {
                return await CorrectErrorsSequentialAsync(input, output, rs, dataSize, ecSize, reportRepairs, progress, cancellationToken);
            }
        }

        /// <summary>
        /// Sequentially corrects errors in data
        /// </summary>
        private async Task<(long bytesWritten, int bytesRepaired)> CorrectErrorsSequentialAsync(Stream input, Stream output,
            ReedSolomonAlgorithm rs, int dataSize, int ecSize,
            Action<long, int>? reportRepairs = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            int blockSize = dataSize + ecSize;
            byte[] buffer = new byte[blockSize];
            long totalBytesWritten = 0;
            int totalRepairs = 0;
            long inputLength = input.Length - 8; // Subtract header size
            long totalBytesRead = 0;

            while (true)
            {
#if NET48_OR_GREATER || NETSTANDARD2_0
                int bytesRead = await input.ReadAsync(buffer, 0, blockSize, cancellationToken);
#else
                int bytesRead = await input.ReadAsync(buffer.AsMemory(0, blockSize), cancellationToken);
#endif

                if (bytesRead == 0)
                {
                    break;
                }

                if (bytesRead < blockSize)
                {
                    Array.Clear(buffer, bytesRead, blockSize - bytesRead);
                }

                try
                {
                    // Try to decode and correct errors
                    (byte[] decoded, int errors) = rs.Decode(buffer);

                    // Write corrected data
#if NET48_OR_GREATER || NETSTANDARD2_0
                    await output.WriteAsync(decoded, 0, decoded.Length, cancellationToken);
#else
                    await output.WriteAsync(decoded, cancellationToken);
#endif

                    // Update statistics
                    totalBytesWritten += decoded.Length;
                    if (errors > 0)
                    {
                        totalRepairs += errors;
                        reportRepairs?.Invoke(totalBytesWritten, errors);
                    }

                    // Progress notification
                    totalBytesRead += blockSize;
                    if (progress != null && inputLength > 0)
                    {
                        progress.Report((double)totalBytesRead / inputLength);
                    }
                }
                catch (Exception ex)
                {
                    throw new IOException($"Error correction failed: {ex.Message}", ex);
                }

                // If this is the last block and it's not completely filled, end the process
                if (bytesRead < blockSize)
                {
                    break;
                }
            }

            return (totalBytesWritten, totalRepairs);
        }

        /// <summary>
        /// Corrects errors in data using parallel processing
        /// </summary>
        private async Task<(long bytesWritten, int bytesRepaired)> CorrectErrorsParallelAsync(Stream input, Stream output,
            ReedSolomonAlgorithm rs, int dataSize, int ecSize,
            Action<long, int>? reportRepairs = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            int blockSize = dataSize + ecSize;
            long fileLength = input.Length - 8; // Subtract header size

            // Calculate number of blocks
            int totalBlocks = (int)Math.Ceiling((double)fileLength / blockSize);

            // Determine thread count
            int threadCount = Math.Min(MaxThreads, Environment.ProcessorCount);
            threadCount = Math.Min(threadCount, totalBlocks);

            // Progress tracking
            double[] blockProgress = new double[totalBlocks];
            object progressLock = new();

            // Repair statistics
            int totalRepairs = 0;
            long totalBytesWritten = 0;
            object statsLock = new();

            // Use semaphore to limit concurrent operations
            using SemaphoreSlim semaphore = new(threadCount);
            using SemaphoreSlim inputLock = new(1, 1);
            using SemaphoreSlim outputLock = new(1, 1);

            // Process blocks in batches to minimize memory usage
            int batchSize = Math.Min(threadCount * 4, totalBlocks);
            int completedBlocks = 0;

            while (completedBlocks < totalBlocks)
            {
                int currentBatchSize = Math.Min(batchSize, totalBlocks - completedBlocks);
                List<Task<(byte[] decoded, int errors, int blockIndex)>> batchTasks = new(currentBatchSize);

                // Process batch
                for (int i = 0; i < currentBatchSize; i++)
                {
                    int blockIndex = completedBlocks + i;
                    long startPosition = 8 + ((long)blockIndex * blockSize); // Skip header

                    await semaphore.WaitAsync(cancellationToken);

                    batchTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            // Read block
                            byte[] blockBuffer = new byte[blockSize];
                            int bytesRead;

                            await inputLock.WaitAsync(cancellationToken);
                            try
                            {
                                input.Position = startPosition;

#if NET48_OR_GREATER || NETSTANDARD2_0
                                bytesRead = await input.ReadAsync(blockBuffer, 0, blockSize, cancellationToken);
#else
                                bytesRead = await input.ReadAsync(blockBuffer.AsMemory(0, blockSize), cancellationToken);
#endif

                                if (bytesRead < blockSize)
                                {
                                    Array.Clear(blockBuffer, bytesRead, blockSize - bytesRead);
                                }
                            }
                            finally
                            {
                                inputLock.Release();
                            }

                            // Decode and correct errors
                            (byte[] decoded, int errors) = rs.Decode(blockBuffer);

                            // Update progress
                            lock (progressLock)
                            {
                                blockProgress[blockIndex] = 1.0;
                                if (progress != null)
                                {
                                    double overallProgress = blockProgress.Sum() / totalBlocks;
                                    progress.Report(overallProgress);
                                }
                            }

                            return (decoded, errors, blockIndex);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, cancellationToken));
                }

                // Process results for this batch
                (byte[] decoded, int errors, int blockIndex)[] batchResults = await Task.WhenAll(batchTasks);

                // Sort results by block index to ensure correct output order
                Array.Sort(batchResults, (a, b) => a.blockIndex.CompareTo(b.blockIndex));

                // Write batch results to output and track repairs
                await outputLock.WaitAsync(cancellationToken);
                try
                {
                    foreach ((byte[] decoded, int errors, int _) in batchResults)
                    {
                        long currentPosition = output.Position;

#if NET48_OR_GREATER || NETSTANDARD2_0
                        await output.WriteAsync(decoded, 0, decoded.Length, cancellationToken);
#else
                        await output.WriteAsync(decoded, cancellationToken);
#endif

                        long bytesWritten = decoded.Length;

                        lock (statsLock)
                        {
                            totalBytesWritten += bytesWritten;

                            if (errors > 0)
                            {
                                totalRepairs += errors;
                                reportRepairs?.Invoke(currentPosition, errors);
                            }
                        }
                    }
                }
                finally
                {
                    outputLock.Release();
                }

                completedBlocks += currentBatchSize;
            }

            // Final progress update
            progress?.Report(1.0);

            return (totalBytesWritten, totalRepairs);
        }

        /// <summary>
        /// Calculates the additional data size required for error correction
        /// </summary>
        public override long CalculateOverhead(long dataSize)
        {
            if (dataSize <= 0)
            {
                return 0;
            }

            // Header size
            int headerSize = 8;

            // Calculate optimal data and error correction sizes
            (int optimalDataSize, int optimalECSize) = CalculateOptimalBlockSizes();

            // Total number of blocks (round up)
            long totalDataBlocks = (dataSize + optimalDataSize - 1) / optimalDataSize;

            // Total additional data size
            return headerSize + (totalDataBlocks * optimalECSize);
        }

        /// <summary>
        /// Calculates optimal data and error correction sizes
        /// </summary>
        private (int dataSize, int ecSize) CalculateOptimalBlockSizes()
        {
            // Standard Reed-Solomon codes typically have a total length of 255 bytes
            // For example RS(255,223) -> 223 data + 32 error correction

            // Adjust sizes according to error correction level
            int ecRatio = CorrectionLevel;
            int dataRatio = 100 - ecRatio;

            // Safety limits
            if (dataRatio < 50)
            {
                dataRatio = 50; // Minimum 50% data
            }

            if (dataRatio > 90)
            {
                dataRatio = 90; // Maximum 90% data
            }

            // Maximum block size for Reed-Solomon
            int maxTotalSize = ReedSolomonAlgorithm.GetMaxBlockSize();

            // Calculate data and EC sizes from ratios, within 254 byte limit
            int dataSize = maxTotalSize * dataRatio / 100;
            int ecSize = maxTotalSize - dataSize;

            // Safety check
            if (dataSize + ecSize > maxTotalSize)
            {
                dataSize = maxTotalSize - ecSize;
            }

            // Minimum size check
            if (dataSize < 1)
            {
                dataSize = 1;
            }

            if (ecSize < 1)
            {
                ecSize = 1;
            }

            return (dataSize, ecSize);
        }

        /// <summary>
        /// Writes error correction header information
        /// </summary>
        private static async Task WriteHeaderAsync(Stream output, int dataSize, int ecSize, CancellationToken cancellationToken)
        {
            byte[] header =
            [
                // Magic bytes (RS)
                (byte)'R',
                (byte)'S',
                // Data size (4 bytes, little-endian)
                (byte)(dataSize & 0xFF),
                (byte)((dataSize >> 8) & 0xFF),
                (byte)((dataSize >> 16) & 0xFF),
                (byte)((dataSize >> 24) & 0xFF),
                // Error correction size (2 bytes, little-endian)
                (byte)(ecSize & 0xFF),
                (byte)((ecSize >> 8) & 0xFF),
            ];

#if NET48_OR_GREATER || NETSTANDARD2_0
            await output.WriteAsync(header, 0, header.Length, cancellationToken);
#else
            await output.WriteAsync(header, cancellationToken);
#endif
        }

        /// <summary>
        /// Reads error correction header information
        /// </summary>
        private static async Task<(int dataSize, int ecSize)> ReadHeaderAsync(Stream input, CancellationToken cancellationToken)
        {
            byte[] header = new byte[8];

#if NET48_OR_GREATER || NETSTANDARD2_0
            if (await input.ReadAsync(header, 0, header.Length, cancellationToken) != header.Length)
#else
            if (await input.ReadAsync(header, cancellationToken) != header.Length)
#endif
            {
                throw new EndOfStreamException("Unexpected end of file - header could not be read");
            }

            // Check magic bytes
            if (header[0] != 'R' || header[1] != 'S')
            {
                throw new InvalidDataException("Invalid error correction header");
            }

            // Read data size
            int dataSize = header[2] | (header[3] << 8) | (header[4] << 16) | (header[5] << 24);

            // Read error correction size
            int ecSize = header[6] | (header[7] << 8);

            return (dataSize, ecSize);
        }
    }
}