using System.IO;
using System.Security.Cryptography;

namespace Fragile.Integrity
{
    /// <summary>
    /// Provides SHA-512 checksum computation for data integrity verification in the Fragile library.
    /// </summary>
    public class Sha512ChecksumProvider : ChecksumProviderBase
    {
        /// <inheritdoc/>
        public override ChecksumAlgorithm Algorithm => ChecksumAlgorithm.SHA512;

        /// <inheritdoc/>
        protected override byte[] ComputeChecksumInternal(Stream input)
        {
            using (var sha512 = SHA512.Create())
            {
                return sha512.ComputeHash(input);
            }
        }
    }
} 