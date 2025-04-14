using Fragile.Core.Options;
using Fragile.Interfaces.Providers;

namespace Fragile.Implementations.Providers.ErrorCorrection;

/// <summary>
/// Placeholder implementation for error correction.
/// Throws NotImplementedException for all operations.
/// </summary>
internal class NotImplementedErrorCorrectionProvider : IErrorCorrectionProvider
{
    private const string ErrorMessage = "Error correction functionality (e.g., Reed-Solomon) is not implemented in this version.";

    public Task AddErrorCorrectionAsync(Stream sourceData, Stream targetStream, ErrorCorrectionOptions options, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(ErrorMessage);
    }

    public Task<bool> VerifyAndRepairAsync(Stream sourceStream, Stream targetData, ErrorCorrectionOptions options, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(ErrorMessage);
    }

    public void AddErrorCorrection(Stream sourceData, Stream targetStream, ErrorCorrectionOptions options)
    {
        throw new NotImplementedException(ErrorMessage);
    }

    public bool VerifyAndRepair(Stream sourceStream, Stream targetData, ErrorCorrectionOptions options)
    {
        throw new NotImplementedException(ErrorMessage);
    }
}