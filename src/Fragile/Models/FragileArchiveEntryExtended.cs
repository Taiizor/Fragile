using Fragile.Compression;
using Fragile.Encryption;
using Fragile.Metadata;

namespace Fragile.Models
{
    /// <summary>
    /// Extended version of FragileArchiveEntry with additional properties
    /// </summary>
    public class FragileArchiveEntryExtended : FragileArchiveEntry
    {
        /// <summary>
        /// Custom internal flags
        /// </summary>
        public int Flags { get; set; }

        /// <summary>
        /// The part index if this entry is split across multiple parts
        /// </summary>
        public int PartIndex { get; set; }

        /// <summary>
        /// The total parts if this entry is split
        /// </summary>
        public int TotalParts { get; set; } = 1;

        /// <summary>
        /// Checksum of the entry content
        /// </summary>
        public byte[] Checksum { get; set; } = [];

        /// <summary>
        /// Whether this entry has error correction data
        /// </summary>
        public bool HasErrorCorrection { get; set; }

        /// <summary>
        /// Metadata for the entry
        /// </summary>
        public EntryMetadata Metadata { get; set; } = new EntryMetadata();

        /// <summary>
        /// Encryption method used for this entry
        /// </summary>
        public EncryptionMethod EncryptionMethod { get; set; } = EncryptionMethod.None;

        /// <summary>
        /// Compression algorithm used for this entry
        /// </summary>
        public CompressionAlgorithm CompressionAlgorithm { get; set; } = CompressionAlgorithm.Deflate;

        /// <summary>
        /// Gets a tag value indicating if the entry has special attributes
        /// </summary>
        public bool IsSpecial()
        {
            return (Flags & 0x01) != 0;
        }

        /// <summary>
        /// Sets the entry as special
        /// </summary>
        /// <param name="isSpecial">Whether the entry is special</param>
        public void SetSpecial(bool isSpecial)
        {
            if (isSpecial)
            {
                Flags |= 0x01;
            }
            else
            {
                Flags &= ~0x01;
            }
        }

        /// <summary>
        /// Creates a new extended entry from a basic entry
        /// </summary>
        /// <param name="entry">The base entry to extend</param>
        /// <returns>An extended entry</returns>
        public static FragileArchiveEntryExtended FromEntry(FragileArchiveEntry entry)
        {
#if NET48_OR_GREATER || NETSTANDARD2_0_OR_GREATER
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }
#else
            ArgumentNullException.ThrowIfNull(entry);
#endif

            if (entry is FragileArchiveEntryExtended extendedEntry)
            {
                return extendedEntry;
            }

            return new FragileArchiveEntryExtended
            {
                Data = entry.Data,
                Path = entry.Path,
                Size = entry.Size,
                Position = entry.Position,
                SourcePath = entry.SourcePath,
                IsDirectory = entry.IsDirectory,
                HeaderOffset = entry.HeaderOffset,
                LastModified = entry.LastModified,
                CompressedSize = entry.CompressedSize,
                PositionOffset = entry.PositionOffset
            };
        }
    }
}