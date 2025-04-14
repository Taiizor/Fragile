using System;

namespace Fragile.Integrity
{
    /// <summary>
    /// Defines the checksum algorithms supported by the Fragile library for data integrity verification.
    /// </summary>
    public enum ChecksumAlgorithm
    {
        /// <summary>
        /// CRC32 checksum algorithm.
        /// </summary>
        CRC32,

        /// <summary>
        /// MD5 hash algorithm.
        /// </summary>
        MD5,

        /// <summary>
        /// SHA-1 hash algorithm.
        /// </summary>
        SHA1,

        /// <summary>
        /// SHA-256 hash algorithm.
        /// </summary>
        SHA256,

        /// <summary>
        /// SHA-512 hash algorithm.
        /// </summary>
        SHA512
    }

    /// <summary>
    /// Configuration options for data integrity and error correction operations.
    /// </summary>
    public class DataIntegrityOptions
    {
        /// <summary>
        /// Gets or sets the checksum algorithm to use for data integrity verification.
        /// </summary>
        public ChecksumAlgorithm ChecksumAlgorithm { get; set; } = ChecksumAlgorithm.SHA256;

        /// <summary>
        /// Gets or sets a value indicating whether to use per-file checksum verification.
        /// </summary>
        public bool UsePerFileChecksum { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to enable Reed-Solomon error correction.
        /// </summary>
        public bool EnableErrorCorrection { get; set; } = true;

        /// <summary>
        /// Gets or sets the error correction level as a percentage of archive size.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than 0 or greater than 100.</exception>
        public int ErrorCorrectionLevel
        {
            get => _errorCorrectionLevel;
            set
            {
                if (value < 0 || value > 100)
                    throw new ArgumentOutOfRangeException(nameof(ErrorCorrectionLevel), "Error correction level must be between 0 and 100.");
                _errorCorrectionLevel = value;
            }
        }

        private int _errorCorrectionLevel = 5;

        /// <summary>
        /// Gets or sets a value indicating whether to use per-file error correction settings.
        /// </summary>
        public bool UsePerFileErrorCorrection { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to enable automatic error detection and repair.
        /// </summary>
        public bool EnableAutoRepair { get; set; } = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="DataIntegrityOptions"/> class with default values.
        /// </summary>
        public DataIntegrityOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataIntegrityOptions"/> class with the specified checksum algorithm.
        /// </summary>
        /// <param name="checksumAlgorithm">The checksum algorithm to use for data integrity verification.</param>
        public DataIntegrityOptions(ChecksumAlgorithm checksumAlgorithm)
        {
            ChecksumAlgorithm = checksumAlgorithm;
        }
    }
} 