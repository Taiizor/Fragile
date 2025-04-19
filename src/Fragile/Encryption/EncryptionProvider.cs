using Fragile.Models;
using System.Security.Cryptography;

namespace Fragile.Encryption
{
    /// <summary>
    /// Abstract base class for encryption algorithm providers
    /// </summary>
    public abstract class EncryptionProvider
    {
        /// <summary>
        /// The encryption method used by this provider
        /// </summary>
        public abstract EncryptionMethod Method { get; }

        /// <summary>
        /// Creates an encryption provider for the specified method
        /// </summary>
        /// <param name="method">The encryption method to use</param>
        /// <param name="password">Password for encryption/decryption</param>
        /// <returns>A suitable encryption provider</returns>
        public static EncryptionProvider Create(EncryptionMethod method, string password)
        {
            if (string.IsNullOrWhiteSpace(password) && method != EncryptionMethod.None)
            {
                throw new ArgumentException("Password cannot be null or empty for encrypted archives", nameof(password));
            }

            return method switch
            {
                EncryptionMethod.None => new NoneEncryptionProvider(),
                EncryptionMethod.AES128 => new AesEncryptionProvider(password, 128),
                EncryptionMethod.AES256 => new AesEncryptionProvider(password, 256),
                EncryptionMethod.ChaCha20 => new ChaCha20EncryptionProvider(password),
                EncryptionMethod.Twofish => new TwofishEncryptionProvider(password),
                _ => throw new NotSupportedException($"Encryption method {method} is not supported")
            };
        }

        /// <summary>
        /// Creates an encryption provider based on the provided options
        /// </summary>
        /// <param name="options">Options containing encryption settings</param>
        /// <returns>A suitable encryption provider</returns>
        public static EncryptionProvider Create(FragileOptions options)
        {
#if NET48_OR_GREATER || NETSTANDARD2_0_OR_GREATER
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
#else
            ArgumentNullException.ThrowIfNull(options);
#endif

            // If encryption is disabled, use the None provider
            if (!options.EnableEncryption)
            {
                return new NoneEncryptionProvider();
            }

            // Otherwise, create a provider based on the selected method and password
            if (string.IsNullOrWhiteSpace(options.Password) && options.EncryptionMethod != EncryptionMethod.None)
            {
                throw new ArgumentException("Password cannot be null or empty when encryption is enabled", nameof(options.Password));
            }

            return options.EncryptionMethod switch
            {
                EncryptionMethod.None => new NoneEncryptionProvider(),
                EncryptionMethod.AES128 => new AesEncryptionProvider(options.Password, 128),
                EncryptionMethod.AES256 => new AesEncryptionProvider(options.Password, 256),
                EncryptionMethod.ChaCha20 => new ChaCha20EncryptionProvider(options.Password),
                EncryptionMethod.Twofish => new TwofishEncryptionProvider(options.Password),
                _ => throw new NotSupportedException($"Encryption method {options.EncryptionMethod} is not supported")
            };
        }

        /// <summary>
        /// Encrypts the input stream to the output stream
        /// </summary>
        /// <param name="input">Source stream to encrypt</param>
        /// <param name="output">Destination stream for encrypted data</param>
        /// <param name="progress">Optional progress reporting</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The number of bytes written to the output stream</returns>
        public abstract Task<long> EncryptAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Decrypts the input stream to the output stream
        /// </summary>
        /// <param name="input">Source stream with encrypted data</param>
        /// <param name="output">Destination stream for decrypted data</param>
        /// <param name="progress">Optional progress reporting</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The number of bytes written to the output stream</returns>
        public abstract Task<long> DecryptAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the storage overhead for the encryption (headers, padding, etc.)
        /// </summary>
        /// <returns>The size of encryption overhead in bytes</returns>
        public abstract int GetOverheadSize();

        /// <summary>
        /// Generates a derived key from a password using PBKDF2
        /// </summary>
        /// <param name="password">The password</param>
        /// <param name="salt">Salt value (should be at least 8 bytes)</param>
        /// <param name="keySize">Size of the key in bytes</param>
        /// <param name="iterations">Number of iterations (higher is more secure but slower)</param>
        /// <returns>The derived key</returns>
        protected static byte[] DeriveKeyFromPassword(string password, byte[] salt, int keySize, int iterations = 10000)
        {
#if NETSTANDARD2_0
            using Rfc2898DeriveBytes pbkdf2 = new(password, salt, iterations);
            return pbkdf2.GetBytes(keySize);
#else
            using Rfc2898DeriveBytes pbkdf2 = new(password, salt, iterations, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(keySize);
#endif
        }
    }
}