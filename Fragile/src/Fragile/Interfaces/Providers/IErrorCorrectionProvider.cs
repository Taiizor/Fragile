using Fragile.Core.Options;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Fragile.Interfaces.Providers;

/// <summary>
/// Defines the interface for an error correction code (ECC) provider.
/// Implementations handle adding redundancy for error detection/correction and attempting repairs.
/// </summary>
public interface IErrorCorrectionProvider
{
    /// <summary>
    /// Adds error correction codes (redundancy) to the source data stream, writing the result (data + ECC) to the target stream asynchronously.
    /// </summary>
    /// <param name="sourceData">The original data stream.</param>
    /// <param name="targetStream">The stream to write the data combined with ECC information to.</param>
    /// <param name="options">Error correction options, specifying the desired level/strength.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddErrorCorrectionAsync(Stream sourceData, Stream targetStream, ErrorCorrectionOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the source stream using its embedded error correction codes and attempts to repair it, writing the potentially corrected data to the target stream asynchronously.
    /// </summary>
    /// <param name="sourceStream">The stream containing data potentially protected by ECC.</param>
    /// <param name="targetData">The stream to write the verified (and potentially repaired) original data to.</param>
    /// <param name="options">Error correction options used during verification/repair.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation, yielding true if the data is valid or successfully repaired, false otherwise.</returns>
    Task<bool> VerifyAndRepairAsync(Stream sourceStream, Stream targetData, ErrorCorrectionOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds error correction codes (redundancy) to the source data stream, writing the result (data + ECC) to the target stream.
    /// </summary>
    /// <param name="sourceData">The original data stream.</param>
    /// <param name="targetStream">The stream to write the data combined with ECC information to.</param>
    /// <param name="options">Error correction options, specifying the desired level/strength.</param>
    void AddErrorCorrection(Stream sourceData, Stream targetStream, ErrorCorrectionOptions options);

    /// <summary>
    /// Verifies the source stream using its embedded error correction codes and attempts to repair it, writing the potentially corrected data to the target stream.
    /// </summary>
    /// <param name="sourceStream">The stream containing data potentially protected by ECC.</param>
    /// <param name="targetData">The stream to write the verified (and potentially repaired) original data to.</param>
    /// <param name="options">Error correction options used during verification/repair.</param>
    /// <returns>True if the data is valid or successfully repaired, false otherwise.</returns>
    bool VerifyAndRepair(Stream sourceStream, Stream targetData, ErrorCorrectionOptions options);
} 