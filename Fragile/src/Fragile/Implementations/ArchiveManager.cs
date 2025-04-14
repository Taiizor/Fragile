using Fragile.Core;
using Fragile.Core.Events;
using Fragile.Core.Options;
using Fragile.Interfaces;
using Fragile.Interfaces.Providers;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Fragile.Core.Format;
using System.Text.Json; // For potential metadata serialization
using System.Text;
using Fragile.Core.Metadata;
using Fragile.Core.Enums; // For encoding

namespace Fragile.Implementations;

/// <summary>
/// Concrete implementation of the <see cref="IArchiveManager"/> interface.
/// Coordinates archive operations using underlying format handlers and algorithm providers.
/// </summary>
public class ArchiveManager : IArchiveManager
{
    // Placeholder for the actual archive format handling logic (e.g., reading/writing headers, entries)
    // This would likely be another interface/class specific to the .frgl format.
    // private readonly IFragileFormatHandler _formatHandler;

    public event EventHandler<ProgressEventArgs>? ProgressChanged;

    public ArchiveManager(/* IFragileFormatHandler formatHandler */)
    {
        // _formatHandler = formatHandler ?? throw new ArgumentNullException(nameof(formatHandler));
        // In a real scenario, dependencies like the format handler might be injected.
    }

    // --- Implementation of IArchiveManager methods will go here --- 

    public async Task CreateFromDirectoryAsync(string sourceDirectoryPath, string archiveFilePath, ArchiveOptions options, IProgress<ProgressEventArgs>? progress = null, CancellationToken cancellationToken = default)
    {
        // 1. Validate paths and options
        if (string.IsNullOrWhiteSpace(sourceDirectoryPath))
            throw new ArgumentNullException(nameof(sourceDirectoryPath));
        if (string.IsNullOrWhiteSpace(archiveFilePath))
            throw new ArgumentNullException(nameof(archiveFilePath));
        if (!Directory.Exists(sourceDirectoryPath))
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectoryPath}");
        if (options is null)
            throw new ArgumentNullException(nameof(options));

        // Ensure options have non-null defaults where appropriate
        options.Compression ??= new CompressionOptions();
        options.Encryption ??= new EncryptionOptions();
        options.Checksum ??= new ChecksumOptions();
        options.ErrorCorrection ??= new ErrorCorrectionOptions();
        options.ArchiveMetadata ??= new ArchiveMetadata();

        // Validate incompatible options (e.g., Encryption requires password)
        if (options.Encryption.Algorithm != EncryptionAlgorithm.None && string.IsNullOrEmpty(options.Encryption.Password))
            throw new ArgumentException("Password is required when encryption is enabled.", nameof(options));

        long totalBytesToProcess = 0;
        List<string> filePaths = new List<string>();

        // Pre-calculate total size for progress reporting (optional but good practice)
        try
        {           
            filePaths = Directory.EnumerateFiles(sourceDirectoryPath, "*", SearchOption.AllDirectories).ToList();
            totalBytesToProcess = filePaths.Sum(filePath => new FileInfo(filePath).Length);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || ex is DirectoryNotFoundException || ex is IOException)
        { 
            throw new IOException($"Error enumerating files in source directory '{sourceDirectoryPath}'. Check permissions and path validity.", ex);
        }

        long bytesProcessed = 0;
        int bufferSize = options.StreamBufferSize;

        // Temporary list to hold disposable providers
        List<IDisposable> disposableProviders = new List<IDisposable>();

