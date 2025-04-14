using Fragile.Core.Format;
using Fragile.Core.Metadata;

namespace Fragile.Core;

/// <summary>
/// Represents a single entry (file or directory) within a Fragile archive.
/// </summary>
public abstract class ArchiveEntry // Abstract base class
{
    /// <summary>
    /// Gets the full path of the entry within the archive.
    /// Directory separators are normalized to '/'.
    /// </summary>
    public string FullPath { get; protected set; } = string.Empty;

    /// <summary>
    /// Gets the name of the entry (the last part of the FullPath).
    /// </summary>
    public string Name => Path.GetFileName(FullPath.TrimEnd('/'));

    /// <summary>
    /// Gets the uncompressed size of the entry in bytes.
    /// For directories, this is typically 0.
    /// </summary>
    public long UncompressedSize { get; internal set; } // 'internal set' as it's determined during processing

    /// <summary>
    /// Gets the compressed size of the entry in bytes as stored in the archive.
    /// For directories, this is typically 0.
    /// </summary>
    public long CompressedSize { get; internal set; }

    /// <summary>
    /// Gets the file-specific metadata associated with this entry.
    /// Will be null for directory entries or if metadata storage was disabled.
    /// </summary>
    public FileMetadata? Metadata { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether this entry represents a directory.
    /// </summary>
    public abstract bool IsDirectory { get; }

    /// <summary>
    /// Gets the archive instance this entry belongs to.
    /// </summary>
    // public FragileArchive Archive { get; internal set; } // Reference back to the parent archive

    /// <summary>
    /// Extracts this entry to the specified stream.
    /// </summary>
    /// <param name="destination">The stream to write the entry's content to.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public abstract Task ExtractToStreamAsync(Stream destination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts this entry to the specified stream.
    /// </summary>
    /// <param name="destination">The stream to write the entry's content to.</param>
    public abstract void ExtractToStream(Stream destination);

    /// <summary>
    /// Extracts this entry to a file on the filesystem.
    /// </summary>
    /// <param name="destinationFileName">The path of the file to create.</param>
    /// <param name="overwrite">True to overwrite an existing file; otherwise, false.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public abstract Task ExtractToFileAsync(string destinationFileName, bool overwrite = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts this entry to a file on the filesystem.
    /// </summary>
    /// <param name="destinationFileName">The path of the file to create.</param>
    /// <param name="overwrite">True to overwrite an existing file; otherwise, false.</param>
    public abstract void ExtractToFile(string destinationFileName, bool overwrite = false);

    // Potential future methods:
    // public Stream Open();
    // public Task<Stream> OpenAsync(CancellationToken cancellationToken = default);
    // public void Delete(); // If modifying archives is supported

    protected ArchiveEntry(string fullPath)
    {
        // Normalize path separators
        FullPath = fullPath.Replace('\\', '/').Trim();
        if (string.IsNullOrWhiteSpace(FullPath))
        {
            throw new ArgumentException("Entry path cannot be empty.", nameof(fullPath));
        }
    }
}

/// <summary>
/// Represents a file entry within a Fragile archive.
/// </summary>
public class FileArchiveEntry : ArchiveEntry
{
    public override bool IsDirectory => false;

    /// <summary>
    /// Gets the starting position (offset) of the entry's compressed data within the archive stream.
    /// </summary>
    internal long DataOffset { get; set; }

    /// <summary>
    /// Gets the starting position (offset) of the entry's FileMetadata block within the archive stream.
    /// Value is 0 if the entry has no metadata.
    /// </summary>
    internal long MetadataOffset { get; set; }

    /// <summary>
    /// Gets the length of the entry's FileMetadata block in bytes.
    /// Value is 0 if the entry has no metadata.
    /// </summary>
    internal long MetadataLength { get; set; }

    /// <summary>
    /// Gets the flags associated with this entry, read from the entry header.
    /// </summary>
    internal FormatConstants.EntryHeaderFlags Flags { get; set; }

    internal FileArchiveEntry(string fullPath) : base(fullPath)
    {
        if (fullPath.EndsWith("/"))
        {
            throw new ArgumentException("File entry path cannot end with a separator.", nameof(fullPath));
        }
    }

    // Implementation of abstract methods would go in the actual archive format reader/writer
    public override void ExtractToFile(string destinationFileName, bool overwrite = false)
    {
        throw new NotImplementedException("Extraction logic depends on the specific archive implementation.");
    }

    public override Task ExtractToFileAsync(string destinationFileName, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Extraction logic depends on the specific archive implementation.");
    }

    public override void ExtractToStream(Stream destination)
    {
        throw new NotImplementedException("Extraction logic depends on the specific archive implementation.");
    }

    public override Task ExtractToStreamAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Extraction logic depends on the specific archive implementation.");
    }
}

/// <summary>
/// Represents a directory entry within a Fragile archive.
/// </summary>
public class DirectoryArchiveEntry : ArchiveEntry
{
    public override bool IsDirectory => true;

    internal DirectoryArchiveEntry(string fullPath) : base(fullPath.EndsWith("/") ? fullPath : fullPath + "/")
    {
        // Directory paths should conceptually end with a separator internally
        UncompressedSize = 0;
        CompressedSize = 0;
        Metadata = null; // Directories typically don't have the same metadata fields as files
    }

    // Extraction for directories usually means creating the directory structure on the filesystem.
    public override void ExtractToFile(string destinationFileName, bool overwrite = false)
    {
        // This might map to Directory.CreateDirectory(destinationFileName)
        throw new NotImplementedException("Directory extraction logic depends on the specific archive implementation.");
    }

    public override Task ExtractToFileAsync(string destinationFileName, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        // This might map to Directory.CreateDirectory(destinationFileName)
        throw new NotImplementedException("Directory extraction logic depends on the specific archive implementation.");
    }

    public override void ExtractToStream(Stream destination)
    {
        // Extracting a directory to a stream doesn't typically make sense.
        throw new InvalidOperationException("Cannot extract a directory entry to a stream.");
    }

    public override Task ExtractToStreamAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        // Extracting a directory to a stream doesn't typically make sense.
        throw new InvalidOperationException("Cannot extract a directory entry to a stream.");
    }
}