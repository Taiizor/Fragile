namespace Fragile.Core.Enums;

/// <summary>
/// Defines the level of compression to apply, balancing speed and compression ratio.
/// </summary>
public enum CompressionLevel
{
    /// <summary>
    /// Fastest compression speed, lowest compression ratio.
    /// </summary>
    Fastest = 0,

    /// <summary>
    /// Faster compression speed, lower compression ratio.
    /// </summary>
    Fast = 1,

    /// <summary>
    /// Balanced compression speed and ratio (default).
    /// </summary>
    Normal = 2,

    /// <summary>
    /// Slower compression speed, higher compression ratio.
    /// </summary>
    High = 3,

    /// <summary>
    /// Slowest compression speed, highest compression ratio.
    /// </summary>
    Ultra = 4
} 