        try
        {
            using (var archiveStream = new FileStream(archiveFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true))
            {
                // --- 4. Write Archive Header (Placeholder) ---
                await archiveStream.WriteAsync(FormatConstants.MagicBytes, 0, FormatConstants.MagicBytes.Length, cancellationToken).ConfigureAwait(false);
                await WriteUShortAsync(archiveStream, FormatConstants.FormatVersionMajor, cancellationToken).ConfigureAwait(false);
                await WriteUShortAsync(archiveStream, FormatConstants.FormatVersionMinor, cancellationToken).ConfigureAwait(false);
                // TODO: Determine and write actual ArchiveHeaderFlags
                await WriteULongAsync(archiveStream, (ulong)FormatConstants.ArchiveHeaderFlags.None, cancellationToken).ConfigureAwait(false);
                // TODO: Write Archive Metadata (potentially serialized/encrypted)
                // For now, write 0 metadata length
                await WriteLongAsync(archiveStream, 0, cancellationToken).ConfigureAwait(false);
                // --- End Placeholder Header ---

                OnProgressChanged(new ProgressEventArgs(bytesProcessed, totalBytesToProcess, statusMessage: "Starting archival..."));

                // 5. Iterate through source directory
                foreach (var filePath in filePaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string relativePath = Path.GetRelativePath(sourceDirectoryPath, filePath).Replace('\\', '/');
                    FileInfo fileInfo = new FileInfo(filePath);
                    string currentFileNameForProgress = Path.GetFileName(filePath);

                    OnProgressChanged(new ProgressEventArgs(bytesProcessed, totalBytesToProcess, currentFile: currentFileNameForProgress, statusMessage: $"Processing: {relativePath}"));

                    // --- 6. Process Each File ---                    
                    long entryStartPosition = archiveStream.Position;
                    byte[]? checksum = null;
                    long compressedSize = -1; // Will be set after processing

                    // --- 6.c Write Entry Header (Placeholder) ---
                    // TODO: Define and write actual entry header structure (flags, name length, name, sizes, timestamps, etc.)
                    FormatConstants.EntryHeaderFlags entryFlags = FormatConstants.EntryHeaderFlags.None;
                    if (options.Encryption.Algorithm != EncryptionAlgorithm.None) entryFlags |= FormatConstants.EntryHeaderFlags.IsEncrypted;
                    if (options.Checksum.Algorithm != ChecksumAlgorithm.None) entryFlags |= FormatConstants.EntryHeaderFlags.HasChecksum;
                    // ... other flags based on options ...
                    
                    await WriteUIntAsync(archiveStream, (uint)entryFlags, cancellationToken).ConfigureAwait(false);
                    byte[] pathBytes = Encoding.UTF8.GetBytes(relativePath);
                    await WriteUShortAsync(archiveStream, (ushort)pathBytes.Length, cancellationToken).ConfigureAwait(false);
                    await archiveStream.WriteAsync(pathBytes, 0, pathBytes.Length, cancellationToken).ConfigureAwait(false);
                    await WriteLongAsync(archiveStream, fileInfo.Length, cancellationToken).ConfigureAwait(false); // Uncompressed size
                    // Need placeholder for compressed size, will update later if possible, or store in central directory
                    long compressedSizePosition = archiveStream.Position;
                    await WriteLongAsync(archiveStream, -1, cancellationToken).ConfigureAwait(false); // Placeholder
                    // --- End Placeholder Entry Header ---

                    long entryDataStartPosition = archiveStream.Position;

                    using (var sourceStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true))
                    {
                        Stream currentStream = sourceStream;
                        Stream? checksumStream = null;
                        IChecksumProvider? checksumProvider = null;
                        IEncryptionProvider? encryptionProvider = null;
                        ICompressionProvider? compressionProvider = null;

                        // --- 6.b Wrap Stream with Providers ---
                        try
                        {
                            // Checksum (calculated on original data *before* encryption/compression)
                            if (options.Checksum.Algorithm != ChecksumAlgorithm.None)
                            {
                                checksumProvider = ProviderFactory.GetChecksumProvider(options.Checksum.Algorithm, bufferSize);
                                if (checksumProvider is IDisposable disposableChecksum) disposableProviders.Add(disposableChecksum);
                                // We need to calculate checksum while copying. A Tee stream or similar approach is needed.
                                // For simplicity now, calculate *after* potential encryption/compression (less ideal)
                                // Or calculate separately before processing (requires reading twice or caching)
                                // Let's calculate before for now (simple but reads twice)
                                checksum = await checksumProvider.ComputeChecksumAsync(sourceStream, options.Checksum, cancellationToken).ConfigureAwait(false);
                                // Reset stream position after checksum calculation
                                if (sourceStream.CanSeek) sourceStream.Position = 0;
                                else throw new InvalidOperationException("Source stream must be seekable for checksum calculation in this implementation.");
                            }

                            // Encryption
                            if (options.Encryption.Algorithm != EncryptionAlgorithm.None)
                            {
                                encryptionProvider = ProviderFactory.GetEncryptionProvider(options.Encryption.Algorithm, bufferSize);
                                var encryptedStream = new MemoryStream(); // Encrypt in memory first to know the size? Complex.
                                // Direct encryption to archive stream is simpler for now
                                // TODO: This needs careful handling of CryptoStream disposal and underlying stream.
                                // Wrapping needs a proper implementation, potentially custom streams.
                                // Placeholder: Assume direct encryption is handled within a dedicated processing function later.
                            }

                            // Compression
                            if (options.Compression.Algorithm != CompressionAlgorithm.Store)
                            {
                                compressionProvider = ProviderFactory.GetCompressionProvider(options.Compression.Algorithm, bufferSize);
                                // TODO: Apply compression stream wrapping
                            }

                            // --- 6.d Copy Processed Stream Data --- 
                            // Simplified copy - assumes no intermediate wrapping for now
                            // In reality, currentStream would be wrapped by compression -> encryption streams
                            await currentStream.CopyToAsync(archiveStream, bufferSize, cancellationToken).ConfigureAwait(false);
                        }
                        finally
                        {
                           // Ensure providers that need disposal are tracked (handled outside loop)
                        }
                    }
                    
                    long entryDataEndPosition = archiveStream.Position;
                    compressedSize = entryDataEndPosition - entryDataStartPosition; 

                    // --- 6.e Write Entry Footer/Metadata (Placeholder) ---
                    // Write checksum if calculated
                    if (checksum != null)
                    {
                        await archiveStream.WriteAsync(checksum, 0, checksum.Length, cancellationToken).ConfigureAwait(false);
                    }
                    // TODO: Write File Metadata (if options.StoreFileMetadata)
                    // TODO: Write ECC data (if options.ErrorCorrection.Level != None)

                    // TODO: If possible, seek back and update compressed size in entry header
                    // if (archiveStream.CanSeek)
                    // {
                    //    long currentPos = archiveStream.Position;
                    //    archiveStream.Position = compressedSizePosition;
                    //    await WriteLongAsync(archiveStream, compressedSize, cancellationToken).ConfigureAwait(false);
                    //    archiveStream.Position = currentPos;
                    // }
                    // --- End Placeholder Entry Footer ---

                    bytesProcessed += fileInfo.Length; // Progress based on original file size
                    OnProgressChanged(new ProgressEventArgs(bytesProcessed, totalBytesToProcess, currentFile: currentFileNameForProgress, statusMessage: $"Completed: {relativePath}"));
                }

                // --- 7. Write Archive Footer (Placeholder) ---
                // TODO: Write Central Directory if used
                // TODO: Write final archive metadata/signatures
                // --- End Placeholder Footer ---
            }
        }
        finally
        {
            // 8. Dispose providers
            foreach (var provider in disposableProviders)
            {
                provider.Dispose();
            }
        }

        OnProgressChanged(new ProgressEventArgs(totalBytesToProcess, totalBytesToProcess, statusMessage: "Archival complete."));
    }

    public void CreateFromDirectory(string sourceDirectoryPath, string archiveFilePath, ArchiveOptions options, IProgress<ProgressEventArgs>? progress = null, CancellationToken cancellationToken = default)
    {
        // Synchronous version of CreateFromDirectoryAsync
        // Consider dedicated sync logic or careful sync-over-async
        CreateFromDirectoryAsync(sourceDirectoryPath, archiveFilePath, options, progress, cancellationToken).GetAwaiter().GetResult();
    }

    public async Task ExtractToDirectoryAsync(string archiveFilePath, string destinationDirectoryPath, ArchiveOptions options, IProgress<ProgressEventArgs>? progress = null, CancellationToken cancellationToken = default)
    {
        // 1. Validate paths and options
        if (string.IsNullOrWhiteSpace(archiveFilePath))
            throw new ArgumentNullException(nameof(archiveFilePath));
        if (string.IsNullOrWhiteSpace(destinationDirectoryPath))
            throw new ArgumentNullException(nameof(destinationDirectoryPath));
        if (!File.Exists(archiveFilePath))
            throw new FileNotFoundException("Archive file not found.", archiveFilePath);
        if (options is null)
            throw new ArgumentNullException(nameof(options));

        // Ensure options have non-null defaults where appropriate
        options.Compression ??= new CompressionOptions();
        options.Encryption ??= new EncryptionOptions();
        options.Checksum ??= new ChecksumOptions();
        options.ErrorCorrection ??= new ErrorCorrectionOptions();

        // Validate options (e.g., password needed if encrypted entries exist)
        // This might require reading the header first to check flags.

        long totalBytesToProcess = 0; // Ideally read from archive header/central directory
        long bytesProcessed = 0;
        int bufferSize = options.StreamBufferSize;
        List<IDisposable> disposableProviders = new List<IDisposable>();

        try
        {
            // Create destination directory if it doesn't exist
            Directory.CreateDirectory(destinationDirectoryPath);

            using (var archiveStream = new FileStream(archiveFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true))
            {
                // --- 3. Read Archive Header (Placeholder) ---
                byte[] magic = await ReadBytesAsync(archiveStream, FormatConstants.MagicBytes.Length, cancellationToken).ConfigureAwait(false);
                if (!magic.SequenceEqual(FormatConstants.MagicBytes))
                    throw new InvalidDataException("File is not a valid Fragile archive.");
                
                ushort versionMajor = await ReadUShortAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                ushort versionMinor = await ReadUShortAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                // TODO: Check version compatibility

                FormatConstants.ArchiveHeaderFlags archiveFlags = (FormatConstants.ArchiveHeaderFlags)await ReadULongAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                long metadataLength = await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                // TODO: Read and potentially decrypt/deserialize Archive Metadata
                if(metadataLength > 0) 
                    archiveStream.Seek(metadataLength, SeekOrigin.Current); // Skip metadata for now
                // --- End Placeholder Header ---

                OnProgressChanged(new ProgressEventArgs(bytesProcessed, totalBytesToProcess, statusMessage: "Starting extraction..."));

                // --- 4. Iterate Through Entries (Placeholder - assumes sequential reading) ---
                // TODO: Implement proper entry reading based on the actual .frgl format.
                // This might involve reading entry headers sequentially or loading a central directory.
                // The loop below is a *highly* simplified placeholder.
                while (archiveStream.Position < archiveStream.Length) // Very basic loop condition
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // --- 4.a Read Entry Header (Placeholder) ---
                    FormatConstants.EntryHeaderFlags entryFlags = (FormatConstants.EntryHeaderFlags)await ReadUIntAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                    ushort pathLength = await ReadUShortAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                    byte[] pathBytes = await ReadBytesAsync(archiveStream, pathLength, cancellationToken).ConfigureAwait(false);
                    string relativePath = Encoding.UTF8.GetString(pathBytes);
                    long uncompressedSize = await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                    long compressedSize = await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false); // Read compressed size
                    // TODO: Read timestamps and other metadata if HasMetadata flag is set
                    // --- End Placeholder Entry Header ---
                    
                    string destinationPath = Path.Combine(destinationDirectoryPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                    string? currentFileNameForProgress = Path.GetFileName(relativePath);

                    OnProgressChanged(new ProgressEventArgs(bytesProcessed, totalBytesToProcess, currentFile: currentFileNameForProgress, statusMessage: $"Extracting: {relativePath}"));

                    if ((entryFlags & FormatConstants.EntryHeaderFlags.IsDirectory) != 0)
                    {
                        Directory.CreateDirectory(destinationPath);
                        // Directories don't have content or checksums in this simple model
                    }
                    else
                    {
                        // Ensure destination directory exists
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? destinationDirectoryPath);

                        // --- 4.a.ii Get Providers --- 
                        IChecksumProvider? checksumProvider = null;
                        IEncryptionProvider? decryptionProvider = null;
                        ICompressionProvider? decompressionProvider = null;
                        byte[]? expectedChecksum = null;

                        // Determine providers based on entry flags (needs actual format definition)
                        bool isEncrypted = (entryFlags & FormatConstants.EntryHeaderFlags.IsEncrypted) != 0;
                        bool hasChecksum = (entryFlags & FormatConstants.EntryHeaderFlags.HasChecksum) != 0;
                        // Assume compression based on options for now (real format needs to store this info)
                        bool isCompressed = options.Compression?.Algorithm != CompressionAlgorithm.Store;

                        // TODO: Get providers based on algorithms stored in the archive/entry header, not just options
                        if (isEncrypted)
                        {
                            if (options.Encryption?.Algorithm == EncryptionAlgorithm.None || string.IsNullOrEmpty(options.Encryption.Password))
                                throw new InvalidOperationException($"Password is required to extract encrypted entry: {relativePath}");
                            decryptionProvider = ProviderFactory.GetEncryptionProvider(options.Encryption.Algorithm, bufferSize);
                        }
                        if (isCompressed) 
                        { 
                            decompressionProvider = ProviderFactory.GetCompressionProvider(options.Compression.Algorithm, bufferSize); 
                        }
                        if (hasChecksum)
                        { 
                            checksumProvider = ProviderFactory.GetChecksumProvider(options.Checksum.Algorithm, bufferSize);
                            if (checksumProvider is IDisposable disposableChecksum) disposableProviders.Add(disposableChecksum);
                        }
                        
                        // --- 4.a.iii Get Entry Data Stream --- 
                        // Create a limited stream wrapper to read only the compressed data
                        // TODO: Need robust way to get entry data (seek or read exact length)
                        Stream entryDataStream = archiveStream; // Placeholder - needs limiting
                        long dataToRead = compressedSize;

                        // --- 4.a.v Open Destination --- 
                        // TODO: Handle overwrite option
                        using (var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true))
                        {
                            Stream streamToWriteTo = destinationStream;
                            Stream? streamToReadFrom = null;
                            
                            // --- 4.a.iv Wrap Stream --- 
                            // Apply wrappers in reverse order: Decryption -> Decompression
                            // TODO: Implement proper stream wrapping
                            Stream readingStream = entryDataStream; // Start with raw entry data stream
                            
                            if(decryptionProvider != null)
                            {
                                // TODO: Wrap readingStream with decryption CryptoStream
                                // readingStream = new CryptoStream(readingStream, decryptionProvider.CreateDecryptor(...), CryptoStreamMode.Read, leaveOpen: true);
                            }
                            if(decompressionProvider != null)
                            { 
                                // TODO: Wrap readingStream with decompression stream (e.g., DeflateStream)
                                // readingStream = new DeflateStream(readingStream, CompressionMode.Decompress, leaveOpen: true);
                            }

                            streamToReadFrom = readingStream; // Final stream to read decompressed, decrypted data from

                            // TODO: Implement checksum calculation *while* writing to destination or after
                            // If calculating after, need to write to a temporary stream first or read destination file back.
                            // Simple approach: Copy first, then verify.

                            // --- 4.a.vi Copy Data --- 
                            // Placeholder: Copy limited bytes directly for now
                            await CopyLimitedBytesAsync(streamToReadFrom, streamToWriteTo, dataToRead, bufferSize, cancellationToken).ConfigureAwait(false);
                            
                        } // Dispose destinationStream

                        // --- 4.a.vii Verify Checksum (Placeholder) --- 
                        if (checksumProvider != null)
                        {
                            // TODO: Read the expected checksum from the end of the entry data
                            // expectedChecksum = await ReadBytesAsync(archiveStream, checksumProvider.ChecksumLengthBytes, cancellationToken).ConfigureAwait(false);
                            
                            // Recalculate checksum on the extracted file
                            // byte[] actualChecksum; 
                            // using (var extractedFileStream = File.OpenRead(destinationPath)) {\n                            //    actualChecksum = await checksumProvider.ComputeChecksumAsync(extractedFileStream, options.Checksum, cancellationToken);\n                            // } 
                            // if (!actualChecksum.SequenceEqual(expectedChecksum)) {\n                            //     throw new InvalidDataException($\"Checksum mismatch for entry: {relativePath}\");\n                            // }\n                        }
                         // --- 4.a.viii Set File Attributes/Timestamps --- 
                         // TODO: Read metadata from archive and apply using File.SetAttributes, SetCreationTimeUtc etc.
                    }

                    // Progress update based on compressed size (more accurate for extraction progress)
                    bytesProcessed += compressedSize; // Add header/footer sizes too? Needs clarification.
                    // Use uncompressed size if totalBytesToProcess was based on that.
                    // bytesProcessed += uncompressedSize;

                    OnProgressChanged(new ProgressEventArgs(bytesProcessed, totalBytesToProcess, currentFile: currentFileNameForProgress, statusMessage: $"Extracted: {relativePath}"));
                }
                // --- End Entry Iteration --- 
            }
        }
        finally
        {
            // 5. Dispose providers
            foreach (var provider in disposableProviders)
            {
                provider.Dispose();
            }
        }
        OnProgressChanged(new ProgressEventArgs(bytesProcessed, totalBytesToProcess, statusMessage: "Extraction complete.")); // Use actual total extracted size?
    }

    public void ExtractToDirectory(string archiveFilePath, string destinationDirectoryPath, ArchiveOptions options, IProgress<ProgressEventArgs>? progress = null, CancellationToken cancellationToken = default)
    {
        // Synchronous version of ExtractToDirectoryAsync
        ExtractToDirectoryAsync(archiveFilePath, destinationDirectoryPath, options, progress, cancellationToken).GetAwaiter().GetResult();
    }

    public Task<IReadableArchive> OpenReadAsync(string archiveFilePath, ArchiveOptions options, CancellationToken cancellationToken = default)
    {
        // 1. Validate path and options
        // 2. Open archive file stream
        // 3. Read/parse archive structure (headers, entry list) using _formatHandler
        // 4. Create and return an implementation of IReadableArchive (e.g., ReadableArchive)
        //    This object would hold the stream, entry list, and potentially the format handler.
        throw new NotImplementedException();
    }

    public IReadableArchive OpenRead(string archiveFilePath, ArchiveOptions options)
    {
        // Synchronous version of OpenReadAsync
        return OpenReadAsync(archiveFilePath, options).GetAwaiter().GetResult();
    }

    public Task<IReadableArchive> OpenReadAsync(Stream archiveStream, ArchiveOptions options, bool leaveOpen = false, CancellationToken cancellationToken = default)
    {
        // Similar to file path version, but uses the provided stream.
        // Validate stream properties (CanRead, CanSeek).
        throw new NotImplementedException();
    }

    public IReadableArchive OpenRead(Stream archiveStream, ArchiveOptions options, bool leaveOpen = false)
    {
        // Synchronous version of OpenReadAsync (Stream)
        return OpenReadAsync(archiveStream, options, leaveOpen).GetAwaiter().GetResult();
    }

    public Task<bool> VerifyArchiveAsync(string archiveFilePath, ArchiveOptions options, IProgress<ProgressEventArgs>? progress = null, CancellationToken cancellationToken = default)
    {
        // 1. Open archive
        // 2. Verify header/signature
        // 3. Optionally iterate through entries, verify checksums/ECC data
        // 4. Report progress/cancellation
        throw new NotImplementedException();
    }

    public bool VerifyArchive(string archiveFilePath, ArchiveOptions options, IProgress<ProgressEventArgs>? progress = null, CancellationToken cancellationToken = default)
    {
        // Synchronous version of VerifyArchiveAsync
        return VerifyArchiveAsync(archiveFilePath, options, progress, cancellationToken).GetAwaiter().GetResult();
    }

    protected virtual void OnProgressChanged(ProgressEventArgs e)
    {
        ProgressChanged?.Invoke(this, e);
    }

    #region Binary Writer Helpers (Placeholder - Consider a dedicated BinaryWriter/Reader class)
    private async Task WriteUShortAsync(Stream stream, ushort value, CancellationToken cancellationToken)
    {
        byte[] buffer = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian) Array.Reverse(buffer); // Assume Little Endian format
        await stream.WriteAsync(buffer, 0, 2, cancellationToken).ConfigureAwait(false);
    }
    private async Task WriteUIntAsync(Stream stream, uint value, CancellationToken cancellationToken)
    {
        byte[] buffer = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian) Array.Reverse(buffer);
        await stream.WriteAsync(buffer, 0, 4, cancellationToken).ConfigureAwait(false);
    }
    private async Task WriteLongAsync(Stream stream, long value, CancellationToken cancellationToken)
    {
        byte[] buffer = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian) Array.Reverse(buffer);
        await stream.WriteAsync(buffer, 0, 8, cancellationToken).ConfigureAwait(false);
    }
    private async Task WriteULongAsync(Stream stream, ulong value, CancellationToken cancellationToken)
    {
        byte[] buffer = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian) Array.Reverse(buffer);
        await stream.WriteAsync(buffer, 0, 8, cancellationToken).ConfigureAwait(false);
    }
    #endregion

    #region Binary Reader Helpers (Placeholder)
    private async Task<byte[]> ReadBytesAsync(Stream stream, int count, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            int read = await stream.ReadAsync(buffer, offset, count - offset, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException("Unexpected end of stream while reading archive data.");
            offset += read;
        }
        return buffer;
    }
    private async Task<ushort> ReadUShortAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] buffer = await ReadBytesAsync(stream, 2, cancellationToken).ConfigureAwait(false);
        if (!BitConverter.IsLittleEndian) Array.Reverse(buffer);
        return BitConverter.ToUInt16(buffer, 0);
    }
    private async Task<uint> ReadUIntAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] buffer = await ReadBytesAsync(stream, 4, cancellationToken).ConfigureAwait(false);
        if (!BitConverter.IsLittleEndian) Array.Reverse(buffer);
        return BitConverter.ToUInt32(buffer, 0);
    }
    private async Task<long> ReadLongAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] buffer = await ReadBytesAsync(stream, 8, cancellationToken).ConfigureAwait(false);
        if (!BitConverter.IsLittleEndian) Array.Reverse(buffer);
        return BitConverter.ToInt64(buffer, 0);
    }
    private async Task<ulong> ReadULongAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] buffer = await ReadBytesAsync(stream, 8, cancellationToken).ConfigureAwait(false);
        if (!BitConverter.IsLittleEndian) Array.Reverse(buffer);
        return BitConverter.ToUInt64(buffer, 0);
    }
    private async Task CopyLimitedBytesAsync(Stream source, Stream destination, long count, int bufferSize, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[bufferSize];
        long remaining = count;
        while (remaining > 0)
        {
            int bytesToRead = (int)Math.Min(remaining, buffer.Length);
            int bytesRead = await source.ReadAsync(buffer, 0, bytesToRead, cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0) break; // End of source stream
            await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
            remaining -= bytesRead;
        }
        if (remaining > 0) 
            throw new EndOfStreamException("Unexpected end of entry data stream.");
    }
    #endregion
} 