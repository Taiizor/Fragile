using System.IO;
using System.Security.Cryptography;

namespace Fragile.Integrity
{
    /// <summary>
    /// Provides MD5 checksum computation for data integrity verification in the Fragile library.
    /// </summary>
    public class Md5ChecksumProvider : ChecksumProviderBase
    {
        /// <inheritdoc/>
        public override ChecksumAlgorithm Algorithm => ChecksumAlgorithm.MD5;

        /// <inheritdoc/>
        protected override byte[] ComputeChecksumInternal(Stream input)
        {
            using (var md5 = MD5.Create())
            {
                return md5.ComputeHash(input);
            }
        }
    }
} 