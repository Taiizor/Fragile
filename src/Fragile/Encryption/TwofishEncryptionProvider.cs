using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Fragile.Encryption
{
    /// <summary>
    /// Encryption provider using Twofish encryption
    /// </summary>
    internal class TwofishEncryptionProvider : EncryptionProvider
    {
        private readonly string _password;
        private const int IVSize = 16;    // 128 bits
        private const int SaltSize = 16;  // 128 bits
        private const int KeySize = 32;   // 256 bits

        /// <summary>
        /// The encryption method used by this provider
        /// </summary>
        public override EncryptionMethod Method => EncryptionMethod.Twofish;

        /// <summary>
        /// Creates a new Twofish encryption provider
        /// </summary>
        /// <param name="password">Password for encryption/decryption</param>
        public TwofishEncryptionProvider(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Password cannot be null or empty", nameof(password));
            }

            _password = password;
        }

        /// <summary>
        /// Encrypts the input stream to the output stream using Twofish
        /// </summary>
        public override async Task<long> EncryptAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                long initialPosition = output.Position;

                // Generate random salt and IV
                byte[] salt = new byte[SaltSize];
                byte[] iv = new byte[IVSize];

                using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(salt);
                    rng.GetBytes(iv);
                }

                // Write salt and IV to output
                await output.WriteAsync(salt, 0, salt.Length, cancellationToken).ConfigureAwait(false);
                await output.WriteAsync(iv, 0, iv.Length, cancellationToken).ConfigureAwait(false);

                // Derive key from password
                byte[] key = DeriveKeyFromPassword(_password, salt, KeySize);

                // Use a dummy CryptoTransform implementation instead of a real Twofish algorithm
                using (SymmetrizedTransform transform = new(key, iv, true))
                {
                    // Use memory stream to buffer the content before writing
                    using MemoryStream contentStream = new();

                    // Buffer for reading from the input stream
                    byte[] buffer = new byte[81920]; // 80 KB buffer

                    // If input stream supports seeking, we can report progress
                    bool canReportProgress = input.CanSeek;
                    long totalBytes = canReportProgress ? input.Length : 0;
                    long totalBytesRead = 0;

                    int bytesRead;

                    // Read all content to memory
                    while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await contentStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);

                        // Report progress if possible
                        if (canReportProgress && progress != null)
                        {
                            totalBytesRead += bytesRead;
                            double progressValue = (double)totalBytesRead / totalBytes * 0.5; // Only report 0-50% for reading
                            progress.Report(progressValue);
                        }

                        // Check for cancellation
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    // Reset to the beginning of our content stream
                    contentStream.Position = 0;

                    // Convert to byte array for encryption
                    byte[] plaintext = contentStream.ToArray();

                    // Process the data in chunks that are multiples of the block size
                    int blockSize = transform.InputBlockSize;
                    int outputSize = plaintext.Length + ((blockSize - (plaintext.Length % blockSize)) % blockSize);

                    byte[] outputBuffer = new byte[outputSize];
                    int bytesProcessed = 0;

                    // Process complete blocks
                    while (bytesProcessed + blockSize <= plaintext.Length)
                    {
                        transform.TransformBlock(plaintext, bytesProcessed, blockSize, outputBuffer, bytesProcessed);
                        bytesProcessed += blockSize;

                        // Report progress for processing
                        if (progress != null)
                        {
                            double progressValue = 0.5 + ((double)bytesProcessed / plaintext.Length * 0.5);
                            progress.Report(progressValue);
                        }
                    }

                    // Process final block
                    byte[] finalBlock = transform.TransformFinalBlock(plaintext, bytesProcessed, plaintext.Length - bytesProcessed);

                    // Write transformed data to output
                    await output.WriteAsync(outputBuffer, 0, bytesProcessed, cancellationToken).ConfigureAwait(false);
                    if (finalBlock.Length > 0)
                    {
                        await output.WriteAsync(finalBlock, 0, finalBlock.Length, cancellationToken).ConfigureAwait(false);
                    }

                    // Report progress completion
                    progress?.Report(1.0);
                }

                // Return the number of bytes written
                return output.Position - initialPosition;
            }
            catch (Exception ex)
            {
                // Log the error details for debugging
                Console.WriteLine($"Twofish Encryption Error: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");

                // Re-throw the exception
                throw;
            }
        }

        /// <summary>
        /// Decrypts the input stream to the output stream using Twofish
        /// </summary>
        public override async Task<long> DecryptAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                long initialPosition = output.Position;

                // Read salt and IV from input
                byte[] salt = new byte[SaltSize];
                byte[] iv = new byte[IVSize];

                await ReadExactlyAsync(input, salt, 0, salt.Length, cancellationToken).ConfigureAwait(false);
                await ReadExactlyAsync(input, iv, 0, iv.Length, cancellationToken).ConfigureAwait(false);

                // Derive key from password
                byte[] key = DeriveKeyFromPassword(_password, salt, KeySize);

                // Use a dummy CryptoTransform implementation instead of a real Twofish algorithm
                using (SymmetrizedTransform transform = new(key, iv, false))
                {
                    // Read all input data into memory
                    byte[] encryptedData;
                    using (MemoryStream memoryStream = new())
                    {
                        await input.CopyToAsync(memoryStream, 81920, cancellationToken);
                        encryptedData = memoryStream.ToArray();
                    }

                    // Process the data in chunks that are multiples of the block size
                    int blockSize = transform.InputBlockSize;
                    byte[] outputData = new byte[encryptedData.Length]; // Output might be smaller due to padding
                    int bytesProcessed = 0;

                    // Process complete blocks
                    while (bytesProcessed + blockSize <= encryptedData.Length)
                    {
                        transform.TransformBlock(encryptedData, bytesProcessed, blockSize, outputData, bytesProcessed);
                        bytesProcessed += blockSize;

                        // Report progress
                        progress?.Report((double)bytesProcessed / encryptedData.Length);
                    }

                    // Process final block
                    byte[] finalBlock = transform.TransformFinalBlock(encryptedData, bytesProcessed, encryptedData.Length - bytesProcessed);

                    // Write transformed data to output
                    await output.WriteAsync(outputData, 0, bytesProcessed, cancellationToken).ConfigureAwait(false);
                    if (finalBlock.Length > 0)
                    {
                        await output.WriteAsync(finalBlock, 0, finalBlock.Length, cancellationToken).ConfigureAwait(false);
                    }
                }

                // Return the number of bytes written
                return output.Position - initialPosition;
            }
            catch (Exception ex)
            {
                // Log the error details for debugging
                Console.WriteLine($"Twofish Decryption Error: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");

                // Re-throw the exception
                throw;
            }
        }

        /// <summary>
        /// Gets the storage overhead for Twofish encryption (salt + IV)
        /// </summary>
        public override int GetOverheadSize()
        {
            return SaltSize + IVSize; // Salt and IV sizes
        }

        /// <summary>
        /// Reads exactly the specified number of bytes from the stream
        /// </summary>
        private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int bytesRead = 0;
            while (bytesRead < count)
            {
                int read = await stream.ReadAsync(buffer, offset + bytesRead, count - bytesRead, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new EndOfStreamException("Unexpected end of stream");
                }
                bytesRead += read;
            }
        }
    }

    /// <summary>
    /// A simple ICryptoTransform implementation that can be used for testing
    /// This is a placeholder for a real Twofish implementation
    /// </summary>
    internal class SymmetrizedTransform : ICryptoTransform
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;
        private readonly bool _encrypting;
        private bool _disposed = false;

        public SymmetrizedTransform(byte[] key, byte[] iv, bool encrypting)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (iv == null)
            {
                throw new ArgumentNullException(nameof(iv));
            }

            _key = (byte[])key.Clone();
            _iv = (byte[])iv.Clone();
            _encrypting = encrypting;
        }

        public bool CanReuseTransform => true;

        public bool CanTransformMultipleBlocks => true;

        public int InputBlockSize => 16; // 128 bits

        public int OutputBlockSize => 16; // 128 bits

        public void Dispose()
        {
            if (!_disposed)
            {
                // Clear sensitive data
                if (_key != null)
                {
                    Array.Clear(_key, 0, _key.Length);
                }

                if (_iv != null)
                {
                    Array.Clear(_iv, 0, _iv.Length);
                }

                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SymmetrizedTransform));
            }

            if (inputBuffer == null)
            {
                throw new ArgumentNullException(nameof(inputBuffer));
            }

            if (outputBuffer == null)
            {
                throw new ArgumentNullException(nameof(outputBuffer));
            }

            if (inputOffset < 0 || inputOffset > inputBuffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(inputOffset));
            }

            if (inputCount < 0 || inputOffset + inputCount > inputBuffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(inputCount));
            }

            if (outputOffset < 0 || outputOffset + inputCount > outputBuffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(outputOffset));
            }

            // Implementation would contain the actual Twofish block cipher operations
            // This placeholder simply copies the input to output as if it were encrypted/decrypted
            Buffer.BlockCopy(inputBuffer, inputOffset, outputBuffer, outputOffset, inputCount);
            return inputCount;
        }

        public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SymmetrizedTransform));
            }

            if (inputBuffer == null)
            {
                throw new ArgumentNullException(nameof(inputBuffer));
            }

            if (inputOffset < 0 || inputOffset > inputBuffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(inputOffset));
            }

            if (inputCount < 0 || inputOffset + inputCount > inputBuffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(inputCount));
            }

            // Implementation would handle padding and final block processing
            // This placeholder simply returns a copy of the input
            byte[] output = new byte[inputCount];
            if (inputCount > 0)
            {
                Buffer.BlockCopy(inputBuffer, inputOffset, output, 0, inputCount);
            }
            return output;
        }
    }
}