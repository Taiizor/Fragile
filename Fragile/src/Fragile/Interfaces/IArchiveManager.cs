using Fragile.Core;
using Fragile.Core.Events;
using Fragile.Core.Metadata;
using Fragile.Core.Options;

namespace Fragile.Interfaces;

/// <summary>
/// Defines the core interface for managing Fragile archives (.frgl).
/// Provides methods for creating, opening, extracting, and manipulating archive files.
/// </summary>
public interface IArchiveManager
{
    /// <summary>
    /// Occurs when progress is made during a long-running archive operation.
    /// </summary>
    event EventHandler<ProgressEventArgs>? ProgressChanged;

    // --- Archive Creation --- 

    /// <summary>
    /// Creates a new archive file from the contents of a source directory asynchronously.
    /// </summary>
    /// <param name="sourceDirectoryPath">The path to the directory whose contents will be archived.</param>
    /// <param name="archiveFilePath">The path where the new archive file (.frgl) will be created.</param>
    /// <param name="options">Options for configuring compression, encryption, metadata, etc.</param>
    /// <param name="progress">Provider for progress updates (optional).</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests (optional).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CreateFromDirectoryAsync(string sourceDirectoryPath, string archiveFilePath, ArchiveOptions options, IProgress<ProgressEventArgs>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new archive file from the contents of a source directory.
    /// </summary>
    /// <param name="sourceDirectoryPath">The path to the directory whose contents will be archived.</param>
    /// <param name="archiveFilePath">The path where the new archive file (.frgl) will be created.</param>
    /// <param name="options">Options for configuring compression, encryption, metadata, etc.</param>
    /// <param name="progress">Provider for progress updates (optional).</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests (optional).</param>
    void CreateFromDirectory(string sourceDirectoryPath, string archiveFilePath, ArchiveOptions options, IProgress<ProgressEventArgs>? progress = null, CancellationToken cancellationToken = default);

    // --- Archive Extraction --- 

    /// <summary>
    /// Extracts all entries from an archive file to a destination directory asynchronously.
    /// </summary>
    /// <param name="archiveFilePath">The path to the archive file (.frgl) to extract.</param>
    /// <param name="destinationDirectoryPath">The path to the directory where entries will be extracted.</param>
    /// <param name="options">Options for handling extraction (e.g., password for decryption, verification settings).</param>
    /// <param name="progress">Provider for progress updates (optional).</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests (optional).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExtractToDirectoryAsync(string archiveFilePath, string destinationDirectoryPath, ArchiveOptions options, IProgress<ProgressEventArgs>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts all entries from an archive file to a destination directory.
    /// </summary>
    /// <param name="archiveFilePath">The path to the archive file (.frgl) to extract.</param>
    /// <param name="destinationDirectoryPath">The path to the directory where entries will be extracted.</param>
    /// <param name="options">Options for handling extraction (e.g., password for decryption, verification settings).</param>
    /// <param name="progress">Provider for progress updates (optional).</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests (optional).</param>
    void ExtractToDirectory(string archiveFilePath, string destinationDirectoryPath, ArchiveOptions options, IProgress<ProgressEventArgs>? progress = null, CancellationToken cancellationToken = default);

    // --- Archive Reading/Listing --- 

