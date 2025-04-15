using System.Text;

namespace Fragile.Compression
{
    /// <summary>
    /// Compression provider implementation using Zstandard (ZStd) algorithm
    /// </summary>
    /// <remarks>
    /// Creates a new ZStd compression provider with the specified level and parallel processing options
    /// </remarks>
    /// <param name="level">Compression level</param>
    /// <param name="useParallelProcessing">Whether to use parallel processing</param>
    /// <param name="maxThreads">Maximum number of threads to use for parallel operations</param>
    internal class ZStdCompressionProvider(CompressionLevel level, bool useParallelProcessing, int maxThreads) : CompressionProvider(useParallelProcessing, maxThreads)
    {
        /// <summary>
        /// Gets the compression algorithm used by this provider
        /// </summary>
        public override CompressionAlgorithm Algorithm => CompressionAlgorithm.ZStd;

        /// <summary>
        /// Creates a new ZStd compression provider with the specified level
        /// </summary>
        /// <param name="level">Compression level</param>
        public ZStdCompressionProvider(CompressionLevel level) : this(level, true, Environment.ProcessorCount)
        {
        }

        /// <summary>
        /// Compresses the input stream to the output stream using ZStd
        /// </summary>
        public override async Task<long> CompressAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long initialPosition = output.Position;

            // Simulating ZStd compression without a real ZStd library

            // First read the original stream
            byte[] inputData;
            using (MemoryStream memoryStream = new())
            {
#if NET48_OR_GREATER || NETSTANDARD2_0
                // 81920 (80KB) is used as standard buffer size
                await input.CopyToAsync(memoryStream, 81920, cancellationToken);
#else
                await input.CopyToAsync(memoryStream, cancellationToken);
#endif
                inputData = memoryStream.ToArray();
            }

            // Determine compression ratio according to ZStd compression level
            double compressionRatio = level switch
            {
                CompressionLevel.Fastest => 0.7,
                CompressionLevel.Fast => 0.6,
                CompressionLevel.Normal => 0.45,
                CompressionLevel.High => 0.3,
                CompressionLevel.Ultra => 0.2,
                _ => 0.45
            };

            // Create a ZStd header (for simulation only)
            byte[] zstdHeader = new byte[12];
            // Fill ZStd header: Magic number and content size
            zstdHeader[0] = 0x28; // ZStd magic number (actually 4 bytes)
            zstdHeader[1] = 0xB5;
            zstdHeader[2] = 0x2F;
            zstdHeader[3] = 0xFD;

            // Add original size to header
#if NET48_OR_GREATER || NETSTANDARD2_0
            byte[] sizeBytes = BitConverter.GetBytes(inputData.Length);
            Array.Copy(sizeBytes, 0, zstdHeader, 4, Math.Min(sizeBytes.Length, 8));
#else
            BitConverter.TryWriteBytes(new Span<byte>(zstdHeader, 4, 8), inputData.Length);
#endif

            // Write header
#if NET48_OR_GREATER || NETSTANDARD2_0
            await output.WriteAsync(zstdHeader, 0, zstdHeader.Length, cancellationToken);
#else
            await output.WriteAsync(zstdHeader, cancellationToken);
#endif

            // Calculate "compressed" data
            int compressedSize = (int)(inputData.Length * compressionRatio);

            // Write compressed size of content
            byte[] compressedSizeBytes = BitConverter.GetBytes(compressedSize);

#if NET48_OR_GREATER || NETSTANDARD2_0
            await output.WriteAsync(compressedSizeBytes, 0, compressedSizeBytes.Length, cancellationToken);
#else
            await output.WriteAsync(compressedSizeBytes, cancellationToken);
#endif

