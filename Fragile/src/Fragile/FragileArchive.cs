using Fragile.Core.Events;
using Fragile.Core.Options;
using Fragile.Implementations;
using Fragile.Interfaces;

namespace Fragile;

/// <summary>
/// Static entry point for interacting with Fragile archives (.frgl).
/// Provides high-level methods for creating, extracting, opening, and verifying archives.
/// </summary>
public static class FragileArchive
{
    // Lazily initialize the manager instance
    private static readonly Lazy<IArchiveManager> _manager = new(() => new ArchiveManager());
    private static IArchiveManager Manager => _manager.Value;

    /// <summary>
    /// Creates a new archive file from the contents of a source directory asynchronously.
    /// </summary>
    /// <param name="sourceDirectoryPath">The path to the directory whose contents will be archived.</param>
    /// <param name="archiveFilePath">The path where the new archive file (.frgl) will be created.</param>
    /// <param name="options">Options for configuring compression, encryption, metadata, etc. (Optional)</param>
    /// <param name="progress">Provider for progress updates (optional).</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests (optional).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task CreateFromDirectoryAsync(
        string sourceDirectoryPath,
        string archiveFilePath,
        ArchiveOptions? options = null,
        IProgress<ProgressEventArgs>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArchiveOptions archiveOptions = options ?? new ArchiveOptions(); // Use default options if null
        return Manager.CreateFromDirectoryAsync(sourceDirectoryPath, archiveFilePath, archiveOptions, progress, cancellationToken);
    }

    /// <summary>
    /// Creates a new archive file from the contents of a source directory.
    /// </summary>
    /// <param name="sourceDirectoryPath">The path to the directory whose contents will be archived.</param>
    /// <param name="archiveFilePath">The path where the new archive file (.frgl) will be created.</param>
    /// <param name="options">Options for configuring compression, encryption, metadata, etc. (Optional)</param>
    /// <param name="progress">Provider for progress updates (optional).</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests (optional).</param>
    public static void CreateFromDirectory(
        string sourceDirectoryPath,
        string archiveFilePath,
        ArchiveOptions? options = null,
        IProgress<ProgressEventArgs>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArchiveOptions archiveOptions = options ?? new ArchiveOptions();
        Manager.CreateFromDirectory(sourceDirectoryPath, archiveFilePath, archiveOptions, progress, cancellationToken);
    }

    /// <summary>
    /// Extracts all entries from an archive file to a destination directory asynchronously.
    /// </summary>
    /// <param name="archiveFilePath">The path to the archive file (.frgl) to extract.</param>
    /// <param name="destinationDirectoryPath">The path to the directory where entries will be extracted.</param>
    /// <param name="options">Options for handling extraction (e.g., password for decryption). (Optional)</param>
    /// <param name="progress">Provider for progress updates (optional).</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests (optional).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task ExtractToDirectoryAsync(
        string archiveFilePath,
        string destinationDirectoryPath,
        ArchiveOptions? options = null,
        IProgress<ProgressEventArgs>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArchiveOptions archiveOptions = options ?? new ArchiveOptions();
        return Manager.ExtractToDirectoryAsync(archiveFilePath, destinationDirectoryPath, archiveOptions, progress, cancellationToken);
    }

    /// <summary>
    /// Extracts all entries from an archive file to a destination directory.
    /// </summary>
    /// <param name="archiveFilePath">The path to the archive file (.frgl) to extract.</param>
    /// <param name="destinationDirectoryPath">The path to the directory where entries will be extracted.</param>
    /// <param name="options">Options for handling extraction (e.g., password for decryption). (Optional)</param>
    /// <param name="progress">Provider for progress updates (optional).</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests (optional).</param>
    public static void ExtractToDirectory(
        string archiveFilePath,
        string destinationDirectoryPath,
        ArchiveOptions? options = null,
        IProgress<ProgressEventArgs>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArchiveOptions archiveOptions = options ?? new ArchiveOptions();
        Manager.ExtractToDirectory(archiveFilePath, destinationDirectoryPath, archiveOptions, progress, cancellationToken);
    }

    /// <summary>
    /// Opens an archive file for reading its entries asynchronously.
    /// </summary>
    /// <param name="archiveFilePath">The path to the archive file (.frgl).</param>
    /// <param name="options">Options for opening (e.g., password for encrypted headers). (Optional)</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests (optional).</param>
    /// <returns>A task representing the asynchronous operation, yielding a readable archive object.</returns>
    /// <remarks>The returned IReadableArchive should be disposed after use (e.g., using `await using`).</remarks>
    public static Task<IReadableArchive> OpenReadAsync(
        string archiveFilePath,
        ArchiveOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArchiveOptions archiveOptions = options ?? new ArchiveOptions();
        return Manager.OpenReadAsync(archiveFilePath, archiveOptions, cancellationToken);
    }

    /// <summary>
    /// Opens an archive file for reading its entries.
    /// </summary>
    /// <param name="archiveFilePath">The path to the archive file (.frgl).</param>
    /// <param name="options">Options for opening (e.g., password for encrypted headers). (Optional)</param>
    /// <returns>A readable archive object.</returns>
    /// <remarks>The returned IReadableArchive should be disposed after use (e.g., using `using`).</remarks>
    public static IReadableArchive OpenRead(
        string archiveFilePath,
        ArchiveOptions? options = null)
    {
        ArchiveOptions archiveOptions = options ?? new ArchiveOptions();
        return Manager.OpenRead(archiveFilePath, archiveOptions);
    }

    /// <summary>
    /// Opens an archive from a stream for reading its entries asynchronously.
    /// </summary>
    /// <param name="archiveStream">The stream containing the archive data. The stream must be readable and seekable.</param>
    /// <param name="options">Options for opening (e.g., password for encrypted headers). (Optional)</param>
    /// <param name="leaveOpen">True to leave the stream open after the archive object is disposed; otherwise, false. Defaults to false.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests (optional).</param>
    /// <returns>A task representing the asynchronous operation, yielding a readable archive object.</returns>
    /// <remarks>The returned IReadableArchive should be disposed after use (e.g., using `await using`).</remarks>
    public static Task<IReadableArchive> OpenReadAsync(
        Stream archiveStream,
        ArchiveOptions? options = null,
        bool leaveOpen = false,
        CancellationToken cancellationToken = default)
    {
        ArchiveOptions archiveOptions = options ?? new ArchiveOptions();
        return Manager.OpenReadAsync(archiveStream, archiveOptions, leaveOpen, cancellationToken);
    }

    /// <summary>
    /// Opens an archive from a stream for reading its entries.
    /// </summary>
    /// <param name="archiveStream">The stream containing the archive data. The stream must be readable and seekable.</param>
    /// <param name="options">Options for opening (e.g., password for encrypted headers). (Optional)</param>
    /// <param name="leaveOpen">True to leave the stream open after the archive object is disposed; otherwise, false. Defaults to false.</param>
    /// <returns>A readable archive object.</returns>
    /// <remarks>The returned IReadableArchive should be disposed after use (e.g., using `using`).</remarks>
    public static IReadableArchive OpenRead(
        Stream archiveStream,
        ArchiveOptions? options = null,
        bool leaveOpen = false)
    {
        ArchiveOptions archiveOptions = options ?? new ArchiveOptions();
        return Manager.OpenRead(archiveStream, archiveOptions, leaveOpen);
    }

    /// <summary>
    /// Verifies the integrity of an archive file asynchronously.
    /// </summary>
    /// <param name="archiveFilePath">The path to the archive file (.frgl) to verify.</param>
    /// <param name="options">Options controlling the verification process. (Optional)</param>
    /// <param name="progress">Provider for progress updates (optional).</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests (optional).</param>
    /// <returns>A task representing the asynchronous operation, yielding true if verification succeeds, false otherwise.</returns>
    public static Task<bool> VerifyArchiveAsync(
        string archiveFilePath,
        ArchiveOptions? options = null,
        IProgress<ProgressEventArgs>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArchiveOptions archiveOptions = options ?? new ArchiveOptions();
        return Manager.VerifyArchiveAsync(archiveFilePath, archiveOptions, progress, cancellationToken);
    }

    /// <summary>
    /// Verifies the integrity of an archive file.
    /// </summary>
    /// <param name="archiveFilePath">The path to the archive file (.frgl) to verify.</param>
    /// <param name="options">Options controlling the verification process. (Optional)</param>
    /// <param name="progress">Provider for progress updates (optional).</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests (optional).</param>
    /// <returns>True if verification succeeds, false otherwise.</returns>
    public static bool VerifyArchive(
        string archiveFilePath,
        ArchiveOptions? options = null,
        IProgress<ProgressEventArgs>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArchiveOptions archiveOptions = options ?? new ArchiveOptions();
        return Manager.VerifyArchive(archiveFilePath, archiveOptions, progress, cancellationToken);
    }

    /// <summary>
    /// Allows subscribing to progress events globally for operations initiated through this static class.
    /// </summary>
    /// <remarks>
    /// Note: This event handler will be attached to the single static ArchiveManager instance.
    /// If you need separate progress tracking for concurrent operations, consider creating 
    /// separate IArchiveManager instances or using the IProgress parameter on individual methods.
    /// </remarks>
    public static event EventHandler<ProgressEventArgs>? GlobalProgressChanged
    { add => Manager.ProgressChanged += value; remove => Manager.ProgressChanged -= value;
    }
}