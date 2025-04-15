using System.Security.Cryptography;

namespace Fragile.Verification
{
    /// <summary>
    /// Verification provider using cryptographic hash algorithms
    /// </summary>
    internal class HashVerificationProvider : VerificationProvider
    {
        private readonly ChecksumAlgorithm _algorithm;

        /// <summary>
        /// The checksum algorithm used by this provider
        /// </summary>
        public override ChecksumAlgorithm Algorithm => _algorithm;

        /// <summary>
        /// Creates a new hash verification provider with the specified algorithm
        /// </summary>
        /// <param name="algorithm">The hash algorithm to use</param>
        public HashVerificationProvider(ChecksumAlgorithm algorithm)
            : base(false, 1)
        {
            if (algorithm is not ChecksumAlgorithm.MD5 and
                not ChecksumAlgorithm.SHA1 and
                not ChecksumAlgorithm.SHA256 and
                not ChecksumAlgorithm.SHA512)
            {
                throw new ArgumentException($"Unsupported hash algorithm: {algorithm}", nameof(algorithm));
            }

            _algorithm = algorithm;
        }

        /// <summary>
        /// Creates a new hash verification provider with the specified algorithm and parallel processing options
        /// </summary>
        /// <param name="algorithm">The hash algorithm to use</param>
        /// <param name="useParallelProcessing">Whether to use parallel processing</param>
        /// <param name="maxThreads">Maximum number of threads to use</param>
        public HashVerificationProvider(ChecksumAlgorithm algorithm, bool useParallelProcessing, int maxThreads)
            : base(useParallelProcessing, maxThreads)
        {
            if (algorithm is not ChecksumAlgorithm.MD5 and
                not ChecksumAlgorithm.SHA1 and
                not ChecksumAlgorithm.SHA256 and
                not ChecksumAlgorithm.SHA512)
            {
                throw new ArgumentException($"Unsupported hash algorithm: {algorithm}", nameof(algorithm));
            }

            _algorithm = algorithm;
        }

