namespace Fragile.Core.Enums;

/// <summary>
/// Specifies the level of error correction data to include in the archive.
/// </summary>
/// <remarks>
/// Higher levels provide more robustness against data corruption but increase archive size.
/// The percentage indicates the amount of redundant data added relative to the original data size.
/// </remarks>
public enum ErrorCorrectionLevel
{
    /// <summary>
    /// No error correction data is added.
    /// </summary>
    None = 0,

    /// <summary>
    /// Low level of error correction (e.g., ~5% redundancy).
    /// </summary>
    Low = 1,

    /// <summary>
    /// Medium level of error correction (e.g., ~15% redundancy).
    /// </summary>
    Medium = 2,

    /// <summary>
    /// High level of error correction (e.g., ~25% redundancy).
    /// </summary>
    High = 3,

    /// <summary>
    /// Maximum level of error correction (e.g., ~50% redundancy or more, implementation defined).
    /// </summary>
    Maximum = 4
} 