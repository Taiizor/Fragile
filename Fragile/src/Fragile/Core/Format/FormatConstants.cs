namespace Fragile.Core.Format;

/// <summary>
/// Defines constants related to the Fragile archive (.frgl) file format.
/// </summary>
internal static class FormatConstants
{
    /// <summary>
    /// Magic bytes identifying a Fragile archive file (e.g., "FRGL").
    /// </summary>
    public static readonly byte[] MagicBytes = { (byte)'F', (byte)'R', (byte)'G', (byte)'L' };

    /// <summary>
    /// Current version of the Fragile archive format specification.
    /// </summary>
    public const ushort FormatVersionMajor = 0;
    public const ushort FormatVersionMinor = 1;

    /// <summary>
    /// Size of the main archive header excluding variable-length metadata.
    /// (Magic Bytes (4) + Version Major (2) + Version Minor (2) + Header Flags (8) + Metadata Length (8) ...)
    /// Placeholder - actual size depends on final header definition.
    /// </summary>
    public const int BaseArchiveHeaderSize = 24; // Example value

    /// <summary>
    /// Flags indicating features used in the archive header (e.g., encryption, central directory).
    /// </summary>
    [Flags]
    public enum ArchiveHeaderFlags : ulong
    {
        None = 0,
        HasCentralDirectory = 1 << 0, // Indicates a central directory exists at the end
        ArchiveMetadataEncrypted = 1 << 1, // Indicates the ArchiveMetadata block is encrypted
        SolidArchive = 1 << 2, // Indicates solid compression mode was used
        // ... other global flags
    }

    /// <summary>
    /// Flags indicating features used for a specific entry.
    /// </summary>
    [Flags]
    public enum EntryHeaderFlags : uint
    {
        None = 0,
        IsDirectory = 1 << 0,
        IsEncrypted = 1 << 1,
        HasMetadata = 1 << 2, // Indicates FileMetadata block follows header
        HasChecksum = 1 << 3, // Indicates checksum follows compressed data
        HasErrorCorrection = 1 << 4, // Indicates ECC data follows checksum/compressed data
        // ... other entry-specific flags
    }

    // Other constants like block sizes, default settings related to format can go here.
}