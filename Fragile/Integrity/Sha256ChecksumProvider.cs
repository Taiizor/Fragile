using System.IO;
using System.Security.Cryptography;

namespace Fragile.Integrity
{
    /// <summary>
    /// Provides SHA-256 checksum computation for data integrity verification in the Fragile library.
    /// </summary>
    public class Sha256ChecksumProvider : ChecksumProviderBase
    {
        /// <inheritdoc/>
        public override ChecksumAlgorithm Algorithm => ChecksumAlgorithm.SHA256;

        /// <inheritdoc/>
        protected override byte[] ComputeChecksumInternal(Stream input)
        {
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(input);
            }
        }
    }
} 