using System;
using System.Collections.Generic;

namespace Fragile.Integrity
{
    /// <summary>
    /// Factory class for creating checksum providers based on the specified algorithm.
    /// </summary>
    public static class ChecksumProviderFactory
    {
        private static readonly Dictionary<ChecksumAlgorithm, Func<IChecksumProvider>> _providerFactories;

        static ChecksumProviderFactory()
        {
            _providerFactories = new Dictionary<ChecksumAlgorithm, Func<IChecksumProvider>>
            {
                { ChecksumAlgorithm.CRC32, () => new Crc32ChecksumProvider() },
                { ChecksumAlgorithm.MD5, () => new Md5ChecksumProvider() },
                { ChecksumAlgorithm.SHA1, () => new Sha1ChecksumProvider() },
                { ChecksumAlgorithm.SHA256, () => new Sha256ChecksumProvider() },
                { ChecksumAlgorithm.SHA512, () => new Sha512ChecksumProvider() }
            };
        }

        /// <summary>
        /// Creates a checksum provider for the specified algorithm.
        /// </summary>
        /// <param name="algorithm">The checksum algorithm to create a provider for.</param>
        /// <returns>An instance of a checksum provider supporting the specified algorithm.</returns>
        /// <exception cref="NotSupportedException">Thrown when the specified algorithm is not supported.</exception>
        public static IChecksumProvider CreateProvider(ChecksumAlgorithm algorithm)
        {
            if (_providerFactories.TryGetValue(algorithm, out var factory))
            {
                return factory();
            }

            throw new NotSupportedException($"No checksum provider found for algorithm: {algorithm}");
        }

        /// <summary>
        /// Registers a new checksum provider factory for a specific algorithm.
        /// </summary>
        /// <param name="algorithm">The checksum algorithm to register the provider for.</param>
        /// <param name="factory">The factory function that creates the provider.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
        public static void RegisterProvider(ChecksumAlgorithm algorithm, Func<IChecksumProvider> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory), "Factory function cannot be null.");

            _providerFactories[algorithm] = factory;
        }
    }
} 