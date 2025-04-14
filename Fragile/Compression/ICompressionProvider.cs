using System;
using System.IO;

namespace Fragile.Compression
{
    /// <summary>
    /// Interface for compression providers used in the Fragile library.
    /// </summary>
    public interface ICompressionProvider
    {
        /// <summary>
        /// Gets the compression algorithm supported by this provider.
        /// </summary>
        CompressionAlgorithm Algorithm { get; }

        /// <summary>
        /// Compresses the input stream to the output stream using the specified options.
        /// </summary>
        /// <param name="input">The input stream to compress.</param>
        /// <param name="output">The output stream to write compressed data to.</param>
        /// <param name="options">The compression options to use.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> or <paramref name="output"/> is null.</exception>
        /// <exception cref="NotSupportedException">Thrown when the specified options are not supported by this provider.</exception>
        void Compress(Stream input, Stream output, CompressionOptions options);

        /// <summary>
        /// Decompresses the input stream to the output stream.
        /// </summary>
        /// <param name="input">The input stream containing compressed data.</param>
        /// <param name="output">The output stream to write decompressed data to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> or <paramref name="output"/> is null.</exception>
        /// <exception cref="InvalidDataException">Thrown when the input stream contains invalid compressed data.</exception>
        void Decompress(Stream input, Stream output);
    }

    /// <summary>
    /// Base class for compression providers, providing common functionality.
    /// </summary>
    public abstract class CompressionProviderBase : ICompressionProvider
    {
        /// <inheritdoc/>
        public abstract CompressionAlgorithm Algorithm { get; }

        /// <inheritdoc/>
        public virtual void Compress(Stream input, Stream output, CompressionOptions options)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input), "Input stream cannot be null.");

            if (output == null)
                throw new ArgumentNullException(nameof(output), "Output stream cannot be null.");

            if (options == null)
                throw new ArgumentNullException(nameof(options), "Compression options cannot be null.");

            if (options.Algorithm != Algorithm)
                throw new NotSupportedException($"This provider does not support {options.Algorithm} compression.");

            // Call the abstract method for specific implementation
            CompressInternal(input, output, options);
        }

        /// <inheritdoc/>
        public virtual void Decompress(Stream input, Stream output)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input), "Input stream cannot be null.");

            if (output == null)
                throw new ArgumentNullException(nameof(output), "Output stream cannot be null.");

            // Call the abstract method for specific implementation
            DecompressInternal(input, output);
        }

        /// <summary>
        /// Internal method to perform compression, to be implemented by derived classes.
        /// </summary>
        /// <param name="input">The input stream to compress.</param>
        /// <param name="output">The output stream to write compressed data to.</param>
        /// <param name="options">The compression options to use.</param>
        protected abstract void CompressInternal(Stream input, Stream output, CompressionOptions options);

        /// <summary>
        /// Internal method to perform decompression, to be implemented by derived classes.
        /// </summary>
        /// <param name="input">The input stream containing compressed data.</param>
        /// <param name="output">The output stream to write decompressed data to.</param>
        protected abstract void DecompressInternal(Stream input, Stream output);
    }
} 