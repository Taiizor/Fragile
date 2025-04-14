using System;
using System.IO;

namespace Fragile.Encryption
{
    /// <summary>
    /// Interface for encryption providers used in the Fragile library.
    /// </summary>
    public interface IEncryptionProvider
    {
        /// <summary>
        /// Gets the encryption algorithm supported by this provider.
        /// </summary>
        EncryptionAlgorithm Algorithm { get; }

        /// <summary>
        /// Encrypts the input stream to the output stream using the specified options.
        /// </summary>
        /// <param name="input">The input stream to encrypt.</param>
        /// <param name="output">The output stream to write encrypted data to.</param>
        /// <param name="options">The encryption options to use.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> or <paramref name="output"/> is null.</exception>
        /// <exception cref="NotSupportedException">Thrown when the specified options are not supported by this provider.</exception>
        void Encrypt(Stream input, Stream output, EncryptionOptions options);

        /// <summary>
        /// Decrypts the input stream to the output stream using the specified options.
        /// </summary>
        /// <param name="input">The input stream containing encrypted data.</param>
        /// <param name="output">The output stream to write decrypted data to.</param>
        /// <param name="options">The encryption options to use.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> or <paramref name="output"/> is null.</exception>
        /// <exception cref="InvalidDataException">Thrown when the input stream contains invalid encrypted data.</exception>
        void Decrypt(Stream input, Stream output, EncryptionOptions options);
    }

    /// <summary>
    /// Base class for encryption providers, providing common functionality.
    /// </summary>
    public abstract class EncryptionProviderBase : IEncryptionProvider
    {
        /// <inheritdoc/>
        public abstract EncryptionAlgorithm Algorithm { get; }

        /// <inheritdoc/>
        public virtual void Encrypt(Stream input, Stream output, EncryptionOptions options)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input), "Input stream cannot be null.");

            if (output == null)
                throw new ArgumentNullException(nameof(output), "Output stream cannot be null.");

            if (options == null)
                throw new ArgumentNullException(nameof(options), "Encryption options cannot be null.");

            if (options.Algorithm != Algorithm)
                throw new NotSupportedException($"This provider does not support {options.Algorithm} encryption.");

            // Call the abstract method for specific implementation
            EncryptInternal(input, output, options);
        }

        /// <inheritdoc/>
        public virtual void Decrypt(Stream input, Stream output, EncryptionOptions options)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input), "Input stream cannot be null.");

            if (output == null)
                throw new ArgumentNullException(nameof(output), "Output stream cannot be null.");

            if (options == null)
                throw new ArgumentNullException(nameof(options), "Encryption options cannot be null.");

            if (options.Algorithm != Algorithm)
                throw new NotSupportedException($"This provider does not support {options.Algorithm} encryption.");

            // Call the abstract method for specific implementation
            DecryptInternal(input, output, options);
        }

        /// <summary>
        /// Internal method to perform encryption, to be implemented by derived classes.
        /// </summary>
        /// <param name="input">The input stream to encrypt.</param>
        /// <param name="output">The output stream to write encrypted data to.</param>
        /// <param name="options">The encryption options to use.</param>
        protected abstract void EncryptInternal(Stream input, Stream output, EncryptionOptions options);

        /// <summary>
        /// Internal method to perform decryption, to be implemented by derived classes.
        /// </summary>
        /// <param name="input">The input stream containing encrypted data.</param>
        /// <param name="output">The output stream to write decrypted data to.</param>
        /// <param name="options">The encryption options to use.</param>
        protected abstract void DecryptInternal(Stream input, Stream output, EncryptionOptions options);
    }
} 