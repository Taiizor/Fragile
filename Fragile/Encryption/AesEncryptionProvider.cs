using System;
using System.IO;
using System.Security.Cryptography;

namespace Fragile.Encryption
{
    /// <summary>
    /// Provides AES encryption for the Fragile library.
    /// </summary>
    public class AesEncryptionProvider : EncryptionProviderBase
    {
        private readonly EncryptionAlgorithm _algorithm;

        /// <summary>
        /// Initializes a new instance of the <see cref="AesEncryptionProvider"/> class with the specified algorithm.
        /// </summary>
        /// <param name="algorithm">The AES algorithm variant to use (AES128 or AES256).</param>
        /// <exception cref="ArgumentException">Thrown when the algorithm is not AES128 or AES256.</exception>
        public AesEncryptionProvider(EncryptionAlgorithm algorithm)
        {
            if (algorithm != EncryptionAlgorithm.AES128 && algorithm != EncryptionAlgorithm.AES256)
                throw new ArgumentException("AesEncryptionProvider only supports AES128 and AES256 algorithms.", nameof(algorithm));

            _algorithm = algorithm;
        }

        /// <inheritdoc/>
        public override EncryptionAlgorithm Algorithm => _algorithm;

        /// <inheritdoc/>
        protected override void EncryptInternal(Stream input, Stream output, EncryptionOptions options)
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = _algorithm == EncryptionAlgorithm.AES256 ? 256 : 128;
                
                // Derive key from password using PBKDF2
                using (var deriveBytes = new Rfc2898DeriveBytes(options.Password, options.Salt ?? new byte[16], options.Pbkdf2Iterations))
                {
                    aes.Key = deriveBytes.GetBytes(aes.KeySize / 8);
                    aes.IV = deriveBytes.GetBytes(aes.BlockSize / 8);
                }

                // Write IV to the output stream (needed for decryption)
                output.Write(aes.IV, 0, aes.IV.Length);

                using (var cryptoStream = new CryptoStream(output, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true))
                {
                    input.CopyTo(cryptoStream);
                }
            }
        }

        /// <inheritdoc/>
        protected override void DecryptInternal(Stream input, Stream output, EncryptionOptions options)
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = _algorithm == EncryptionAlgorithm.AES256 ? 256 : 128;
                
                // Derive key from password using PBKDF2
                using (var deriveBytes = new Rfc2898DeriveBytes(options.Password, options.Salt ?? new byte[16], options.Pbkdf2Iterations))
                {
                    aes.Key = deriveBytes.GetBytes(aes.KeySize / 8);
                    aes.IV = deriveBytes.GetBytes(aes.BlockSize / 8);
                }

                // Read IV from the input stream
                byte[] iv = new byte[aes.BlockSize / 8];
                int bytesRead = input.Read(iv, 0, iv.Length);
                if (bytesRead != iv.Length)
                    throw new InvalidDataException("Invalid encrypted data: missing initialization vector.");
                aes.IV = iv;

                using (var cryptoStream = new CryptoStream(input, aes.CreateDecryptor(), CryptoStreamMode.Read, leaveOpen: true))
                {
                    cryptoStream.CopyTo(output);
                }
            }
        }
    }
} 