using Fragile.Core;
using Fragile.Core.Metadata;
using Fragile.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Fragile.Implementations;

/// <summary>
/// Concrete implementation of <see cref="IReadableArchive"/> representing an opened archive.
/// </summary>
internal class ReadableArchive : IReadableArchive
{
    private readonly Stream? _archiveStream;
    private readonly bool _leaveOpen;
    private readonly List<ArchiveEntry> _entries;
    private bool _disposed = false;

    // Potentially also hold a reference to the format handler or manager that created it
    // private readonly IFragileFormatReader _formatReader;

    /// <summary>
    /// Gets the collection of entries (files and directories) contained within the archive.
    /// </summary>
    public IReadOnlyCollection<ArchiveEntry> Entries => _entries.AsReadOnly();

    /// <summary>
    /// Gets the archive-level metadata read from the archive.
    /// </summary>
    public ArchiveMetadata? Metadata { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadableArchive"/> class.
    /// Typically created by an <see cref="IArchiveManager"/> implementation.
    /// </summary>
    /// <param name="archiveStream">The underlying stream of the archive. Null if not stream-based.</param>
    /// <param name="entries">The list of entries read from the archive.</param>
    /// <param name="metadata">The archive-level metadata.</param>
    /// <param name="leaveOpen">Indicates whether to leave the stream open upon disposal.</param>
    internal ReadableArchive(Stream? archiveStream, List<ArchiveEntry> entries, ArchiveMetadata? metadata, bool leaveOpen)
    {
        _archiveStream = archiveStream;
        _entries = entries ?? throw new ArgumentNullException(nameof(entries));
        Metadata = metadata;
        _leaveOpen = leaveOpen;

        // Associate the archive with each entry (if ArchiveEntry has the property)
        // foreach (var entry in _entries) { entry.Archive = this; }
    }

    /// <summary>
    /// Retrieves a specific entry by its full path within the archive.
    /// Path comparison is case-insensitive, uses '/' as separator.
    /// </summary>
    /// <param name="entryPath">The full path of the entry to retrieve.</param>
    /// <returns>The <see cref="ArchiveEntry"/> if found; otherwise, null.</returns>
    public ArchiveEntry? GetEntry(string entryPath)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ReadableArchive));
        
        // Normalize path for comparison
        string normalizedPath = entryPath.Replace('\\', '/').Trim();
        // Ensure consistent trailing slash for directories if DirectoryArchiveEntry stores them that way
        // bool mightBeDir = !Path.HasExtension(normalizedPath) || normalizedPath.EndsWith("/"); 
        // if (mightBeDir && !normalizedPath.EndsWith("/")) normalizedPath += "/";
        
        // Case-insensitive search is often expected for archive entries
        return _entries.FirstOrDefault(e => string.Equals(e.FullPath, normalizedPath, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(false); // Dispose managed resources synchronously
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    { 
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources
                if (_archiveStream != null && !_leaveOpen)
                {
                    _archiveStream.Dispose();
                }
                // Clear entries? Maybe not necessary if they don't hold resources directly.
                // _entries.Clear(); 
            }
            _disposed = true;
        }
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_archiveStream != null && !_leaveOpen)
        {
            await _archiveStream.DisposeAsync().ConfigureAwait(false);
        }
    }

    // Finalizer (just in case)
    ~ReadableArchive()
    {
        Dispose(false);
    }
} 