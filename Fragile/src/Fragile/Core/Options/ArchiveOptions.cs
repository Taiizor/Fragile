using Fragile.Core.Metadata;

namespace Fragile.Core.Options;

/// <summary>
/// Represents the complete set of options for creating or manipulating a Fragile archive.
/// </summary>
public class ArchiveOptions
{
    /// <summary>
    /// Gets or sets the compression options.
    /// If null, default compression settings will be used.
    /// </summary>
    public CompressionOptions? Compression { get; set; } = new CompressionOptions(); // Default instance

    /// <summary>
    /// Gets or sets the encryption options.
    /// If null, no encryption will be applied by default.
    /// </summary>
    public EncryptionOptions? Encryption { get; set; } = new EncryptionOptions(); // Default instance

    /// <summary>
    /// Gets or sets the checksum options for data integrity.
    /// If null, default checksum settings will be used.
    /// </summary>
    public ChecksumOptions? Checksum { get; set; } = new ChecksumOptions(); // Default instance

    /// <summary>
    /// Gets or sets the error correction options.
    /// If null, no error correction will be applied by default.
    /// </summary>
    public ErrorCorrectionOptions? ErrorCorrection { get; set; } = new ErrorCorrectionOptions(); // Default instance

    /// <summary>
    /// Gets or sets the metadata to be associated with the archive itself.
    /// If null, minimal default metadata (like creation time) might be generated.
    /// </summary>
    public ArchiveMetadata? ArchiveMetadata { get; set; } = new ArchiveMetadata(); // Default instance

    // --- Advanced Options ---

    /// <summary>
    /// Gets or sets options related to splitting large archives into multiple parts.
    /// If null (default), archive splitting is disabled.
    /// </summary>
    // public ArchiveSplittingOptions? Splitting { get; set; } = null; // To be defined later

    /// <summary>
    /// Gets or sets options related to parallel processing during archive operations.
    /// This often overlaps with specific options like Compression.ThreadCount, but can provide global control.
    /// If null (default), the library determines parallelism based on specific feature options or system capabilities.
    /// </summary>
    // public ParallelProcessingOptions? ParallelProcessing { get; set; } = null; // To be defined later

    /// <summary>
    /// Gets or sets a value indicating whether to store file metadata (timestamps, attributes, etc.).
    /// Defaults to true.
    /// </summary>
    public bool StoreFileMetadata { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the specific Fragile archive signature and version
    /// should be verified when opening an existing archive.
    /// Defaults to true for security and compatibility.
    /// </summary>
    public bool VerifyArchiveSignature { get; set; } = true;

    /// <summary>
    /// Gets or sets the buffer size used for stream operations (copying, compressing, etc.).
    /// Defaults to a sensible value (e.g., 81920 bytes, similar to Stream.CopyTo default).
    /// </summary>
    public int StreamBufferSize { get; set; } = 81920;
}