    /// <summary>
    /// Opens an archive file for reading its entries asynchronously.
    /// </summary>
    /// <param name="archiveFilePath">The path to the archive file (.frgl).</param>
    /// <param name="options">Options for opening (e.g., password for encrypted headers).</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests (optional).</param>
    /// <returns>A task representing the asynchronous operation, yielding a readable archive object.</returns>
    /// <remarks>The returned IReadableArchive should be disposed after use.</remarks>
    Task<IReadableArchive> OpenReadAsync(string archiveFilePath, ArchiveOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens an archive file for reading its entries.
    /// </summary>
    /// <param name="archiveFilePath">The path to the archive file (.frgl).</param>
    /// <param name="options">Options for opening (e.g., password for encrypted headers).</param>
    /// <returns>A readable archive object.</returns>
    /// <remarks>The returned IReadableArchive should be disposed after use.</remarks>
    IReadableArchive OpenRead(string archiveFilePath, ArchiveOptions options);

    /// <summary>
    /// Opens an archive from a stream for reading its entries asynchronously.
    /// </summary>
    /// <param name="archiveStream">The stream containing the archive data. The stream must be readable and seekable.</param>
    /// <param name="options">Options for opening (e.g., password for encrypted headers).</param>
    /// <param name="leaveOpen">True to leave the stream open after the archive object is disposed; otherwise, false.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests (optional).</param>
    /// <returns>A task representing the asynchronous operation, yielding a readable archive object.</returns>
    /// <remarks>The returned IReadableArchive should be disposed after use.</remarks>
    Task<IReadableArchive> OpenReadAsync(Stream archiveStream, ArchiveOptions options, bool leaveOpen = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens an archive from a stream for reading its entries.
    /// </summary>
    /// <param name="archiveStream">The stream containing the archive data. The stream must be readable and seekable.</param>
    /// <param name="options">Options for opening (e.g., password for encrypted headers).</param>
    /// <param name="leaveOpen">True to leave the stream open after the archive object is disposed; otherwise, false.</param>
    /// <returns>A readable archive object.</returns>
    /// <remarks>The returned IReadableArchive should be disposed after use.</remarks>
    IReadableArchive OpenRead(Stream archiveStream, ArchiveOptions options, bool leaveOpen = false);

    // --- Archive Verification (Optional Separate Method) ---

    /// <summary>
    /// Verifies the integrity of an archive file (e.g., checks signature, version, and optionally checksums/error correction data) asynchronously.
    /// </summary>
    /// <param name="archiveFilePath">The path to the archive file (.frgl) to verify.</param>
    /// <param name="options">Options controlling the verification process (e.g., which checks to perform).</param>
    /// <param name="progress">Provider for progress updates (optional).</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests (optional).</param>
    /// <returns>A task representing the asynchronous operation, yielding true if verification succeeds, false otherwise.</returns>
    Task<bool> VerifyArchiveAsync(string archiveFilePath, ArchiveOptions options, IProgress<ProgressEventArgs>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the integrity of an archive file (e.g., checks signature, version, and optionally checksums/error correction data).
    /// </summary>
    /// <param name="archiveFilePath">The path to the archive file (.frgl) to verify.</param>
    /// <param name="options">Options controlling the verification process (e.g., which checks to perform).</param>
    /// <param name="progress">Provider for progress updates (optional).</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests (optional).</param>
    /// <returns>True if verification succeeds, false otherwise.</returns>
    bool VerifyArchive(string archiveFilePath, ArchiveOptions options, IProgress<ProgressEventArgs>? progress = null, CancellationToken cancellationToken = default);

    // Add methods for Update/Add operations if supported by the design
    // Task AddFileAsync(...)
    // void AddFile(...)
    // Task AddDirectoryAsync(...)
    // void AddDirectory(...)
    // IWritableArchive OpenWrite(...) / OpenUpdate(...)
}

/// <summary>
/// Represents an archive opened in read-only mode, allowing access to its entries.
/// </summary>
public interface IReadableArchive : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the collection of entries (files and directories) contained within the archive.
    /// </summary>
    IReadOnlyCollection<ArchiveEntry> Entries { get; }

    /// <summary>
    /// Gets the archive-level metadata read from the archive.
    /// </summary>
    ArchiveMetadata? Metadata { get; }

    /// <summary>
    /// Retrieves a specific entry by its full path within the archive.
    /// </summary>
    /// <param name="entryPath">The full path of the entry to retrieve (case-insensitive, uses '/' as separator).</param>
    /// <returns>The <see cref="ArchiveEntry"/> if found; otherwise, null.</returns>
    ArchiveEntry? GetEntry(string entryPath);

    /// <summary>
    /// Extracts the content of the specified file entry to the given stream asynchronously.
    /// </summary>
    /// <param name="entry">The file entry to extract. Must be a FileArchiveEntry.</param>
    /// <param name="destination">The stream to write the entry's content to.</param>
    /// <param name="options">Options potentially needed for extraction (e.g., password for decryption, checksum verification). If null, options used to open the archive might be assumed, or defaults used.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous extraction operation.</returns>
    /// <exception cref="ArgumentException">Thrown if the entry is not a FileArchiveEntry.</exception>
    /// <exception cref="InvalidOperationException">Thrown if extraction requires options (like a password) that are not provided.</exception>
    Task ExtractEntryToStreamAsync(ArchiveEntry entry, Stream destination, ArchiveOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads and deserializes the FileMetadata for the specified file entry asynchronously, 
    /// using the offset stored in the entry.
    /// </summary>
    /// <param name="entry">The file entry whose metadata is to be read. Must be a FileArchiveEntry.</param>
    /// <param name="options">Options potentially needed for reading metadata (e.g., password if metadata is encrypted). If null, options used to open the archive might be assumed.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation, yielding the deserialized FileMetadata, or null if the entry has no metadata or an error occurs.</returns>
    /// <exception cref="ArgumentException">Thrown if the entry is not a FileArchiveEntry.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the underlying stream is unavailable or metadata requires options (like a password) that are not provided.</exception>
    Task<FileMetadata?> ReadEntryMetadataAsync(ArchiveEntry entry, ArchiveOptions? options = null, CancellationToken cancellationToken = default);

    // Potentially add methods for seeking entries if needed
}

// Interface for writable/updateable archive could be defined here if needed
// public interface IWritableArchive : IReadableArchive
// {
//     void AddEntry(string sourceFileName, string entryPath, FileMetadata? metadata = null);
//     Task AddEntryAsync(string sourceFileName, string entryPath, FileMetadata? metadata = null, CancellationToken cancellationToken = default);
//     void AddDirectoryEntry(string entryPath);
//     void DeleteEntry(ArchiveEntry entry);
//     // ... other modification methods
// } 