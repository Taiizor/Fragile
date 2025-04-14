using Fragile.Core.Enums;

namespace Fragile.Core.Options;

/// <summary>
/// Provides configuration options for data integrity verification using checksums.
/// </summary>
public class ChecksumOptions
{
    /// <summary>
    /// Gets or sets the checksum algorithm to use for verifying file integrity.
    /// Defaults to <see cref="ChecksumAlgorithm.Crc32"/>.
    /// Set to <see cref="ChecksumAlgorithm.None"/> to disable checksum verification.
    /// </summary>
    public ChecksumAlgorithm Algorithm { get; set; } = ChecksumAlgorithm.Crc32;

    /// <summary>
    /// Gets or sets a value indicating whether checksums should be verified during extraction.
    /// Defaults to true.
    /// </summary>
    /// <remarks>
    /// Disabling verification can speed up extraction but increases the risk of using corrupted data.
    /// </remarks>
    public bool VerifyOnExtract { get; set; } = true;
}