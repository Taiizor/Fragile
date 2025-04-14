using System.IO;

namespace Fragile.Compression
{
    /// <summary>
    /// Provides Store (no compression) method for the Fragile library.
    /// </summary>
    public class StoreCompressionProvider : CompressionProviderBase
    {
        /// <inheritdoc/>
        public override CompressionAlgorithm Algorithm => CompressionAlgorithm.Store;

        /// <inheritdoc/>
        protected override void CompressInternal(Stream input, Stream output, CompressionOptions options)
        {
            // Store method simply copies the input to output without any compression
            input.CopyTo(output);
        }

        /// <inheritdoc/>
        protected override void DecompressInternal(Stream input, Stream output)
        {
            // Store method simply copies the input to output as there's no compression
            input.CopyTo(output);
        }
    }
} 