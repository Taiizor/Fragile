using Fragile.Core.Enums;

namespace Fragile.Core.Options;

/// <summary>
/// Provides configuration options for adding error correction data to the archive.
/// </summary>
public class ErrorCorrectionOptions
{
    /// <summary>
    /// Gets or sets the level of error correction codes to add.
    /// Higher levels increase robustness against corruption but also increase archive size.
    /// Defaults to <see cref="ErrorCorrectionLevel.None"/> (no error correction).
    /// </summary>
    public ErrorCorrectionLevel Level { get; set; } = ErrorCorrectionLevel.None;

    /// <summary>
    /// Gets or sets a value indicating whether error correction should be applied globally 
    /// to the entire archive or on a per-file basis.
    /// Applying globally might be more efficient for certain archive structures but less flexible.
    /// Defaults to true (per-file error correction).
    /// </summary>
    /// <remarks>
    /// The feasibility of global vs. per-file error correction depends on the specific 
    /// error correction algorithm (e.g., Reed-Solomon) and the archive format implementation.
    /// </remarks>
    public bool UsePerFileErrorCorrection { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the library should attempt to automatically 
    /// detect and repair errors using the available error correction data during extraction.
    /// Defaults to true.
    /// </summary>
    /// <remarks>
    /// Disabling automatic repair might be useful in specific scenarios where manual 
    /// intervention or analysis of the corruption is required.
    /// </remarks>
    public bool AttemptAutomaticRepair { get; set; } = true;

    // Potential future options:
    // public int? CustomRedundancyPercentage { get; set; } // Allow finer control than levels?
    // public ErrorCorrectionAlgorithm Algorithm { get; set; } // If more than Reed-Solomon is supported later.
} 