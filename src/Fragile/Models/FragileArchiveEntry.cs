using Fragile.Encryption;

namespace Fragile.Models
{
    /// <summary>
    /// Represents a file or directory entry in a Fragile archive
    /// </summary>
    public class FragileArchiveEntry
    {
        /// <summary>
        /// Original file size
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// File content (if stored in memory)
        /// </summary>
        public byte[]? Data { get; set; }

        /// <summary>
        /// Position inside archive
        /// </summary>
        public long Position { get; set; }

        /// <summary>
        /// Is this entry encrypted?
        /// </summary>
        public bool IsEncrypted { get; set; }

        /// <summary>
        /// Is this a directory?
        /// </summary>
        public bool IsDirectory { get; set; }

        /// <summary>
        /// File source (path on disk)
        /// </summary>
        public string? SourcePath { get; set; }

        /// <summary>
        /// Compressed size
        /// </summary>
        public long CompressedSize { get; set; }

        /// <summary>
        /// Internal use: Header offsets
        /// </summary>
        internal long HeaderOffset { get; set; }

        /// <summary>
        /// File last modified date
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Internal use: Data offsets
        /// </summary>
        internal long PositionOffset { get; set; }

        /// <summary>
        /// Path inside the archive
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Encryption method used for this entry
        /// </summary>
        public EncryptionMethod EncryptionMethod { get; set; } = EncryptionMethod.None;

        /// <summary>
        /// Calculates compression ratio
        /// </summary>
        public double CompressionRatio
        {
            get
            {
                if (Size == 0 || IsDirectory)
                {
                    return 0;
                }

                return 100.0 - ((double)CompressedSize / Size * 100.0);
            }
        }
    }
}