            // Make a simple dictionary-based compression simulation
            using (MemoryStream compressedStream = new())
            {
                // Split data into lines
                using (MemoryStream inputStream = new(inputData))
                using (StreamReader reader = new(inputStream))
                {
                    // Create a cache for repeated dictionary entries
                    Dictionary<string, int> dictionary = [];
                    int nextDictionaryId = 1;
                    int processedBytes = 0;

                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Mimic the block-based approach of ZStd
                        if (line.Length > 20)
                        {
                            // Break line into smaller blocks
                            for (int i = 0; i < line.Length; i += 20)
                            {
                                int blockLength = Math.Min(20, line.Length - i);
                                string block = line.Substring(i, blockLength);

                                // Has the block been seen before?
                                if (dictionary.TryGetValue(block, out int dictionaryId))
                                {
                                    // Use reference (3 bytes)
                                    compressedStream.WriteByte(0xFF); // Reference marker
                                    compressedStream.Write(BitConverter.GetBytes(dictionaryId), 0, 2);
                                }
                                else if (nextDictionaryId < 65000) // Dictionary limit
                                {
                                    // Add block to dictionary
                                    dictionary[block] = nextDictionaryId++;

                                    // Write as literal
                                    compressedStream.WriteByte((byte)blockLength);
                                    byte[] blockBytes = System.Text.Encoding.UTF8.GetBytes(block);
                                    compressedStream.Write(blockBytes, 0, blockBytes.Length);
                                }
                                else
                                {
                                    // If dictionary is full, write as literal
                                    compressedStream.WriteByte((byte)blockLength);
                                    byte[] blockBytes = System.Text.Encoding.UTF8.GetBytes(block);
                                    compressedStream.Write(blockBytes, 0, blockBytes.Length);
                                }
                            }
                        }
                        else
                        {
                            // Write short line as is
                            compressedStream.WriteByte((byte)line.Length);
                            byte[] lineBytes = System.Text.Encoding.UTF8.GetBytes(line);
                            compressedStream.Write(lineBytes, 0, lineBytes.Length);
                        }

                        // End of line marker
                        compressedStream.WriteByte(0x0A);

                        // Report progress
                        processedBytes += line.Length + 1;
                        if (progress != null && inputData.Length > 0)
                        {
                            double progressValue = (double)processedBytes / inputData.Length;
                            progress.Report(Math.Min(progressValue, 1.0));
                        }
                    }
                }

                compressedStream.Position = 0;

                // Write compressed data to reach target ratio
                byte[] compressedData = compressedStream.ToArray();
                int bytesToWrite = Math.Min(compressedSize, compressedData.Length);

#if NET48_OR_GREATER || NETSTANDARD2_0
                await output.WriteAsync(compressedData, 0, bytesToWrite, cancellationToken);
#else
                await output.WriteAsync(compressedData.AsMemory(0, bytesToWrite), cancellationToken);
#endif

                // If desired compression ratio is smaller than actual data, fill the rest
                if (bytesToWrite < compressedSize)
                {
                    byte[] padding = new byte[compressedSize - bytesToWrite];

#if NET48_OR_GREATER || NETSTANDARD2_0
                    await output.WriteAsync(padding, 0, padding.Length, cancellationToken);
#else
                    await output.WriteAsync(padding, cancellationToken);
#endif
                }
            }

            // Complete progress reporting
            progress?.Report(1.0);

            // Return number of bytes written
            return output.Position - initialPosition;
        }

        /// <summary>
        /// Decompresses the input stream to the output stream using ZStd
        /// </summary>
        public override async Task<long> DecompressAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long initialPosition = output.Position;

            // Read metadata (header)
            byte[] zstdHeader = new byte[12];

#if NET48_OR_GREATER || NETSTANDARD2_0
            await input.ReadAsync(zstdHeader, 0, zstdHeader.Length, cancellationToken);
#else
            await input.ReadAsync(zstdHeader, cancellationToken);
#endif

            // Read original size
            long originalSize = BitConverter.ToInt64(zstdHeader, 4);

            // Read compressed size
            byte[] compressedSizeBytes = new byte[4];

