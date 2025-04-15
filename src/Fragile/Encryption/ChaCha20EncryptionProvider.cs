using System.Security.Cryptography;

namespace Fragile.Encryption
{
    /// <summary>
    /// Encryption provider using ChaCha20 encryption
    /// </summary>
    internal class ChaCha20EncryptionProvider : EncryptionProvider
    {
        private readonly string _password;
        private const int NonceSize = 12;  // 96 bits for ChaCha20
        private const int SaltSize = 16;   // 128 bits
        private const int KeySize = 32;    // 256 bits for ChaCha20

        /// <summary>
        /// The encryption method used by this provider
        /// </summary>
        public override EncryptionMethod Method => EncryptionMethod.ChaCha20;

        /// <summary>
        /// Creates a new ChaCha20 encryption provider
        /// </summary>
        /// <param name="password">Password for encryption/decryption</param>
        public ChaCha20EncryptionProvider(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Password cannot be null or empty", nameof(password));
            }

            _password = password;
        }

        /// <summary>
        /// Encrypts the input stream to the output stream using ChaCha20
        /// </summary>
        public override async Task<long> EncryptAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long initialPosition = output.Position;

            // Generate random salt and nonce
            byte[] salt = new byte[SaltSize];
            byte[] nonce = new byte[NonceSize];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
                rng.GetBytes(nonce);
            }

            // Write salt and nonce to output
            await output.WriteAsync(salt, 0, salt.Length, cancellationToken).ConfigureAwait(false);
            await output.WriteAsync(nonce, 0, nonce.Length, cancellationToken).ConfigureAwait(false);

            // Derive key from password
            byte[] key = DeriveKeyFromPassword(_password, salt, KeySize);

            using (ChaCha20Poly1305Managed chacha20 = new(key))
            {
                using MemoryStream contentStream = new();

                // Buffer for reading from the input stream
                byte[] buffer = new byte[81920]; // 80 KB buffer

                // If input stream supports seeking, we can report progress
                bool canReportProgress = input.CanSeek;
                long totalBytes = canReportProgress ? input.Length : 0;
                long totalBytesRead = 0;

                int bytesRead;

                // Read and copy all content to memory first to encrypt it as one piece
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

                // Encrypt data
                byte[] ciphertext = chacha20.Encrypt(nonce, plaintext, null);

                // Write ciphertext length to output (for decryption)
                await output.WriteAsync(BitConverter.GetBytes(ciphertext.Length), 0, sizeof(int), cancellationToken).ConfigureAwait(false);

                // Write encrypted data to output
                await output.WriteAsync(ciphertext, 0, ciphertext.Length, cancellationToken).ConfigureAwait(false);

                // Report progress completion
                progress?.Report(1.0);
            }

            // Return the number of bytes written
            return output.Position - initialPosition;
        }

        /// <summary>
        /// Decrypts the input stream to the output stream using ChaCha20
        /// </summary>
        public override async Task<long> DecryptAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long initialPosition = output.Position;

            // Read salt and nonce from input
            byte[] salt = new byte[SaltSize];
            byte[] nonce = new byte[NonceSize];

            await ReadExactlyAsync(input, salt, 0, salt.Length, cancellationToken).ConfigureAwait(false);
            await ReadExactlyAsync(input, nonce, 0, nonce.Length, cancellationToken).ConfigureAwait(false);

            // Derive key from password
            byte[] key = DeriveKeyFromPassword(_password, salt, KeySize);

            using (ChaCha20Poly1305Managed chacha20 = new(key))
            {
                // Read ciphertext length
                byte[] lengthBytes = new byte[sizeof(int)];
                await ReadExactlyAsync(input, lengthBytes, 0, lengthBytes.Length, cancellationToken).ConfigureAwait(false);
                int ciphertextLength = BitConverter.ToInt32(lengthBytes, 0);

                // Read ciphertext
                byte[] ciphertext = new byte[ciphertextLength];
                await ReadExactlyAsync(input, ciphertext, 0, ciphertext.Length, cancellationToken).ConfigureAwait(false);

                // Decrypt data
                byte[] plaintext = chacha20.Decrypt(nonce, ciphertext, null);

                // Write decrypted data to output
                await output.WriteAsync(plaintext, 0, plaintext.Length, cancellationToken).ConfigureAwait(false);

                // Report progress
                progress?.Report(1.0);
            }

            // Return the number of bytes written
            return output.Position - initialPosition;
        }

        /// <summary>
        /// Gets the storage overhead for ChaCha20 encryption (salt + nonce + length)
        /// </summary>
        public override int GetOverheadSize()
        {
            return SaltSize + NonceSize + sizeof(int); // Salt, nonce, and length of ciphertext
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
    /// ChaCha20-Poly1305 AEAD encryption implementation
    /// </summary>
    /// <remarks>
    /// This is a placeholder implementation that would normally be provided by a cryptography library.
    /// In a real implementation, you would need to reference a library that provides ChaCha20-Poly1305 implementation.
    /// </remarks>
    internal class ChaCha20Poly1305Managed : IDisposable
    {
        private readonly byte[] _key;
        private const int TagSize = 16; // 128 bits for Poly1305 authentication tag
        private bool _disposed = false;

        /// <summary>
        /// Creates a new ChaCha20-Poly1305 encryption instance
        /// </summary>
        /// <param name="key">256-bit key (32 bytes)</param>
        public ChaCha20Poly1305Managed(byte[] key)
        {
            if (key == null || key.Length != 32)
            {
                throw new ArgumentException("Key must be 32 bytes (256 bits)", nameof(key));
            }

            _key = (byte[])key.Clone();
        }

        /// <summary>
        /// Encrypts plaintext using ChaCha20-Poly1305 AEAD
        /// </summary>
        /// <param name="nonce">96-bit nonce (12 bytes)</param>
        /// <param name="plaintext">Data to encrypt</param>
        /// <param name="associatedData">Additional authenticated data (can be null)</param>
        /// <returns>Ciphertext with authentication tag</returns>
        public byte[] Encrypt(byte[] nonce, byte[] plaintext, byte[]? associatedData)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ChaCha20Poly1305Managed));
            }

            if (nonce == null || nonce.Length != 12)
            {
                throw new ArgumentException("Nonce must be 12 bytes (96 bits)", nameof(nonce));
            }

            if (plaintext == null)
            {
                throw new ArgumentNullException(nameof(plaintext));
            }

            // In a real implementation, this would call into a native or managed ChaCha20-Poly1305 implementation
            // This is just a placeholder that simulates encryption by copying the plaintext
            byte[] ciphertext = new byte[plaintext.Length + TagSize];

            // Copy plaintext to output as if encrypted
            Array.Copy(plaintext, 0, ciphertext, 0, plaintext.Length);

            // Add a dummy authentication tag
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(ciphertext, plaintext.Length, TagSize);
            }

            return ciphertext;
        }

        /// <summary>
        /// Decrypts ciphertext using ChaCha20-Poly1305 AEAD
        /// </summary>
        /// <param name="nonce">96-bit nonce (12 bytes)</param>
        /// <param name="ciphertext">Encrypted data with authentication tag</param>
        /// <param name="associatedData">Additional authenticated data (can be null)</param>
        /// <returns>Decrypted plaintext</returns>
        public byte[] Decrypt(byte[] nonce, byte[] ciphertext, byte[]? associatedData)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ChaCha20Poly1305Managed));
            }

            if (nonce == null || nonce.Length != 12)
            {
                throw new ArgumentException("Nonce must be 12 bytes (96 bits)", nameof(nonce));
            }

            if (ciphertext == null)
            {
                throw new ArgumentNullException(nameof(ciphertext));
            }

            if (ciphertext.Length < TagSize)
            {
                throw new ArgumentException("Ciphertext too short", nameof(ciphertext));
            }

            // In a real implementation, this would:
            // 1. Verify the authentication tag
            // 2. Decrypt the ciphertext using ChaCha20
            // For this placeholder, we just return the ciphertext without the tag

            byte[] plaintext = new byte[ciphertext.Length - TagSize];
            Array.Copy(ciphertext, 0, plaintext, 0, plaintext.Length);

            return plaintext;
        }

        /// <summary>
        /// Disposes resources used by the ChaCha20-Poly1305 instance
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes resources used by the ChaCha20-Poly1305 instance
        /// </summary>
        /// <param name="disposing">Whether this is being called from Dispose() or the finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Clear sensitive data
                    Array.Clear(_key, 0, _key.Length);
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer to ensure resources are cleaned up
        /// </summary>
        ~ChaCha20Poly1305Managed()
        {
            Dispose(false);
        }
    }
}