        /// <summary>
        /// Calculates hash for the input stream
        /// </summary>
        public override async Task<byte[]> CalculateChecksumAsync(Stream input, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            // Check if we can use parallel processing
            if (UseParallelProcessing && input.CanSeek && input.Length > 1024 * 1024 * 10) // Only use parallel for streams larger than 10MB
            {
                return await CalculateChecksumParallelAsync(input, progress, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                return await CalculateChecksumSequentialAsync(input, progress, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Calculates hash for the input stream sequentially
        /// </summary>
        private async Task<byte[]> CalculateChecksumSequentialAsync(Stream input, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            using HashAlgorithm hashAlgorithm = CreateHashAlgorithm();

            // If input stream supports seeking, we can report progress
            bool canReportProgress = input.CanSeek;
            long totalBytes = canReportProgress ? input.Length : 0;
            long totalBytesRead = 0;

            byte[] buffer = new byte[81920]; // 80 KB buffer
            int bytesRead;

            while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                // Update hash
                hashAlgorithm.TransformBlock(buffer, 0, bytesRead, null, 0);

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

            // Finalize hash
            hashAlgorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            return hashAlgorithm.Hash;
        }

        /// <summary>
        /// Calculates hash for the input stream using parallel processing
        /// </summary>
        private async Task<byte[]> CalculateChecksumParallelAsync(Stream input, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long streamLength = input.Length;
            int threadCount = DetermineThreadCount(MaxThreads, streamLength);

            // Calculate chunk size based on stream length and thread count
            long chunkSize = streamLength / threadCount;
            if (chunkSize < 1024 * 1024) // Ensure minimum chunk size of 1MB
            {
                chunkSize = 1024 * 1024;
                threadCount = (int)Math.Min(threadCount, streamLength / chunkSize);
                threadCount = Math.Max(1, threadCount); // At least one thread
            }

            // Create chunks
            List<(long Start, long End)> chunks = new();
            for (int i = 0; i < threadCount; i++)
            {
                long start = i * chunkSize;
                long end = (i == threadCount - 1) ? streamLength : (i + 1) * chunkSize;
                chunks.Add((start, end));
            }

            // Limit concurrent tasks using semaphore
            using SemaphoreSlim semaphore = new(threadCount);
            List<Task<byte[]>> tasks = new();
            ParallelProgress progressTracker = new(chunks.Count, progress);

            foreach ((long start, long end) in chunks)
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        // Create a new stream for this chunk
                        using ChunkStream chunkStream = new(input, start, end - start);
                        using HashAlgorithm hashAlgorithm = CreateHashAlgorithm();

                        byte[] buffer = new byte[81920]; // 80 KB buffer
                        int bytesRead;
                        long totalBytesRead = 0;

                        while ((bytesRead = await chunkStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                        {
                            // Update hash
                            hashAlgorithm.TransformBlock(buffer, 0, bytesRead, null, 0);

                            // Update progress for this chunk
                            totalBytesRead += bytesRead;
                            double chunkProgress = (double)totalBytesRead / (end - start);
                            progressTracker.ReportChunkProgress(chunks.IndexOf((start, end)), chunkProgress);

                            // Check for cancellation
                            cancellationToken.ThrowIfCancellationRequested();
                        }

                        // Finalize hash
                        hashAlgorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                        return hashAlgorithm.Hash;
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            // Wait for all tasks to complete
            byte[][] results = await Task.WhenAll(tasks).ConfigureAwait(false);

            // Combine hashes from all chunks
            using HashAlgorithm combineHashAlgorithm = CreateHashAlgorithm();
            foreach (byte[]? hash in results)
            {
                combineHashAlgorithm.TransformBlock(hash, 0, hash.Length, null, 0);
            }
            combineHashAlgorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            // Report 100% progress
            progress?.Report(1.0);

            return combineHashAlgorithm.Hash;
        }

        /// <summary>
        /// Determines the optimal thread count based on max threads and stream size
        /// </summary>
        private static int DetermineThreadCount(int maxThreads, long streamLength)
        {
            int processorCount = Environment.ProcessorCount;
            int threadCount = maxThreads > 0 ? Math.Min(maxThreads, processorCount) : processorCount;

            // Limit thread count based on stream size
            long minBytesPerThread = 1024 * 1024 * 5; // 5MB per thread minimum
            int maxThreadsBySize = (int)Math.Max(1, streamLength / minBytesPerThread);

            return Math.Min(threadCount, maxThreadsBySize);
        }

        /// <summary>
        /// Verifies hash against the input stream
        /// </summary>
        public override async Task<bool> VerifyChecksumAsync(Stream input, byte[] expectedChecksum, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (expectedChecksum == null || expectedChecksum.Length != GetChecksumSize())
            {
                return false; // Invalid checksum length
            }

            byte[] calculatedChecksum = await CalculateChecksumAsync(input, progress, cancellationToken).ConfigureAwait(false);

            // Compare checksums
            if (calculatedChecksum.Length != expectedChecksum.Length)
            {
                return false;
            }

            for (int i = 0; i < calculatedChecksum.Length; i++)
            {
                if (calculatedChecksum[i] != expectedChecksum[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets the size of the hash in bytes
        /// </summary>
        public override int GetChecksumSize()
        {
            return _algorithm switch
            {
                ChecksumAlgorithm.MD5 => 16,    // 128 bits = 16 bytes
                ChecksumAlgorithm.SHA1 => 20,   // 160 bits = 20 bytes
                ChecksumAlgorithm.SHA256 => 32, // 256 bits = 32 bytes
                ChecksumAlgorithm.SHA512 => 64, // 512 bits = 64 bytes
                _ => throw new NotSupportedException($"Unsupported hash algorithm: {_algorithm}")
            };
        }

        /// <summary>
        /// Creates the appropriate hash algorithm instance
        /// </summary>
        private HashAlgorithm CreateHashAlgorithm()
        {
            return _algorithm switch
            {
                ChecksumAlgorithm.MD5 => MD5.Create(),
                ChecksumAlgorithm.SHA1 => SHA1.Create(),
                ChecksumAlgorithm.SHA256 => SHA256.Create(),
                ChecksumAlgorithm.SHA512 => SHA512.Create(),
                _ => throw new NotSupportedException($"Unsupported hash algorithm: {_algorithm}")
            };
        }
    }

    /// <summary>
    /// Helper class for tracking progress of parallel chunk processing
    /// </summary>
    internal class ParallelProgress
    {
        private readonly double[] _chunkProgress;
        private readonly IProgress<double>? _progress;
        private readonly object _lock = new();

        public ParallelProgress(int chunkCount, IProgress<double>? progress)
        {
            _chunkProgress = new double[chunkCount];
            _progress = progress;
        }

        public void ReportChunkProgress(int chunkIndex, double chunkProgress)
        {
            if (_progress == null)
            {
                return;
            }

            lock (_lock)
            {
                _chunkProgress[chunkIndex] = chunkProgress;
                double overallProgress = _chunkProgress.Sum() / _chunkProgress.Length;
                _progress.Report(overallProgress);
            }
        }
    }

    /// <summary>
    /// A stream that represents a chunk of another stream
    /// </summary>
    internal class ChunkStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly long _start;
        private readonly long _length;
        private long _position;

        public ChunkStream(Stream baseStream, long start, long length)
        {
            _baseStream = baseStream;
            _start = start;
            _length = length;
            _position = 0;

            // Position the base stream at the start of the chunk
            _baseStream.Position = _start;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > _length)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _position = value;
                _baseStream.Position = _start + _position;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= _length)
            {
                return 0;
            }

            int bytesToRead = (int)Math.Min(count, _length - _position);
            int bytesRead = _baseStream.Read(buffer, offset, bytesToRead);
            _position += bytesRead;
            return bytesRead;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_position >= _length)
            {
                return 0;
            }

            int bytesToRead = (int)Math.Min(count, _length - _position);
            int bytesRead = await _baseStream.ReadAsync(buffer, offset, bytesToRead, cancellationToken).ConfigureAwait(false);
            _position += bytesRead;
            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPosition = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _length + offset,
                _ => throw new ArgumentException("Invalid seek origin", nameof(origin))
            };

            if (newPosition < 0 || newPosition > _length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            _position = newPosition;
            _baseStream.Position = _start + _position;
            return _position;
        }

        public override void Flush()
        {
            _baseStream.Flush();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}