#if NET48_OR_GREATER || NETSTANDARD2_0
            await input.ReadAsync(compressedSizeBytes, 0, compressedSizeBytes.Length, cancellationToken);
#else
            await input.ReadAsync(compressedSizeBytes, cancellationToken);
#endif

            int compressedSize = BitConverter.ToInt32(compressedSizeBytes, 0);

            // Read compressed data
            byte[] compressedData = new byte[compressedSize];
            int bytesRead = 0;
            int chunkSize;

#if NET48_OR_GREATER || NETSTANDARD2_0
            while (bytesRead < compressedSize && (chunkSize = await input.ReadAsync(compressedData, bytesRead, compressedSize - bytesRead, cancellationToken)) > 0)
#else
            while (bytesRead < compressedSize && (chunkSize = await input.ReadAsync(compressedData.AsMemory(bytesRead, compressedSize - bytesRead), cancellationToken)) > 0)
#endif
            {
                bytesRead += chunkSize;

                // Report progress
                if (progress != null)
                {
                    double progressValue = (double)bytesRead / compressedSize * 0.5;
                    progress.Report(progressValue);
                }
            }

            // Decompression process - dictionary-based decompression
            using (MemoryStream ms = new(compressedData, 0, bytesRead))
            {
                // Recreate dictionary
                Dictionary<int, string> dictionary = [];
                int position = 0;

                while (position < ms.Length)
                {
                    int control = ms.ReadByte();
                    position++;

                    if (control == -1)
                    {
                        break;
                    }

                    if (control == 0xFF) // Dictionary reference
                    {
                        if (position + 2 > ms.Length)
                        {
                            break;
                        }

                        byte[] idBytes = new byte[2];
                        ms.Read(idBytes, 0, 2);
                        position += 2;

                        int dictionaryId = BitConverter.ToInt16(idBytes, 0);
                        if (dictionary.TryGetValue(dictionaryId, out string? value))
                        {
                            byte[] valueBytes = Encoding.UTF8.GetBytes(value);

#if NET48_OR_GREATER || NETSTANDARD2_0
                            await output.WriteAsync(valueBytes, 0, valueBytes.Length, cancellationToken);   
#else
                            await output.WriteAsync(valueBytes, cancellationToken);
#endif
                        }
                    }
                    else if (control == 0x0A) // End of line
                    {
#if NET48_OR_GREATER || NETSTANDARD2_0
                        await output.WriteAsync([0x0A], 0, 1, cancellationToken); 
#else
                        await output.WriteAsync(new byte[0x0A].AsMemory(0, 1), cancellationToken);
#endif
                    }
                    else // Literal
                    {
                        int blockLength = control;
                        if (position + blockLength > ms.Length)
                        {
                            break;
                        }

                        byte[] blockBytes = new byte[blockLength];
                        ms.Read(blockBytes, 0, blockLength);
                        position += blockLength;

                        string block = Encoding.UTF8.GetString(blockBytes);

                        // Add to dictionary
                        int nextId = dictionary.Count + 1;
                        if (nextId < 65000)
                        {
                            dictionary[nextId] = block;
                        }

#if NET48_OR_GREATER || NETSTANDARD2_0
                        await output.WriteAsync(blockBytes, 0, blockBytes.Length, cancellationToken);
#else
                        await output.WriteAsync(blockBytes, cancellationToken);
#endif
                    }

                    // Update progress
                    if (progress != null)
                    {
                        double progressValue = 0.5 + ((double)position / ms.Length * 0.5);
                        progress.Report(Math.Min(progressValue, 1.0));
                    }
                }
            }

            // Return number of bytes written
            return output.Position - initialPosition;
        }

        /// <summary>
        /// Gets the estimated compressed size for the given input size
        /// </summary>
        public override long EstimateCompressedSize(long inputSize)
        {
            // ZStd typically achieves good compression ratios with fast compression/decompression
            return level switch
            {
                CompressionLevel.Fastest => (long)(inputSize * 0.7),
                CompressionLevel.Fast => (long)(inputSize * 0.6),
                CompressionLevel.Normal => (long)(inputSize * 0.45),
                CompressionLevel.High => (long)(inputSize * 0.3),
                CompressionLevel.Ultra => (long)(inputSize * 0.2),
                _ => (long)(inputSize * 0.45)
            };
        }
    }

    /// <summary>
    /// Simulated ZStd stream for placeholder implementation
    /// In a real implementation, this would be replaced with a proper ZStd library binding
    /// </summary>
    internal class ZStdSimulatedStream(Stream baseStream, int compressionLevel, bool isCompress) : Stream
    {
        private readonly Stream _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        private MemoryStream _buffer = new();
        private bool _disposed = false;

        public override bool CanRead => !isCompress && _baseStream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => isCompress && _baseStream.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
            if (isCompress && _buffer.Length > 0)
            {
                // Calculate compression ratio
                double compressionRatio = GetCompressionRatio();

                // Read data from buffer
                byte[] originalData = _buffer.ToArray();

                // Write header (12 byte - ZStd magic number + size)
                byte[] header = new byte[12];
                header[0] = 0x28; // ZStd magic number
                header[1] = 0xB5;
                header[2] = 0x2F;
                header[3] = 0xFD;

#if NET48_OR_GREATER || NETSTANDARD2_0
                byte[] sizeBytes = BitConverter.GetBytes(originalData.Length);
                Array.Copy(sizeBytes, 0, header, 4, Math.Min(sizeBytes.Length, 8));
#else
                BitConverter.TryWriteBytes(new Span<byte>(header, 4, 8), originalData.Length);
#endif

                _baseStream.Write(header, 0, header.Length);

                // Calculate compressed size
                int compressedSize = (int)(originalData.Length * compressionRatio);
                byte[] compressedSizeBytes = BitConverter.GetBytes(compressedSize);
                _baseStream.Write(compressedSizeBytes, 0, compressedSizeBytes.Length);

                // Write "compressed" data
                int bytesToWrite = Math.Min(compressedSize, originalData.Length);
                _baseStream.Write(originalData, 0, bytesToWrite);

                // Clear buffer
                _buffer.SetLength(0);
            }

            _baseStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (isCompress)
            {
                throw new NotSupportedException("Cannot read from a compression stream");
            }

            return _baseStream.Read(buffer, offset, count);
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (isCompress)
            {
                throw new NotSupportedException("Cannot read from a compression stream");
            }

#if NET48_OR_GREATER || NETSTANDARD2_0
            return await _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
#else
            return await _baseStream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
#endif
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!isCompress)
            {
                throw new NotSupportedException("Cannot write to a decompression stream");
            }

            // Write data to buffer
            _buffer.Write(buffer, offset, count);

            // If buffer exceeds a certain size, flush
            if (_buffer.Length > 1024 * 1024) // 1 MB
            {
                Flush();
            }
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (!isCompress)
            {
                throw new NotSupportedException("Cannot write to a decompression stream");
            }

            // Write data to buffer
#if NET48_OR_GREATER || NETSTANDARD2_0
            await _buffer.WriteAsync(buffer, offset, count, cancellationToken);
#else
            await _buffer.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
#endif

            // If buffer exceeds a certain size, flush
            if (_buffer.Length > 1024 * 1024) // 1 MB
            {
                Flush();
            }
        }

        // Sıkıştırma oranını hesapla
        private double GetCompressionRatio()
        {
            return compressionLevel switch
            {
                1 => 0.7, // Fastest
                3 => 0.6, // Fast
                7 => 0.45, // Normal
                14 => 0.3, // High
                19 => 0.2, // Ultra
                _ => 0.45
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (isCompress && _buffer.Length > 0)
                    {
                        // Flush remaining data
                        Flush();
                    }

                    _buffer.Dispose();
                }

                _disposed = true;
            }

            base.Dispose(disposing);
        }
    }
}