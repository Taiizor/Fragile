using System;
using System.Collections.Generic;

namespace Fragile.Compression
{
    /// <summary>
    /// Factory class for creating compression providers based on the specified algorithm.
    /// </summary>
    public static class CompressionProviderFactory
    {
        private static readonly Dictionary<CompressionAlgorithm, Func<ICompressionProvider>> _providerFactories;

        static CompressionProviderFactory()
        {
            _providerFactories = new Dictionary<CompressionAlgorithm, Func<ICompressionProvider>>
            {
                { CompressionAlgorithm.Store, () => new StoreCompressionProvider() },
                { CompressionAlgorithm.Deflate, () => new DeflateCompressionProvider() },
                { CompressionAlgorithm.LZMA, () => throw new NotSupportedException("LZMA compression is not currently implemented.") },
                { CompressionAlgorithm.BZip2, () => throw new NotSupportedException("BZip2 compression is not currently implemented.") },
                { CompressionAlgorithm.ZStd, () => throw new NotSupportedException("ZStd compression is not currently implemented.") },
                { CompressionAlgorithm.LZ4, () => throw new NotSupportedException("LZ4 compression is not currently implemented.") }
            };
        }

        /// <summary>
        /// Creates a compression provider for the specified algorithm.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to create a provider for.</param>
        /// <returns>An instance of a compression provider supporting the specified algorithm.</returns>
        /// <exception cref="NotSupportedException">Thrown when the specified algorithm is not supported.</exception>
        public static ICompressionProvider CreateProvider(CompressionAlgorithm algorithm)
        {
            if (_providerFactories.TryGetValue(algorithm, out var factory))
            {
                return factory();
            }

            throw new NotSupportedException($"No compression provider found for algorithm: {algorithm}");
        }

        /// <summary>
        /// Registers a new compression provider factory for a specific algorithm.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to register the provider for.</param>
        /// <param name="factory">The factory function that creates the provider.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
        public static void RegisterProvider(CompressionAlgorithm algorithm, Func<ICompressionProvider> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory), "Factory function cannot be null.");

            _providerFactories[algorithm] = factory;
        }
    }
} 