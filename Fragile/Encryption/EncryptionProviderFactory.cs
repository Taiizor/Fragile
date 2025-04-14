using System;
using System.Collections.Generic;

namespace Fragile.Encryption
{
    /// <summary>
    /// Factory class for creating encryption providers based on the specified algorithm.
    /// </summary>
    public static class EncryptionProviderFactory
    {
        private static readonly Dictionary<EncryptionAlgorithm, Func<IEncryptionProvider>> _providerFactories;

        static EncryptionProviderFactory()
        {
            _providerFactories = new Dictionary<EncryptionAlgorithm, Func<IEncryptionProvider>>
            {
                { EncryptionAlgorithm.AES128, () => new AesEncryptionProvider(EncryptionAlgorithm.AES128) },
                { EncryptionAlgorithm.AES256, () => new AesEncryptionProvider(EncryptionAlgorithm.AES256) },
                { EncryptionAlgorithm.ChaCha20, () => throw new NotSupportedException("ChaCha20 encryption is not currently implemented.") },
                { EncryptionAlgorithm.Twofish, () => throw new NotSupportedException("Twofish encryption is not currently implemented.") }
            };
        }

        /// <summary>
        /// Creates an encryption provider for the specified algorithm.
        /// </summary>
        /// <param name="algorithm">The encryption algorithm to create a provider for.</param>
        /// <returns>An instance of an encryption provider supporting the specified algorithm.</returns>
        /// <exception cref="NotSupportedException">Thrown when the specified algorithm is not supported.</exception>
        public static IEncryptionProvider CreateProvider(EncryptionAlgorithm algorithm)
        {
            if (_providerFactories.TryGetValue(algorithm, out var factory))
            {
                return factory();
            }

            throw new NotSupportedException($"No encryption provider found for algorithm: {algorithm}");
        }

        /// <summary>
        /// Registers a new encryption provider factory for a specific algorithm.
        /// </summary>
        /// <param name="algorithm">The encryption algorithm to register the provider for.</param>
        /// <param name="factory">The factory function that creates the provider.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
        public static void RegisterProvider(EncryptionAlgorithm algorithm, Func<IEncryptionProvider> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory), "Factory function cannot be null.");

            _providerFactories[algorithm] = factory;
        }
    }
} 