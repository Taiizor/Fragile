using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Fragile.Encryption
{
    /// <summary>
    /// Encryption provider using AES encryption
    /// </summary>
    internal class AesEncryptionProvider : EncryptionProvider
    {
        private readonly string _password;
        private readonly int _keySize;
        private const int SaltSize = 16; // 128 bits
        private const int IVSize = 16;   // 128 bits
        
        /// <summary>
        /// The encryption method used by this provider
        /// </summary>
        public override EncryptionMethod Method => _keySize == 256 ? EncryptionMethod.AES256 : EncryptionMethod.AES128;
        
        /// <summary>
        /// Creates a new AES encryption provider
        /// </summary>
        /// <param name="password">Password for encryption/decryption</param>
        /// <param name="keySize">Key size in bits (128 or 256)</param>
        public AesEncryptionProvider(string password, int keySize)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Password cannot be null or empty", nameof(password));
            }
            
            if (keySize != 128 && keySize != 256)
            {
                throw new ArgumentException("Key size must be 128 or 256 bits", nameof(keySize));
            }
            
            _password = password;
            _keySize = keySize;
        }
        
        /// <summary>
        /// Encrypts the input stream to the output stream using AES
        /// </summary>
        public override async Task<long> EncryptAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long initialPosition = output.Position;
            
            // Generate random salt and IV
            byte[] salt = new byte[SaltSize];
            byte[] iv = new byte[IVSize];
            
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }
            
            // Write salt and IV to output
            await output.WriteAsync(salt, 0, salt.Length, cancellationToken).ConfigureAwait(false);
            await output.WriteAsync(iv, 0, iv.Length, cancellationToken).ConfigureAwait(false);
            
            // Derive key from password
            byte[] key = DeriveKeyFromPassword(_password, salt, _keySize / 8);
            
            // Create AES encryptor
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                
                using (var cryptoStream = new CryptoStream(output, aes.CreateEncryptor(), CryptoStreamMode.Write, true))
                {
                    // If input stream supports seeking, we can report progress
                    bool canReportProgress = input.CanSeek;
                    long totalBytes = canReportProgress ? input.Length : 0;
                    byte[] buffer = new byte[81920]; // 80 KB buffer
                    
                    int bytesRead;
                    long totalBytesRead = 0;
                    
                    while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await cryptoStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                        
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
        /// Decrypts the input stream to the output stream using AES
        /// </summary>
        public override async Task<long> DecryptAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long initialPosition = output.Position;
            
            // Read salt and IV from input
            byte[] salt = new byte[SaltSize];
            byte[] iv = new byte[IVSize];
            
            await ReadExactlyAsync(input, salt, 0, salt.Length, cancellationToken).ConfigureAwait(false);
            await ReadExactlyAsync(input, iv, 0, iv.Length, cancellationToken).ConfigureAwait(false);
            
            // Derive key from password
            byte[] key = DeriveKeyFromPassword(_password, salt, _keySize / 8);
            
            // Create AES decryptor
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                
                using (var cryptoStream = new CryptoStream(input, aes.CreateDecryptor(), CryptoStreamMode.Read, true))
                {
                    // We can't easily report progress for decryption without knowing the final size
                    byte[] buffer = new byte[81920]; // 80 KB buffer
                    
                    int bytesRead;
                    while ((bytesRead = await cryptoStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await output.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                        
                        // Check for cancellation
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            }
            
            // Return the number of bytes written
            return output.Position - initialPosition;
        }
        
        /// <summary>
        /// Gets the storage overhead for AES encryption (salt + IV)
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
} 