namespace Fragile.Core.Events;

/// <summary>
/// Provides data for progress reporting events during archive operations.
/// </summary>
public class ProgressEventArgs : EventArgs
{
    /// <summary>
    /// Gets the total number of bytes expected to be processed.
    /// Might be null if the total size is unknown beforehand.
    /// </summary>
    public long? TotalBytes { get; }

    /// <summary>
    /// Gets the number of bytes processed so far.
    /// </summary>
    public long BytesProcessed { get; }

    /// <summary>
    /// Gets the progress percentage (0.0 to 100.0).
    /// Calculated based on BytesProcessed and TotalBytes.
    /// Returns null if TotalBytes is not available.
    /// </summary>
    public double? PercentageProcessed => TotalBytes.HasValue && TotalBytes.Value > 0
                                          ? (double)BytesProcessed / TotalBytes.Value * 100.0
                                          : null;

    /// <summary>
    /// Gets the name or path of the current file being processed, if applicable.
    /// </summary>
    public string? CurrentFile { get; }

    /// <summary>
    /// Gets a message describing the current stage or status of the operation.
    /// </summary>
    public string? StatusMessage { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressEventArgs"/> class.
    /// </summary>
    /// <param name="bytesProcessed">The number of bytes processed so far.</param>
    /// <param name="totalBytes">The total number of bytes expected (optional).</param>
    /// <param name="currentFile">The current file being processed (optional).</param>
    /// <param name="statusMessage">A status message (optional).</param>
    public ProgressEventArgs(long bytesProcessed, long? totalBytes = null, string? currentFile = null, string? statusMessage = null)
    {
        if (bytesProcessed < 0) 
            throw new ArgumentOutOfRangeException(nameof(bytesProcessed), "Bytes processed cannot be negative.");
        if (totalBytes.HasValue && totalBytes.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(totalBytes), "Total bytes cannot be negative.");
        if (totalBytes.HasValue && bytesProcessed > totalBytes.Value)
            throw new ArgumentOutOfRangeException(nameof(bytesProcessed), "Bytes processed cannot exceed total bytes.");
            
        BytesProcessed = bytesProcessed;
        TotalBytes = totalBytes;
        CurrentFile = currentFile;
        StatusMessage = statusMessage;
    }
} 