using Fragile.Core.Options;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Fragile.Interfaces.Providers;

/// <summary>
/// Defines the interface for a checksum algorithm provider.
/// Implementations calculate checksums/hashes for data streams.
/// </summary>
public interface IChecksumProvider
{
    /// <summary>
    /// Computes the checksum for the given stream asynchronously.
    /// </summary>
    /// <param name="source">The stream to compute the checksum for. The stream will be read but its position may be altered.</param>
    /// <param name="options">Options related to checksum calculation (primarily the algorithm type).</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation, yielding the computed checksum as a byte array.</returns>
    /// <remarks>
    /// The caller is responsible for resetting the stream's position if needed after the call.
    /// </remarks>
    Task<byte[]> ComputeChecksumAsync(Stream source, ChecksumOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the checksum for the given stream.
    /// </summary>
    /// <param name="source">The stream to compute the checksum for. The stream will be read but its position may be altered.</param>
    /// <param name="options">Options related to checksum calculation (primarily the algorithm type).</param>
    /// <returns>The computed checksum as a byte array.</returns>
    /// <remarks>
    /// The caller is responsible for resetting the stream's position if needed after the call.
    /// </remarks>
    byte[] ComputeChecksum(Stream source, ChecksumOptions options);

    /// <summary>
    /// Gets the expected length (in bytes) of the checksum produced by this provider's algorithm.
    /// </summary>
    int ChecksumLengthBytes { get; }
} 