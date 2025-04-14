using Fragile.Core;
using Fragile.Core.Enums;
using Fragile.Core.Metadata;
using Fragile.Core.Options;
using Fragile.Implementations.Providers.Encryption;
using Fragile.Interfaces;
using Fragile.Interfaces.Providers;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Fragile.Implementations;

/// <summary>
/// Concrete implementation of <see cref="IReadableArchive"/> representing an opened archive.
/// </summary>
internal class ReadableArchive : IReadableArchive
{
    private readonly Stream? _archiveStream;
    private readonly bool _leaveOpen;
    private readonly List<ArchiveEntry> _entries;
    private readonly ArchiveOptions _options;
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
    /// <param name="options">The options used to open the archive.</param>
    internal ReadableArchive(Stream? archiveStream, List<ArchiveEntry> entries, ArchiveMetadata? metadata, bool leaveOpen, ArchiveOptions options)
    {
        _archiveStream = archiveStream;
        _entries = entries ?? throw new ArgumentNullException(nameof(entries));
        Metadata = metadata;
        _leaveOpen = leaveOpen;
        _options = options ?? throw new ArgumentNullException(nameof(options));

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
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ReadableArchive));
        }

        // Normalize path for comparison
        string normalizedPath = entryPath.Replace('\\', '/').Trim();
        // Ensure consistent trailing slash for directories if DirectoryArchiveEntry stores them that way
        // bool mightBeDir = !Path.HasExtension(normalizedPath) || normalizedPath.EndsWith("/"); 
        // if (mightBeDir && !normalizedPath.EndsWith("/")) normalizedPath += "/";

        // Case-insensitive search is often expected for archive entries
        return _entries.FirstOrDefault(e => string.Equals(e.FullPath, normalizedPath, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Extracts the content of the specified file entry to the given stream asynchronously.
    /// Uses the offsets stored in the entry and applies decryption/decompression based on the options provided during OpenRead or optionally overridden here.
    /// </summary>
    public async Task ExtractEntryToStreamAsync(ArchiveEntry entry, Stream destination, ArchiveOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ReadableArchive));
        }

        if (entry is null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        if (destination is null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        if (!destination.CanWrite)
        {
            throw new ArgumentException("Destination stream must be writable.", nameof(destination));
        }

        if (_archiveStream is null || !_archiveStream.CanRead || !_archiveStream.CanSeek)
        {
            throw new InvalidOperationException("The underlying archive stream is not available or not suitable for reading entries.");
        }

        if (entry is not FileArchiveEntry fileEntry)
        {
            throw new ArgumentException("Entry must be a FileArchiveEntry to extract data.", nameof(entry));
        }

        // Use provided options or fall back to the options used when opening the archive
        ArchiveOptions effectiveOptions = options ?? _options;
        // Ensure non-null defaults for sub-options
        effectiveOptions.Compression ??= new CompressionOptions();
        effectiveOptions.Encryption ??= new EncryptionOptions();
        effectiveOptions.Checksum ??= new ChecksumOptions(); // Although checksum isn't verified here yet

        int bufferSize = effectiveOptions.StreamBufferSize;
        List<IDisposable> disposableProviders = new();

        try
        {
            // Determine required providers based on effective options
            // TODO: Ideally, this should use flags stored *in* the entry header, not just options.
            //       For now, we assume options reflect the state of the entry.
            bool isEncrypted = effectiveOptions.Encryption.Algorithm != EncryptionAlgorithm.None;
            bool isCompressed = effectiveOptions.Compression.Algorithm != CompressionAlgorithm.Store;

            IEncryptionProvider? decryptionProvider = null;
            ICompressionProvider? decompressionProvider = null;

            if (isEncrypted)
            {
                if (string.IsNullOrEmpty(effectiveOptions.Encryption.Password))
                {
                    throw new InvalidOperationException($"Password is required to extract potentially encrypted entry: {entry.FullPath}");
                }

                decryptionProvider = ProviderFactory.GetEncryptionProvider(effectiveOptions.Encryption.Algorithm, bufferSize);
            }
            if (isCompressed)
            {
                decompressionProvider = ProviderFactory.GetCompressionProvider(effectiveOptions.Compression.Algorithm, bufferSize);
            }

            // Create a stream that reads only the entry's data section from the main archive stream
            // Ensure the main stream position is set correctly before creating SubStream
            _archiveStream.Position = fileEntry.DataOffset;
            Stream streamToReadFrom = new SubStream(_archiveStream, fileEntry.DataOffset, fileEntry.CompressedSize, true);
            List<IDisposable> processingStreams = new() { streamToReadFrom }; // Track streams to dispose

            try
            {
                // 1. Decryption Layer (if enabled)
                if (decryptionProvider != null)
                {
                    // Assume Salt/IV are at the beginning of the entry data
                    byte[] salt = await ReadBytesAsync(streamToReadFrom, AesEncryptionProviderBase.DefaultSaltSizeBytes, cancellationToken).ConfigureAwait(false);
                    byte[] iv = await ReadBytesAsync(streamToReadFrom, AesEncryptionProviderBase.DefaultIvSizeBytes, cancellationToken).ConfigureAwait(false);
                    byte[] key = DeriveKeyFromPassword(effectiveOptions.Encryption.Password!, salt, decryptionProvider is Aes256EncryptionProvider ? 256 : 128);

                    using Aes aes = CreateAesInstance(decryptionProvider is Aes256EncryptionProvider ? 256 : 128);
                    aes.Key = key;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    CryptoStream cryptoStream = new(streamToReadFrom, aes.CreateDecryptor(), CryptoStreamMode.Read, true);
                    processingStreams.Add(cryptoStream);
                    streamToReadFrom = cryptoStream; // Next layer reads from cryptoStream
                }

                // 2. Decompression Layer (if enabled)
                if (decompressionProvider != null)
                {
                    DeflateStream decompressionStream = new(streamToReadFrom, CompressionMode.Decompress, true);
                    processingStreams.Add(decompressionStream);
                    streamToReadFrom = decompressionStream; // Next layer reads from decompressionStream
                }

                // 3. Copy final processed data to Destination
                await streamToReadFrom.CopyToAsync(destination, bufferSize, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // Dispose wrapper streams in reverse order
                processingStreams.Reverse();
                foreach (IDisposable stream in processingStreams)
                {
                    if (stream is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        stream.Dispose();
                    }
                }
            }

            // TODO: Implement Checksum Verification if needed.
            // This would involve reading the checksum data after the compressed data
            // (using fileEntry.DataOffset + fileEntry.CompressedSize as the starting point)
            // and comparing it against a checksum computed from the *destination* stream.
            // This requires the destination stream to be readable and seekable, or buffering.

        }
        finally
        {
            // Dispose any disposable providers created (like DotNetChecksumProvider if used)
            foreach (IDisposable provider in disposableProviders)
            {
                provider.Dispose();
            }
        }
    }

    /// <summary>
    /// Reads and deserializes the FileMetadata for the specified file entry asynchronously, 
    /// using the offset stored in the entry.
    /// </summary>
    public async Task<FileMetadata?> ReadEntryMetadataAsync(ArchiveEntry entry, ArchiveOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ReadableArchive));
        }

        if (entry is null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        if (_archiveStream is null || !_archiveStream.CanRead || !_archiveStream.CanSeek)
        {
            throw new InvalidOperationException("The underlying archive stream is not available or not suitable for reading metadata.");
        }

        if (entry is not FileArchiveEntry fileEntry)
        {
            throw new ArgumentException("Entry must be a FileArchiveEntry to read metadata.", nameof(entry));
        }

        // Check if metadata actually exists based on offset and length
        if (fileEntry.MetadataOffset <= 0 || fileEntry.MetadataLength <= 0)
        {
            return null; // No metadata stored for this entry
        }

        // Use provided options or fall back to the options used when opening the archive
        ArchiveOptions effectiveOptions = options ?? _options;
        effectiveOptions.Encryption ??= new EncryptionOptions(); // Needed if metadata can be encrypted

        try
        {
            // Seek to the metadata position
            _archiveStream.Position = fileEntry.MetadataOffset;

            // Read the raw metadata bytes
            byte[] metadataBytes = await ReadBytesAsync(_archiveStream, (int)fileEntry.MetadataLength, cancellationToken).ConfigureAwait(false);

            // TODO: Implement metadata decryption if the format supports it.
            // Check a flag on the entry or archive options to see if metadata is encrypted.
            // If encrypted, use an IEncryptionProvider (potentially requiring a password from effectiveOptions)
            // to decrypt metadataBytes before deserialization.
            // Example:
            // bool isMetadataEncrypted = false; // Determine this based on flags/options
            // if (isMetadataEncrypted) {
            //     if (string.IsNullOrEmpty(effectiveOptions.Encryption.Password)) 
            //         throw new InvalidOperationException("Password required to decrypt metadata.");
            //     // Need a way to get salt/IV for metadata - stored before metadata block?
            //     // byte[] metaSalt = ... read from stream ...
            //     // byte[] metaIv = ... read from stream ...
            //     // var decryptionProvider = ProviderFactory.GetEncryptionProvider(...);
            //     // metadataBytes = await DecryptMetadataBytesAsync(metadataBytes, decryptionProvider, effectiveOptions.Encryption.Password, metaSalt, metaIv, cancellationToken);
            // }

            // Deserialize the metadata bytes
            // Use System.Text.Json
            using MemoryStream memoryStream = new(metadataBytes);
            FileMetadata? metadata = await JsonSerializer.DeserializeAsync<FileMetadata>(memoryStream, cancellationToken: cancellationToken).ConfigureAwait(false);
            // Assign LastWriteTime from header as a fallback if not present in metadata block itself?
            // This might already be handled when the entry was first created in OpenReadAsync.
            // We could compare and log discrepancies if needed.
            return metadata;
        }
        catch (JsonException jsonEx)
        {
            // Log or handle deserialization error
            // Consider returning null or re-throwing as InvalidDataException
            System.Diagnostics.Debug.WriteLine($"Error deserializing metadata for entry '{entry.FullPath}': {jsonEx.Message}");
            return null; // Or throw? 
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException)
        {
            // Log or handle stream reading errors
            System.Diagnostics.Debug.WriteLine($"Error reading metadata stream for entry '{entry.FullPath}': {ex.Message}");
            throw new InvalidOperationException("Failed to read metadata from the archive stream.", ex);
        }
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

    #region Helper Methods (Copied/Adapted from ArchiveManager)
    // These are needed for ExtractEntryToStreamAsync

    private async Task<byte[]> ReadBytesAsync(Stream stream, int count, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            int read = await stream.ReadAsync(buffer, offset, count - offset, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("Unexpected end of stream while reading archive data.");
            }

            offset += read;
        }
        return buffer;
    }

    private byte[] DeriveKeyFromPassword(string password, byte[] salt, int keySizeBits)
    {
        int keySizeBytes = keySizeBits / 8;
        using Rfc2898DeriveBytes kdf = new(password, salt, AesEncryptionProviderBase.DefaultPbkdf2Iterations, AesEncryptionProviderBase.DefaultPbkdf2HashAlgorithm);
        return kdf.GetBytes(keySizeBytes);
    }

    private Aes CreateAesInstance(int keySizeBits)
    {
        Aes? aes = Aes.Create();
        if (aes is null)
        {
            throw new PlatformNotSupportedException("AES algorithm is not supported on this platform.");
        }

        aes.KeySize = keySizeBits;
        aes.BlockSize = 128;
        return aes;
    }

    private static System.IO.Compression.CompressionLevel MapCompressionLevel(Fragile.Core.Enums.CompressionLevel level)
    {
        return level switch
        {
            Fragile.Core.Enums.CompressionLevel.Fastest => System.IO.Compression.CompressionLevel.NoCompression,
            Fragile.Core.Enums.CompressionLevel.Fast => System.IO.Compression.CompressionLevel.NoCompression,
            Fragile.Core.Enums.CompressionLevel.Normal => System.IO.Compression.CompressionLevel.Fastest,
            Fragile.Core.Enums.CompressionLevel.High => System.IO.Compression.CompressionLevel.Fastest,
            Fragile.Core.Enums.CompressionLevel.Ultra => System.IO.Compression.CompressionLevel.Optimal,
            _ => System.IO.Compression.CompressionLevel.Optimal,
        };
    }

    // Simple Stream wrapper (essential for reading only a part of the stream)
    private class SubStream : Stream
    {
        private readonly Stream _parent;
        private readonly long _startPosition;
        private readonly long _length;
        private readonly bool _leaveParentOpen;
        private long _currentPosition;

        public SubStream(Stream parent, long startPosition, long length, bool leaveParentOpen = false)
        {
            if (!parent.CanRead)
            {
                throw new ArgumentException("Parent stream must be readable.", nameof(parent));
            }

            if (!parent.CanSeek)
            {
                throw new ArgumentException("Parent stream must be seekable.", nameof(parent));
            }

            if (startPosition < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startPosition));
            }

            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            // Allow reading 0 bytes, but check if parent has enough data
            if (startPosition + length > parent.Length)
            {
                throw new ArgumentOutOfRangeException($"Substream (start:{startPosition}, len:{length}) exceeds parent stream length ({parent.Length}).");
            }

            _parent = parent;
            _startPosition = startPosition;
            _length = length;
            _leaveParentOpen = leaveParentOpen;
            _currentPosition = 0;
            // DO NOT seek parent here, let ReadAsync handle it to avoid conflicts
            // _parent.Position = _startPosition; 
        }

        public override bool CanRead => _parent.CanRead;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position
        {
            get => _currentPosition;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // Ensure parent stream position is correct *before* reading
            long parentRequiredPosition = _startPosition + _currentPosition;
            if (_parent.Position != parentRequiredPosition)
            {
                _parent.Position = parentRequiredPosition;
            }

            long remainingInSubstream = _length - _currentPosition;
            if (remainingInSubstream <= 0)
            {
                return 0;
            }

            int bytesToRead = (int)Math.Min(count, remainingInSubstream);
            int bytesRead = _parent.Read(buffer, offset, bytesToRead);
            _currentPosition += bytesRead;
            return bytesRead;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // Ensure parent stream position is correct *before* reading
            long parentRequiredPosition = _startPosition + _currentPosition;
            if (_parent.Position != parentRequiredPosition)
            {
                _parent.Position = parentRequiredPosition;
            }

            long remainingInSubstream = _length - _currentPosition;
            if (remainingInSubstream <= 0)
            {
                return 0;
            }

            int bytesToRead = (int)Math.Min(count, remainingInSubstream);
            int bytesRead = await _parent.ReadAsync(buffer, offset, bytesToRead, cancellationToken).ConfigureAwait(false);
            _currentPosition += bytesRead;
            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPosition = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _currentPosition + offset,
                SeekOrigin.End => _length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };

            if (newPosition < 0 || newPosition > _length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "Seek position is outside the bounds of the substream.");
            }

            _currentPosition = newPosition;
            // We don't need to seek the parent stream until the next Read/ReadAsync
            return _currentPosition;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override void Flush() { /* No-op */ }

        protected override void Dispose(bool disposing)
        {
            // Only dispose parent if we own it (_leaveOpen is false)
            if (disposing && !_leaveParentOpen)
            {
                _parent?.Dispose();
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_leaveParentOpen)
            {
                await _parent.DisposeAsync().ConfigureAwait(false);
            }
            // Use default DisposeAsync implementation for this class's own resources (if any)
            // await base.DisposeAsync().ConfigureAwait(false); // Not needed if no async resources in this derived class
            GC.SuppressFinalize(this);
        }
    }
    #endregion

    // Finalizer (just in case)
    ~ReadableArchive()
    {
        Dispose(false);
    }
}