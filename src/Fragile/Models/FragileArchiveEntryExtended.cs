using Fragile.Compression;
using Fragile.Encryption;
using Fragile.Metadata;
using System;

namespace Fragile.Models
{
    /// <summary>
    /// Extended version of FragileArchiveEntry with additional properties
    /// </summary>
    public class FragileArchiveEntryExtended : FragileArchiveEntry
    {
        /// <summary>
        /// Metadata for the entry
        /// </summary>
        public EntryMetadata Metadata { get; set; } = new EntryMetadata();

        /// <summary>
        /// Checksum of the entry content
        /// </summary>
        public byte[] Checksum { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Encryption method used for this entry
        /// </summary>
        public EncryptionMethod EncryptionMethod { get; set; } = EncryptionMethod.None;

        /// <summary>
        /// Compression algorithm used for this entry
        /// </summary>
        public CompressionAlgorithm CompressionAlgorithm { get; set; } = CompressionAlgorithm.Deflate;

        /// <summary>
        /// Whether this entry has error correction data
        /// </summary>
        public bool HasErrorCorrection { get; set; }

        /// <summary>
        /// The part index if this entry is split across multiple parts
        /// </summary>
        public int PartIndex { get; set; }

        /// <summary>
        /// The total parts if this entry is split
        /// </summary>
        public int TotalParts { get; set; } = 1;

        /// <summary>
        /// Custom internal flags
        /// </summary>
        public int Flags { get; set; }

        /// <summary>
        /// Creates a new extended entry from a basic entry
        /// </summary>
        /// <param name="entry">The base entry to extend</param>
        /// <returns>An extended entry</returns>
        public static FragileArchiveEntryExtended FromEntry(FragileArchiveEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (entry is FragileArchiveEntryExtended extendedEntry)
            {
                return extendedEntry;
            }

            return new FragileArchiveEntryExtended
            {
                Path = entry.Path,
                Size = entry.Size,
                CompressedSize = entry.CompressedSize,
                LastModified = entry.LastModified,
                IsDirectory = entry.IsDirectory,
                Position = entry.Position,
                SourcePath = entry.SourcePath,
                Data = entry.Data,
                HeaderOffset = entry.HeaderOffset,
                PositionOffset = entry.PositionOffset
            };
        }

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
    }
}