using System;
using System.IO;

namespace Fragile.Integrity
{
    /// <summary>
    /// Interface for checksum providers used in the Fragile library for data integrity verification.
    /// </summary>
    public interface IChecksumProvider
    {
        /// <summary>
        /// Gets the checksum algorithm supported by this provider.
        /// </summary>
        ChecksumAlgorithm Algorithm { get; }

        /// <summary>
        /// Computes the checksum of the input stream.
        /// </summary>
        /// <param name="input">The input stream to compute the checksum for.</param>
        /// <returns>The computed checksum as a byte array.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is null.</exception>
        byte[] ComputeChecksum(Stream input);

        /// <summary>
        /// Verifies if the provided checksum matches the computed checksum of the input stream.
        /// </summary>
        /// <param name="input">The input stream to compute the checksum for.</param>
        /// <param name="checksum">The checksum to verify against.</param>
        /// <returns>True if the computed checksum matches the provided checksum; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> or <paramref name="checksum"/> is null.</exception>
        bool VerifyChecksum(Stream input, byte[] checksum);
    }

    /// <summary>
    /// Base class for checksum providers, providing common functionality.
    /// </summary>
    public abstract class ChecksumProviderBase : IChecksumProvider
    {
        /// <inheritdoc/>
        public abstract ChecksumAlgorithm Algorithm { get; }

        /// <inheritdoc/>
        public virtual byte[] ComputeChecksum(Stream input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input), "Input stream cannot be null.");

            // Call the abstract method for specific implementation
            return ComputeChecksumInternal(input);
        }

        /// <inheritdoc/>
        public virtual bool VerifyChecksum(Stream input, byte[] checksum)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input), "Input stream cannot be null.");

            if (checksum == null)
                throw new ArgumentNullException(nameof(checksum), "Checksum cannot be null.");

            // Compute the checksum and compare with the provided one
            byte[] computedChecksum = ComputeChecksumInternal(input);
            if (computedChecksum.Length != checksum.Length)
                return false;

            for (int i = 0; i < computedChecksum.Length; i++)
            {
                if (computedChecksum[i] != checksum[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Internal method to compute the checksum, to be implemented by derived classes.
        /// </summary>
        /// <param name="input">The input stream to compute the checksum for.</param>
        /// <returns>The computed checksum as a byte array.</returns>
        protected abstract byte[] ComputeChecksumInternal(Stream input);
    }
} 