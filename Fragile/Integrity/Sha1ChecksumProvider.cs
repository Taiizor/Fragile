using System.IO;
using System.Security.Cryptography;

namespace Fragile.Integrity
{
    /// <summary>
    /// Provides SHA-1 checksum computation for data integrity verification in the Fragile library.
    /// </summary>
    public class Sha1ChecksumProvider : ChecksumProviderBase
    {
        /// <inheritdoc/>
        public override ChecksumAlgorithm Algorithm => ChecksumAlgorithm.SHA1;

        /// <inheritdoc/>
        protected override byte[] ComputeChecksumInternal(Stream input)
        {
            using (var sha1 = SHA1.Create())
            {
                return sha1.ComputeHash(input);
            }
        }
    }
} 