using System;
using System.IO;

namespace Fragile.Integrity
{
    /// <summary>
    /// Provides CRC32 checksum computation for data integrity verification in the Fragile library.
    /// </summary>
    public class Crc32ChecksumProvider : ChecksumProviderBase
    {
        /// <inheritdoc/>
        public override ChecksumAlgorithm Algorithm => ChecksumAlgorithm.CRC32;

        /// <inheritdoc/>
        protected override byte[] ComputeChecksumInternal(Stream input)
        {
            uint crc = 0xFFFFFFFF;
            byte[] buffer = new byte[4096];
            int bytesRead;

            while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < bytesRead; i++)
                {
                    crc = (crc >> 8) ^ Crc32Table[(crc & 0xFF) ^ buffer[i]];
                }
            }

            crc = ~crc;
            return BitConverter.GetBytes(crc);
        }

        // Precomputed CRC32 table for performance
        private static readonly uint[] Crc32Table = InitializeCrc32Table();

        private static uint[] InitializeCrc32Table()
        {
            uint[] table = new uint[256];
            const uint polynomial = 0xEDB88320;

            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) == 1)
                        crc = (crc >> 1) ^ polynomial;
                    else
                        crc >>= 1;
                }
                table[i] = crc;
            }

            return table;
        }
    }
} 