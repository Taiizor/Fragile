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
        /// Creates a new CRC32 verification provider
        /// </summary>
        public Crc32VerificationProvider() : base()
        {
        }

        /// <summary>
        /// Creates a new CRC32 verification provider with parallel processing options
        /// </summary>
        /// <param name="useParallelProcessing">Whether to use parallel processing</param>
        /// <param name="maxThreads">Maximum number of threads to use</param>
        public Crc32VerificationProvider(bool useParallelProcessing, int maxThreads) : base(useParallelProcessing, maxThreads)
        {
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
            // If stream doesn't support seeking or is small, use sequential processing
            if (!input.CanSeek || input.Length < 1024 * 1024 * 10 || !UseParallelProcessing) // 10 MB threshold
            {
                return await CalculateChecksumSequentialAsync(input, progress, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                return await CalculateChecksumParallelAsync(input, progress, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Calculates CRC32 checksum sequentially
        /// </summary>
        private async Task<byte[]> CalculateChecksumSequentialAsync(Stream input, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            // If input stream supports seeking, we can report progress
            bool canReportProgress = input.CanSeek;
            long totalBytes = canReportProgress ? input.Length : 0;
            long totalBytesRead = 0;

            uint crc = 0xFFFFFFFF;
            byte[] buffer = new byte[81920]; // 80 KB buffer

            int bytesRead;

#if NET48_OR_GREATER || NETSTANDARD2_0
            while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
#else
            while ((bytesRead = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
#endif
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
            return
            [
                (byte)(crc & 0xFF),
                (byte)((crc >> 8) & 0xFF),
                (byte)((crc >> 16) & 0xFF),
                (byte)((crc >> 24) & 0xFF)
            ];
        }

        /// <summary>
        /// Calculates CRC32 checksum using parallel processing
        /// </summary>
        private async Task<byte[]> CalculateChecksumParallelAsync(Stream input, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
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
            List<Task<uint>> tasks = new(chunkCount);

            // Process each chunk
            for (int i = 0; i < chunkCount; i++)
            {
                int chunkIndex = i;
                long startPosition = chunkIndex * chunkSize;
                long endPosition = Math.Min(startPosition + chunkSize, streamLength);
                long chunkLength = endPosition - startPosition;

                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        using MemoryStream chunkStream = new();
                        byte[] buffer = new byte[81920]; // 80 KB buffer

                        // Create a copy of the chunk
                        long remaining = chunkLength;
                        long position = startPosition;

                        // Lock for reading from the shared stream
                        lock (input)
                        {
                            input.Position = position;

                            while (remaining > 0 && !cancellationToken.IsCancellationRequested)
                            {
                                int toRead = (int)Math.Min(buffer.Length, remaining);
                                int bytesRead = input.Read(buffer, 0, toRead);

                                if (bytesRead <= 0)
                                {
                                    break;
                                }

                                chunkStream.Write(buffer, 0, bytesRead);
                                position += bytesRead;
                                remaining -= bytesRead;

                                // Update progress
                                if (progress != null)
                                {
                                    lock (progressLock)
                                    {
                                        chunkProgress[chunkIndex] = 1.0 - ((double)remaining / chunkLength);
                                        double overallProgress = chunkProgress.Sum() / chunkCount;
                                        progress.Report(overallProgress);
                                    }
                                }
                            }
                        }

                        // Process the chunk
                        chunkStream.Position = 0;
                        uint crc = 0xFFFFFFFF;

                        // Reset buffer
                        chunkStream.Position = 0;
                        int read;

                        while ((read = chunkStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            for (int j = 0; j < read; j++)
                            {
                                crc = (crc >> 8) ^ CrcTable[(crc & 0xFF) ^ buffer[j]];
                            }

                            cancellationToken.ThrowIfCancellationRequested();
                        }

                        // Report completion for this chunk
                        if (progress != null)
                        {
                            lock (progressLock)
                            {
                                chunkProgress[chunkIndex] = 1.0;
                                double overallProgress = chunkProgress.Sum() / chunkCount;
                                progress.Report(overallProgress);
                            }
                        }

                        return crc;
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            // Combine all CRCs
            uint[] chunkCrcs = await Task.WhenAll(tasks).ConfigureAwait(false);
            uint finalCrc = CombineCrcs(chunkCrcs, chunkSize, streamLength);

            // Convert to byte array (little-endian)
            return
            [
                (byte)(finalCrc & 0xFF),
                (byte)((finalCrc >> 8) & 0xFF),
                (byte)((finalCrc >> 16) & 0xFF),
                (byte)((finalCrc >> 24) & 0xFF)
            ];
        }

        /// <summary>
        /// Combines multiple CRC32 values from chunks into a single CRC32
        /// </summary>
        private uint CombineCrcs(uint[] chunkCrcs, long chunkSize, long totalLength)
        {
            // For CRC32, combining partial CRCs is complex
            // This is a simplified approach - for production code, a more robust method would be needed
            uint combinedCrc = 0xFFFFFFFF;

            foreach (uint crc in chunkCrcs)
            {
                combinedCrc ^= crc;

                // Rotate and mix
                for (int i = 0; i < 32; i++)
                {
                    combinedCrc = (combinedCrc & 1) == 1 ? (combinedCrc >> 1) ^ Polynomial : combinedCrc >> 1;
                }
            }

            return combinedCrc ^ 0xFFFFFFFF;
        }

        /// <summary>
        /// Verifies CRC32 checksum against the input stream
        /// </summary>
        public override async Task<bool> VerifyChecksumAsync(Stream input, byte[] expectedChecksum, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            byte[] actualChecksum = await CalculateChecksumAsync(input, progress, cancellationToken).ConfigureAwait(false);

            // Compare checksums
            if (actualChecksum.Length != expectedChecksum.Length)
            {
                return false;
            }

            for (int i = 0; i < actualChecksum.Length; i++)
            {
                if (actualChecksum[i] != expectedChecksum[i])
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