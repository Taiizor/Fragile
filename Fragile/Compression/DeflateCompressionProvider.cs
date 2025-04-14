using System.IO;
using System.IO.Compression;

namespace Fragile.Compression
{
    /// <summary>
    /// Provides Deflate compression for the Fragile library.
    /// </summary>
    public class DeflateCompressionProvider : CompressionProviderBase
    {
        /// <inheritdoc/>
        public override CompressionAlgorithm Algorithm => CompressionAlgorithm.Deflate;

        /// <inheritdoc/>
        protected override void CompressInternal(Stream input, Stream output, CompressionOptions options)
        {
            // Map CompressionLevel enum from options to System.IO.Compression.CompressionLevel
            System.IO.Compression.CompressionLevel compressionLevel;
            switch (options.Level)
            {
                case CompressionLevel.Fastest:
                    compressionLevel = System.IO.Compression.CompressionLevel.Fastest;
                    break;
                case CompressionLevel.Fast:
                case CompressionLevel.Normal:
                    compressionLevel = System.IO.Compression.CompressionLevel.Optimal;
                    break;
                case CompressionLevel.High:
                case CompressionLevel.Ultra:
                    compressionLevel = System.IO.Compression.CompressionLevel.Optimal;
                    break;
                default:
                    compressionLevel = System.IO.Compression.CompressionLevel.Optimal;
                    break;
            }

            using (var deflateStream = new DeflateStream(output, compressionLevel, leaveOpen: true))
            {
                input.CopyTo(deflateStream);
            }
        }

        /// <inheritdoc/>
        protected override void DecompressInternal(Stream input, Stream output)
        {
            using (var deflateStream = new DeflateStream(input, CompressionMode.Decompress, leaveOpen: true))
            {
                deflateStream.CopyTo(output);
            }
        }
    }
} 