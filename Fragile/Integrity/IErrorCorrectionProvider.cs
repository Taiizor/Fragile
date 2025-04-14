using System;
using System.IO;

namespace Fragile.Integrity
{
    /// <summary>
    /// Interface for error correction providers used in the Fragile library for data recovery.
    /// </summary>
    public interface IErrorCorrectionProvider
    {
        /// <summary>
        /// Encodes data with error correction information.
        /// </summary>
        /// <param name="input">The input stream to encode.</param>
        /// <param name="output">The output stream to write encoded data to.</param>
        /// <param name="level">The error correction level as a percentage of data size.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> or <paramref name="output"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="level"/> is invalid.</exception>
        void Encode(Stream input, Stream output, int level);

        /// <summary>
        /// Decodes data and attempts to correct errors using error correction information.
        /// </summary>
        /// <param name="input">The input stream containing encoded data.</param>
        /// <param name="output">The output stream to write decoded data to.</param>
        /// <returns>True if decoding and error correction were successful; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> or <paramref name="output"/> is null.</exception>
        bool Decode(Stream input, Stream output);
    }

    /// <summary>
    /// Base class for error correction providers, providing common functionality.
    /// </summary>
    public abstract class ErrorCorrectionProviderBase : IErrorCorrectionProvider
    {
        /// <inheritdoc/>
        public virtual void Encode(Stream input, Stream output, int level)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input), "Input stream cannot be null.");

            if (output == null)
                throw new ArgumentNullException(nameof(output), "Output stream cannot be null.");

            if (level < 0 || level > 100)
                throw new ArgumentOutOfRangeException(nameof(level), "Error correction level must be between 0 and 100.");

            EncodeInternal(input, output, level);
        }

        /// <inheritdoc/>
        public virtual bool Decode(Stream input, Stream output)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input), "Input stream cannot be null.");

            if (output == null)
                throw new ArgumentNullException(nameof(output), "Output stream cannot be null.");

            return DecodeInternal(input, output);
        }

        /// <summary>
        /// Internal method to encode data with error correction, to be implemented by derived classes.
        /// </summary>
        /// <param name="input">The input stream to encode.</param>
        /// <param name="output">The output stream to write encoded data to.</param>
        /// <param name="level">The error correction level as a percentage of data size.</param>
        protected abstract void EncodeInternal(Stream input, Stream output, int level);

        /// <summary>
        /// Internal method to decode data and correct errors, to be implemented by derived classes.
        /// </summary>
        /// <param name="input">The input stream containing encoded data.</param>
        /// <param name="output">The output stream to write decoded data to.</param>
        /// <returns>True if decoding and error correction were successful; otherwise, false.</returns>
        protected abstract bool DecodeInternal(Stream input, Stream output);
    }
} 