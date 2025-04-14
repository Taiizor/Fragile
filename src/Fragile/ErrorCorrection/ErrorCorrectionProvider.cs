using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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
        /// Creates a new error correction provider
        /// </summary>
        /// <param name="correctionLevel">Error correction level (as percentage)</param>
        protected ErrorCorrectionProvider(int correctionLevel)
        {
            if (correctionLevel is < 0 or > 50)
            {
                throw new ArgumentOutOfRangeException(nameof(correctionLevel), "Error correction level must be between 0-50");
            }

            CorrectionLevel = correctionLevel;
        }

        /// <summary>
        /// Creates an error correction provider with the specified level
        /// </summary>
        /// <param name="correctionLevel">Error correction level (between 0-50)</param>
        /// <returns>Error correction provider</returns>
        public static ErrorCorrectionProvider Create(int correctionLevel)
        {
            if (correctionLevel <= 0)
            {
                return new NoneErrorCorrectionProvider();
            }

            return new ReedSolomonErrorCorrectionProvider(correctionLevel);
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
        public NoneErrorCorrectionProvider() : base(0) { }

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
        private static async Task CopyStreamAsync(Stream input, Stream output,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            byte[] buffer = new byte[81920]; // 80 KB buffer

            // If input stream is seekable, we can report progress
            bool canReportProgress = input.CanSeek;
            long totalBytes = canReportProgress ? input.Length : 0;
            long totalBytesRead = 0;

            int bytesRead;
            while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer, 0, bytesRead, cancellationToken);

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
        public ReedSolomonErrorCorrectionProvider(int correctionLevel) : base(correctionLevel) { }

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

            // Process data in blocks
            byte[] buffer = new byte[dataSize];
            long totalBytesRead = 0;
            long totalBytesWritten = 0;
            long inputLength = input.Length;

            while (true)
            {
                int bytesRead = await input.ReadAsync(buffer, 0, dataSize, cancellationToken);
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
                    await output.WriteAsync(encoded, 0, encoded.Length, cancellationToken);

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
        /// Corrects data using Reed-Solomon error correction codes
        /// </summary>
        public override async Task<(long bytesWritten, int bytesRepaired)> CorrectErrorsAsync(Stream input, Stream output,
            Action<long, int>? reportRepairs = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long initialPosition = output.Position;
            int totalRepaired = 0;

            // If input stream is seekable, we can learn the total size
            long totalBytes = input.CanSeek ? input.Length : 0;
            long processedBytes = 0;

            try
            {
                // Read header
                (int dataSize, int ecSize) = await ReadHeaderAsync(input, cancellationToken);

                // Create Reed-Solomon encoder
                ReedSolomonAlgorithm rs = new(dataSize, ecSize);

                // Encoded block size
                int encodedBlockSize = dataSize + ecSize;

                // Split input data into blocks and decode each block
                byte[] encodedBuffer = new byte[encodedBlockSize];

                while (true)
                {
                    int bytesRead = await ReadExactlyAsync(input, encodedBuffer, 0, encodedBlockSize, cancellationToken);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    // If last block is incomplete, complete the process
                    if (bytesRead < encodedBlockSize)
                    {
                        // Copy remaining data directly
                        await output.WriteAsync(encodedBuffer, 0, Math.Min(bytesRead, dataSize), cancellationToken);
                        break;
                    }

                    try
                    {
                        // Decode with Reed-Solomon and correct errors
                        byte[] decoded = rs.Decode(encodedBuffer);

                        // Check if correction was made
                        int repairedCount = CountRepairs(encodedBuffer, decoded, dataSize, ecSize);

                        // Write decoded data
                        await output.WriteAsync(decoded, 0, dataSize, cancellationToken);

                        // Report repairs
                        if (repairedCount > 0)
                        {
                            totalRepaired += repairedCount;
                            reportRepairs?.Invoke(processedBytes, repairedCount);
                        }
                    }
                    catch (Exception ex)
                    {
                        // If error correction fails, recover as much data as possible
                        await output.WriteAsync(encodedBuffer, 0, Math.Min(bytesRead, dataSize), cancellationToken);
                    }

                    // Report progress
                    processedBytes += encodedBlockSize;
                    if (totalBytes > 0 && progress != null)
                    {
                        double progressValue = (double)processedBytes / totalBytes;
                        progress.Report(progressValue);
                    }

                    // Check for cancellation
                    cancellationToken.ThrowIfCancellationRequested();
                }

                // Final progress update
                progress?.Report(1.0);
            }
            catch (Exception ex)
            {
                // If error correction completely fails, copy remaining data as is
                input.CopyTo(output);
                throw new IOException($"Error occurred during error correction process: {ex.Message}", ex);
            }

            return (output.Position - initialPosition, totalRepaired);
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
        /// Calculates the number of corrected bytes
        /// </summary>
        private static int CountRepairs(byte[] encoded, byte[] decoded, int dataSize, int ecSize)
        {
            int repairedCount = 0;

            // Compare the original data portion
            for (int i = 0; i < Math.Min(dataSize, decoded.Length); i++)
            {
                if (encoded[i + ecSize] != decoded[i])
                {
                    repairedCount++;
                }
            }

            return repairedCount;
        }

        /// <summary>
        /// Writes error correction header information
        /// </summary>
        private static async Task WriteHeaderAsync(Stream output, int dataSize, int ecSize, CancellationToken cancellationToken)
        {
            byte[] header = new byte[8];

            // Magic bytes (RS)
            header[0] = (byte)'R';
            header[1] = (byte)'S';

            // Data size (4 bytes, little-endian)
            header[2] = (byte)(dataSize & 0xFF);
            header[3] = (byte)((dataSize >> 8) & 0xFF);
            header[4] = (byte)((dataSize >> 16) & 0xFF);
            header[5] = (byte)((dataSize >> 24) & 0xFF);

            // Error correction size (2 bytes, little-endian)
            header[6] = (byte)(ecSize & 0xFF);
            header[7] = (byte)((ecSize >> 8) & 0xFF);

            await output.WriteAsync(header, 0, header.Length, cancellationToken);
        }

        /// <summary>
        /// Reads error correction header information
        /// </summary>
        private static async Task<(int dataSize, int ecSize)> ReadHeaderAsync(Stream input, CancellationToken cancellationToken)
        {
            byte[] header = new byte[8];

            if (await input.ReadAsync(header, 0, header.Length, cancellationToken) != header.Length)
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

        /// <summary>
        /// Reads exactly the specified number of bytes from the stream
        /// </summary>
        private static async Task<int> ReadExactlyAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int totalBytesRead = 0;

            while (totalBytesRead < count)
            {
                int bytesRead = await stream.ReadAsync(buffer, offset + totalBytesRead, count - totalBytesRead, cancellationToken);

                if (bytesRead == 0)
                {
                    // End of stream reached
                    break;
                }

                totalBytesRead += bytesRead;
            }

            return totalBytesRead;
        }
    }
}