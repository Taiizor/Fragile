using Fragile.Compression;
using Fragile.Encryption;
using Fragile.Verification;

namespace Fragile.Models
{
    /// <summary>
    /// Configuration options for Fragile archive operations
    /// </summary>
    public class FragileOptions
    {
        /// <summary>
        /// Password for encrypting/decrypting the archive
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Split archive into multiple parts if it exceeds this size (in bytes)
        /// Set to 0 to disable splitting
        /// </summary>
        public long SplitSize { get; set; } = 0;

        /// <summary>
        /// Progress reporting callback
        /// </summary>
        public IProgress<double>? Progress { get; set; }

        /// <summary>
        /// Represents a string property that holds a default value of '.part'.
        /// It is used to specify a name for a split file.
        /// </summary>
        public string SplitName { get; set; } = ".part";

        /// <summary>
        /// Whether to include metadata in the archive
        /// </summary>
        public bool IncludeMetadata { get; set; } = true;

        /// <summary>
        /// Error correction level (as a percentage of archive size)
        /// </summary>
        public int ErrorCorrectionLevel { get; set; } = 5;

        /// <summary>
        /// Enable encryption for the archive
        /// </summary>
        public bool EnableEncryption { get; set; } = false;

        /// <summary>
        /// Whether to store files in solid archive mode
        /// </summary>
        public bool UseSolidCompression { get; set; } = false;

        /// <summary>
        /// Whether to use parallel processing for compression/decompression
        /// </summary>
        public bool UseParallelProcessing { get; set; } = true;

        /// <summary>
        /// Whether to enable error correction data
        /// </summary>
        public bool EnableErrorCorrection { get; set; } = false;

        /// <summary>
        /// Whether to calculate and store checksums for files
        /// </summary>
        public bool EnableChecksumVerification { get; set; } = true;

        /// <summary>
        /// Directory for storing temporary files and operations
        /// </summary>
        public string TempDirectory { get; set; } = Path.GetTempPath();

        /// <summary>
        /// Maximum number of threads to use for parallel operations
        /// </summary>
        public int MaxThreads { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// Compression level for balancing speed vs compression ratio
        /// </summary>
        public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Normal;

        /// <summary>
        /// Encryption method to use if encryption is enabled
        /// </summary>
        public EncryptionMethod EncryptionMethod { get; set; } = EncryptionMethod.AES256;

        /// <summary>
        /// Cancellation token to cancel archive operations
        /// </summary>
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        /// <summary>
        /// Checksum algorithm to use for file integrity verification
        /// </summary>
        public ChecksumAlgorithm ChecksumAlgorithm { get; set; } = ChecksumAlgorithm.CRC32;

        /// <summary>
        /// The compression algorithm to use
        /// </summary>
        public CompressionAlgorithm CompressionAlgorithm { get; set; } = CompressionAlgorithm.Store;
    }
}