using Fragile.Core.Options;
using Fragile.Interfaces.Providers;

namespace Fragile.Implementations.Providers.Checksum;

/// <summary>
/// Provides CRC32 checksum calculation.
/// </summary>
internal class Crc32ChecksumProvider : IChecksumProvider
{
    // Standard CRC32 polynomial (IEEE 802.3)
    private const uint Polynomial = 0xEDB88320u;
    private static readonly uint[] _table = InitializeTable();

    private readonly int _bufferSize;

    public int ChecksumLengthBytes => 4; // CRC32 is 32 bits = 4 bytes

    public Crc32ChecksumProvider(int bufferSize = 81920)
    {
        _bufferSize = bufferSize > 0 ? bufferSize : 81920;
    }

    private static uint[] InitializeTable()
    {
        uint[] createTable = new uint[256];
        for (int i = 0; i < 256; i++)
        {
            uint entry = (uint)i;
            for (int j = 0; j < 8; j++)
            {
                if ((entry & 1) == 1)
                {
                    entry = (entry >> 1) ^ Polynomial;
                }
                else
                {
                    entry = entry >> 1;
                }
            }
            createTable[i] = entry;
        }
        return createTable;
    }

    public async Task<byte[]> ComputeChecksumAsync(Stream source, ChecksumOptions options, CancellationToken cancellationToken = default)
    {
        uint crc = 0xFFFFFFFFu; // Initial value
        byte[] buffer = new byte[_bufferSize];
        int bytesRead;

        // Store initial position and ensure stream is seekable if we need to reset
        long initialPosition = -1;
        bool canSeek = source.CanSeek;
        if (canSeek)
        {
            initialPosition = source.Position;
        }

        try
        {
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                for (int i = 0; i < bytesRead; i++)
                {
                    crc = (crc >> 8) ^ _table[(crc ^ buffer[i]) & 0xFF];
                }
            }
        }
        finally
        {
            // Reset stream position if possible
            if (canSeek && initialPosition != -1)
            {
                source.Position = initialPosition;
            }
        }

        crc ^= 0xFFFFFFFFu; // Final XOR
        return BitConverter.GetBytes(crc);
    }

    public byte[] ComputeChecksum(Stream source, ChecksumOptions options)
    {
        uint crc = 0xFFFFFFFFu;
        byte[] buffer = new byte[_bufferSize];
        int bytesRead;

        long initialPosition = -1;
        bool canSeek = source.CanSeek;
        if (canSeek)
        {
            initialPosition = source.Position;
        }

        try
        {
            while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < bytesRead; i++)
                {
                    crc = (crc >> 8) ^ _table[(crc ^ buffer[i]) & 0xFF];
                }
            }
        }
        finally
        {
            if (canSeek && initialPosition != -1)
            {
                source.Position = initialPosition;
            }
        }

        crc ^= 0xFFFFFFFFu;
        return BitConverter.GetBytes(crc);
    }
}