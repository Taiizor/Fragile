using Fragile.Core;
using Fragile.Core.Enums; // For encoding
using Fragile.Core.Events;
using Fragile.Core.Format;
using Fragile.Core.Metadata;
using Fragile.Core.Options;
using Fragile.Implementations.Providers.Encryption;
using Fragile.Interfaces;
using Fragile.Interfaces.Providers;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json; // For potential metadata serialization

namespace Fragile.Implementations;

/// <summary>
/// Concrete implementation of the <see cref="IArchiveManager"/> interface.
/// Coordinates archive operations using underlying format handlers and algorithm providers.
/// </summary>
public class ArchiveManager : IArchiveManager
{
    /// <summary>
    /// Represents an entry within the manifest of a solid archive block.
    /// </summary>
    private struct SolidManifestEntry
    {
        public string RelativePath { get; set; }
        public long UncompressedSize { get; set; }
        public long Offset { get; set; } // Offset within the combined *uncompressed* data stream
    }

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
        {
            throw new ArgumentNullException(nameof(sourceDirectoryPath));
        }

        if (string.IsNullOrWhiteSpace(archiveFilePath))
        {
            throw new ArgumentNullException(nameof(archiveFilePath));
        }

        if (!Directory.Exists(sourceDirectoryPath))
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectoryPath}");
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        // Ensure options have non-null defaults where appropriate
        options.Compression ??= new CompressionOptions();
        options.Encryption ??= new EncryptionOptions();
        options.Checksum ??= new ChecksumOptions();
        options.ErrorCorrection ??= new ErrorCorrectionOptions();
        options.ArchiveMetadata ??= new ArchiveMetadata();

        // Validate incompatible options (e.g., Encryption requires password)
        if (options.Encryption.Algorithm != EncryptionAlgorithm.None && string.IsNullOrEmpty(options.Encryption.Password))
        {
            throw new ArgumentException("Password is required when encryption is enabled.", nameof(options));
        }

        long totalBytesToProcess = 0;
        List<string> filePaths = new();

        // Pre-calculate total size for progress reporting (optional but good practice)
        try
        {
            filePaths = Directory.EnumerateFiles(sourceDirectoryPath, "*", SearchOption.AllDirectories).ToList();
            totalBytesToProcess = filePaths.Sum(filePath => new FileInfo(filePath).Length);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
        {
            throw new IOException($"Error enumerating files in source directory '{sourceDirectoryPath}'. Check permissions and path validity.", ex);
        }

        long bytesProcessed = 0;
        int bufferSize = options.StreamBufferSize;

        // Temporary list to hold disposable providers
        List<IDisposable> disposableProviders = new();

        try
        {
            using FileStream archiveStream = new(archiveFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true);
            // --- 4. Write Archive Header (Placeholder) ---
            await archiveStream.WriteAsync(FormatConstants.MagicBytes, 0, FormatConstants.MagicBytes.Length, cancellationToken).ConfigureAwait(false);
            await WriteUShortAsync(archiveStream, FormatConstants.FormatVersionMajor, cancellationToken).ConfigureAwait(false);
            await WriteUShortAsync(archiveStream, FormatConstants.FormatVersionMinor, cancellationToken).ConfigureAwait(false);
            // TODO: Determine and write actual ArchiveHeaderFlags
            FormatConstants.ArchiveHeaderFlags archiveFlags = FormatConstants.ArchiveHeaderFlags.None; // Start with None

            // Set SolidArchive flag if solid compression is used
            if (options.Compression?.UseSolidCompression ?? false)
            {
                archiveFlags |= FormatConstants.ArchiveHeaderFlags.SolidArchive;
            }

            // Serialize Archive Metadata (if provided)
            byte[]? serializedMetadata = null;
            long metadataLength = 0;
            if (options.ArchiveMetadata != null)
            {
                try
                {
                    // Ensure UTC times are consistent if necessary (optional)
                    options.ArchiveMetadata.CreationTimeUtc = options.ArchiveMetadata.CreationTimeUtc.ToUniversalTime();
                    if (options.ArchiveMetadata.LastModificationTimeUtc.HasValue)
                    {
                        options.ArchiveMetadata.LastModificationTimeUtc = options.ArchiveMetadata.LastModificationTimeUtc.Value.ToUniversalTime();
                    }
                    // TODO: Implement metadata encryption if needed based on flags/options
                    // if ((archiveFlags & FormatConstants.ArchiveHeaderFlags.ArchiveMetadataEncrypted) != 0) { ... encrypt serializedMetadata ... }
                    serializedMetadata = JsonSerializer.SerializeToUtf8Bytes(options.ArchiveMetadata);
                    metadataLength = serializedMetadata.Length;
                }
                catch (JsonException jsonEx)
                {
                    // Log or handle serialization error - potentially continue without metadata?
                    Console.WriteLine($"Warning: Could not serialize ArchiveMetadata. Error: {jsonEx.Message}");
                    metadataLength = 0;
                    serializedMetadata = null;
                }
            }

            // Write archive flags
            await WriteULongAsync(archiveStream, (ulong)archiveFlags, cancellationToken).ConfigureAwait(false);
            // Write the actual metadata length
            long metadataLengthPosition = archiveStream.Position;
            await WriteLongAsync(archiveStream, metadataLength, cancellationToken).ConfigureAwait(false);

            // Write the serialized metadata itself (if any)
            if (serializedMetadata != null && metadataLength > 0)
            {
                await archiveStream.WriteAsync(serializedMetadata, 0, serializedMetadata.Length, cancellationToken).ConfigureAwait(false);
            }
            // --- End Archive Header ---

            OnProgressChanged(new ProgressEventArgs(bytesProcessed, totalBytesToProcess, statusMessage: "Starting archival..."));

            if (!(options.Compression?.UseSolidCompression ?? false))
            {
                // --- Standard Archival (Non-Solid) ---
                // 5. Iterate through source directory
                foreach (string filePath in filePaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string relativePath = Path.GetRelativePath(sourceDirectoryPath, filePath).Replace('\\', '/');
                    FileInfo fileInfo = new(filePath);
                    string currentFileNameForProgress = Path.GetFileName(filePath);

                    OnProgressChanged(new ProgressEventArgs(bytesProcessed, totalBytesToProcess, currentFile: currentFileNameForProgress, statusMessage: $"Processing: {relativePath}"));

                    // --- 6. Process Each File ---                    
                    long entryStartPosition = archiveStream.Position;
                    byte[]? checksum = null;
                    long compressedSize = -1; // Will be set after processing
                    byte[]? metadataBytes = null;

                    // --- 6.c Write Entry Header (Placeholder) ---
                    FormatConstants.EntryHeaderFlags entryFlags = FormatConstants.EntryHeaderFlags.None;
                    if (options.Encryption.Algorithm != EncryptionAlgorithm.None)
                    {
                        entryFlags |= FormatConstants.EntryHeaderFlags.IsEncrypted;
                    }

                    if (options.Checksum.Algorithm != ChecksumAlgorithm.None)
                    {
                        entryFlags |= FormatConstants.EntryHeaderFlags.HasChecksum;
                    }

                    // --- Prepare FileMetadata (if storing) ---
                    FileMetadata? fileMetadata = null;
                    if (options.StoreFileMetadata)
                    {
                        try
                        {
                            fileMetadata = new FileMetadata
                            {
                                CreationTimeUtc = fileInfo.CreationTimeUtc,
                                LastWriteTimeUtc = fileInfo.LastWriteTimeUtc,
                                LastAccessTimeUtc = fileInfo.LastAccessTimeUtc,
                                Attributes = fileInfo.Attributes,
                                // Owner/Group/MimeType might require platform-specific calls or external libraries
                                // CustomProperties could be populated based on specific application needs
                            };
                            metadataBytes = JsonSerializer.SerializeToUtf8Bytes(fileMetadata);
                            entryFlags |= FormatConstants.EntryHeaderFlags.HasMetadata;
                        }
                        catch (Exception ex)
                        {
                            // Log metadata gathering/serialization error, but maybe continue?
                            // Consider adding an option to control this behavior.
                            Console.WriteLine($"Warning: Could not process metadata for {relativePath}. Error: {ex.Message}");
                            metadataBytes = null;
                            fileMetadata = null; // Ensure it's null if serialization failed
                        }
                    }
                    // --- End Prepare FileMetadata ---

                    await WriteUIntAsync(archiveStream, (uint)entryFlags, cancellationToken).ConfigureAwait(false);
                    byte[] pathBytes = Encoding.UTF8.GetBytes(relativePath);
                    await WriteUShortAsync(archiveStream, (ushort)pathBytes.Length, cancellationToken).ConfigureAwait(false);
                    await archiveStream.WriteAsync(pathBytes, 0, pathBytes.Length, cancellationToken).ConfigureAwait(false);
                    // Write LastWriteTimeUtc.Ticks directly into the header for quick access
                    await WriteLongAsync(archiveStream, fileMetadata?.LastWriteTimeUtc?.Ticks ?? 0L, cancellationToken).ConfigureAwait(false);
                    await WriteLongAsync(archiveStream, fileInfo.Length, cancellationToken).ConfigureAwait(false); // Uncompressed size
                                                                                                                   // Need placeholder for compressed size, will update later if possible, or store in central directory
                    long compressedSizePosition = archiveStream.Position;
                    await WriteLongAsync(archiveStream, -1, cancellationToken).ConfigureAwait(false); // Placeholder
                    long entryMetadataLengthPosition = archiveStream.Position;
                    await WriteLongAsync(archiveStream, metadataBytes?.Length ?? 0, cancellationToken).ConfigureAwait(false); // Write metadata length (0 if none)
                                                                                                                              // --- End Placeholder Entry Header ---

                    long entryDataStartPosition = archiveStream.Position;

                    using (FileStream sourceStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true))
                    {
                        Stream streamToProcess = sourceStream;
                        IChecksumProvider? checksumProvider = null;
                        IEncryptionProvider? encryptionProvider = null;
                        ICompressionProvider? compressionProvider = null;

                        // --- 6.b Prepare Providers and Calculate Checksum (if applicable) ---
                        try
                        {
                            if (options.Checksum.Algorithm != ChecksumAlgorithm.None)
                            {
                                checksumProvider = ProviderFactory.GetChecksumProvider(options.Checksum.Algorithm, bufferSize);
                                if (checksumProvider is IDisposable disposableChecksum)
                                {
                                    disposableProviders.Add(disposableChecksum);
                                }

                                checksum = await checksumProvider.ComputeChecksumAsync(streamToProcess, options.Checksum, cancellationToken).ConfigureAwait(false);
                                if (streamToProcess.CanSeek)
                                {
                                    streamToProcess.Position = 0;
                                }
                                else
                                {
                                    throw new InvalidOperationException("Source stream must be seekable for checksum calculation.");
                                }
                            }

                            if (options.Encryption.Algorithm != EncryptionAlgorithm.None)
                            {
                                encryptionProvider = ProviderFactory.GetEncryptionProvider(options.Encryption.Algorithm, bufferSize);
                            }
                            if (options.Compression.Algorithm != CompressionAlgorithm.Store)
                            {
                                compressionProvider = ProviderFactory.GetCompressionProvider(options.Compression.Algorithm, bufferSize);
                            }

                            // --- 6.d Process and Copy Stream Data --- 
                            // Pipeline: Source -> Compression? -> Encryption? -> Archive
                            Stream currentOutputStream = archiveStream;
                            List<Stream> processingStreams = new();

                            try
                            {
                                // 1. Encryption Layer (applied first to the output stream)
                                byte[]? salt = null; // Store salt/iv if generated
                                byte[]? iv = null;
                                if (encryptionProvider != null)
                                {
                                    using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) // Use instance
                                    {
                                        salt = new byte[AesEncryptionProviderBase.DefaultSaltSizeBytes];
                                        rng.GetBytes(salt);
                                        iv = new byte[AesEncryptionProviderBase.DefaultIvSizeBytes];
                                        rng.GetBytes(iv);
                                    }
                                    byte[] key = DeriveKeyFromPassword(options.Encryption.Password!, salt, encryptionProvider is Aes256EncryptionProvider ? 256 : 128);

                                    // Write Salt and IV *before* encrypted data
                                    await archiveStream.WriteAsync(salt, 0, salt.Length, cancellationToken).ConfigureAwait(false);
                                    await archiveStream.WriteAsync(iv, 0, iv.Length, cancellationToken).ConfigureAwait(false);

                                    using Aes aes = CreateAesInstance(encryptionProvider is Aes256EncryptionProvider ? 256 : 128);
                                    aes.Key = key;
                                    aes.IV = iv;
                                    aes.Mode = CipherMode.CBC;
                                    aes.Padding = PaddingMode.PKCS7;

                                    // Create CryptoStream that writes *encrypted* data to the archive stream
                                    CryptoStream cryptoStream = new(archiveStream, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true);
                                    processingStreams.Add(cryptoStream);
                                    currentOutputStream = cryptoStream; // Compression (if any) will write to CryptoStream
                                }

                                // 2. Compression Layer (writes to the encryption layer or directly to archive)
                                if (compressionProvider != null)
                                {
                                    DeflateStream compressionStream = new(currentOutputStream, MapCompressionLevel(options.Compression.Level), leaveOpen: true);
                                    processingStreams.Add(compressionStream);
                                    currentOutputStream = compressionStream; // Copy source data *to* this compression stream
                                }

                                // 3. Copy Data: Source -> Innermost Wrapper (or Archive Directly)
                                await streamToProcess.CopyToAsync(currentOutputStream, bufferSize, cancellationToken).ConfigureAwait(false);
                            }
                            finally
                            {
                                // Dispose wrapper streams in reverse order (Compression -> Encryption)
                                processingStreams.Reverse();
                                foreach (Stream stream in processingStreams)
                                {
                                    // Important: Dispose flushes the streams (CryptoStream, DeflateStream)
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
                        }
                        finally
                        {
                            // Provider disposal is handled outside the loop
                        }
                    }

                    long entryDataEndPosition = archiveStream.Position;
                    compressedSize = entryDataEndPosition - entryDataStartPosition;

                    // --- 6.e Write Entry Footer/Metadata (Placeholder) ---
                    if (checksum != null)
                    {
                        await archiveStream.WriteAsync(checksum, 0, checksum.Length, cancellationToken).ConfigureAwait(false);
                    }
                    // Write File Metadata if available
                    if (metadataBytes != null)
                    {
                        await archiveStream.WriteAsync(metadataBytes, 0, metadataBytes.Length, cancellationToken).ConfigureAwait(false);
                    }
                    // TODO: Write ECC data (if options.ErrorCorrection.Level != None)

                    // Seek back and update compressed size in header if possible
                    long finalEntryPosition = archiveStream.Position;
                    if (archiveStream.CanSeek)
                    {
                        archiveStream.Position = compressedSizePosition;
                        await WriteLongAsync(archiveStream, compressedSize, cancellationToken).ConfigureAwait(false);
                        archiveStream.Position = finalEntryPosition;
                    }
                    else
                    {
                        // Cannot update size - format needs a central directory or other mechanism
                    }
                    // --- End Placeholder Entry Footer ---

                    bytesProcessed += fileInfo.Length; // Progress based on original file size
                    OnProgressChanged(new ProgressEventArgs(bytesProcessed, totalBytesToProcess, currentFile: currentFileNameForProgress, statusMessage: $"Completed: {relativePath}"));
                }
            }
            else
            {
                // --- Solid Archival Implementation ---
                OnProgressChanged(new ProgressEventArgs(bytesProcessed, totalBytesToProcess, statusMessage: "Starting solid archival..."));

                List<SolidManifestEntry> manifest = new();
                long currentOffset = 0;
                long totalUncompressedSolidSize = 0;

                // Use MemoryStream for combining if total size is reasonable, else temp file.
                // For simplicity, starting with MemoryStream. Add temp file logic later if needed.
                using MemoryStream combinedStream = new();

                // 1 & 2: Combine files and build manifest
                foreach (string filePath in filePaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string relativePath = Path.GetRelativePath(sourceDirectoryPath, filePath).Replace('\\', '/');
                    FileInfo fileInfo = new(filePath);
                    string currentFileNameForProgress = Path.GetFileName(filePath);

                    OnProgressChanged(new ProgressEventArgs(bytesProcessed, totalBytesToProcess, currentFile: currentFileNameForProgress, statusMessage: $"Combining: {relativePath}"));

                    long uncompressedSize = fileInfo.Length;
                    manifest.Add(new SolidManifestEntry
                    {
                        RelativePath = relativePath,
                        UncompressedSize = uncompressedSize,
                        Offset = currentOffset
                        // We don't store length explicitly, it's derived from the next entry's offset or total size
                    });

                    using (FileStream sourceStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true))
                    {
                        await sourceStream.CopyToAsync(combinedStream, bufferSize, cancellationToken).ConfigureAwait(false);
                    }

                    currentOffset += uncompressedSize;
                    totalUncompressedSolidSize += uncompressedSize;
                    bytesProcessed += uncompressedSize; // Progress based on combined size
                    OnProgressChanged(new ProgressEventArgs(bytesProcessed, totalBytesToProcess, currentFile: currentFileNameForProgress, statusMessage: $"Combined: {relativePath}"));
                }

                // 3. Process the combined stream
                combinedStream.Position = 0; // Rewind before processing
                long solidBlockStartPosition = archiveStream.Position; // Position where solid block data will start
                Stream streamToProcess = combinedStream;
                IEncryptionProvider? encryptionProvider = null;
                ICompressionProvider? compressionProvider = null;

                try
                {
                    if (options.Encryption.Algorithm != EncryptionAlgorithm.None)
                    {
                        encryptionProvider = ProviderFactory.GetEncryptionProvider(options.Encryption.Algorithm, bufferSize);
                    }
                    if (options.Compression.Algorithm != CompressionAlgorithm.Store)
                    {
                        compressionProvider = ProviderFactory.GetCompressionProvider(options.Compression.Algorithm, bufferSize);
                    }

                    // Pipeline: CombinedStream -> Compression? -> Encryption? -> Archive
                    Stream currentOutputStream = archiveStream;
                    List<Stream> processingStreams = new();

                    try
                    {
                        // Encryption Layer
                        byte[]? salt = null, iv = null;
                        if (encryptionProvider != null)
                        {
                            using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) { salt = new byte[AesEncryptionProviderBase.DefaultSaltSizeBytes]; rng.GetBytes(salt); iv = new byte[AesEncryptionProviderBase.DefaultIvSizeBytes]; rng.GetBytes(iv); }
                            byte[] key = DeriveKeyFromPassword(options.Encryption.Password!, salt, encryptionProvider is Aes256EncryptionProvider ? 256 : 128);
                            await archiveStream.WriteAsync(salt, 0, salt.Length, cancellationToken).ConfigureAwait(false);
                            await archiveStream.WriteAsync(iv, 0, iv.Length, cancellationToken).ConfigureAwait(false);
                            using Aes aes = CreateAesInstance(encryptionProvider is Aes256EncryptionProvider ? 256 : 128); aes.Key = key; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                            CryptoStream cryptoStream = new(archiveStream, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true);
                            processingStreams.Add(cryptoStream); currentOutputStream = cryptoStream;
                        }

                        // Compression Layer
                        if (compressionProvider != null)
                        {
                            DeflateStream compressionStream = new(currentOutputStream, MapCompressionLevel(options.Compression.Level), leaveOpen: true);
                            processingStreams.Add(compressionStream); currentOutputStream = compressionStream;
                        }

                        // Copy Data
                        await streamToProcess.CopyToAsync(currentOutputStream, bufferSize, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        processingStreams.Reverse();
                        foreach (Stream stream in processingStreams)
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
                }
                finally { /* Provider disposal handled outside */ }

                long solidBlockEndPosition = archiveStream.Position;
                long solidBlockCompressedSize = solidBlockEndPosition - solidBlockStartPosition;

                // 4. Write the "Solid Block" Entry Header
                // Use specific flags to indicate this is the main solid block entry
                FormatConstants.EntryHeaderFlags solidEntryFlags = FormatConstants.EntryHeaderFlags.IsSolidBlock;
                if (options.Encryption.Algorithm != EncryptionAlgorithm.None)
                {
                    solidEntryFlags |= FormatConstants.EntryHeaderFlags.IsEncrypted;
                }
                // Checksum for the solid block itself could be added here if needed
                // if (options.Checksum.Algorithm != ChecksumAlgorithm.None) solidEntryFlags |= FormatConstants.EntryHeaderFlags.HasChecksum;

                await WriteUIntAsync(archiveStream, (uint)solidEntryFlags, cancellationToken).ConfigureAwait(false);
                await WriteUShortAsync(archiveStream, 0, cancellationToken).ConfigureAwait(false); // No relative path for the block itself
                await WriteLongAsync(archiveStream, 0, cancellationToken).ConfigureAwait(false); // No LastWriteTime for the block
                await WriteLongAsync(archiveStream, totalUncompressedSolidSize, cancellationToken).ConfigureAwait(false); // Total uncompressed size of combined files
                await WriteLongAsync(archiveStream, solidBlockCompressedSize, cancellationToken).ConfigureAwait(false); // Compressed size of the block
                await WriteLongAsync(archiveStream, 0, cancellationToken).ConfigureAwait(false); // No specific file metadata for the block entry itself

                // 5. TODO: Write checksum for the solid block data if needed (calculate checksum on the compressed data before this point)

                // 6. Serialize and Write the Manifest
                byte[] manifestBytes;
                try
                {
                    manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest);
                }
                catch (JsonException jsonEx)
                {
                    throw new InvalidOperationException("Failed to serialize solid archive manifest.", jsonEx);
                }

                // Write manifest length header
                await WriteLongAsync(archiveStream, manifestBytes.Length, cancellationToken).ConfigureAwait(false);
                // Write manifest data
                await archiveStream.WriteAsync(manifestBytes, 0, manifestBytes.Length, cancellationToken).ConfigureAwait(false);

                // Update progress to 100% after manifest is written
                OnProgressChanged(new ProgressEventArgs(totalBytesToProcess, totalBytesToProcess, statusMessage: "Solid archival complete."));
            }

            // --- 7. Write Archive Footer (Placeholder) ---
            // TODO: Write Central Directory if used
            // TODO: Write final archive metadata/signatures
            // --- End Placeholder Footer ---
        }
        finally
        {
            // 8. Dispose providers
            foreach (IDisposable provider in disposableProviders)
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
        {
            throw new ArgumentNullException(nameof(archiveFilePath));
        }

        if (string.IsNullOrWhiteSpace(destinationDirectoryPath))
        {
            throw new ArgumentNullException(nameof(destinationDirectoryPath));
        }

        if (!File.Exists(archiveFilePath))
        {
            throw new FileNotFoundException("Archive file not found.", archiveFilePath);
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

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
        List<IDisposable> disposableProviders = new();

        try
        {
            Directory.CreateDirectory(destinationDirectoryPath);
            using FileStream archiveStream = new(archiveFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);
            // --- 3. Read Archive Header (Placeholder) ---
            byte[] magic = await ReadBytesAsync(archiveStream, FormatConstants.MagicBytes.Length, cancellationToken).ConfigureAwait(false);
            if (!magic.SequenceEqual(FormatConstants.MagicBytes))
            {
                throw new InvalidDataException("File is not a valid Fragile archive.");
            }

            ushort versionMajor = await ReadUShortAsync(archiveStream, cancellationToken).ConfigureAwait(false);
            ushort versionMinor = await ReadUShortAsync(archiveStream, cancellationToken).ConfigureAwait(false);
            // TODO: Check version compatibility

            FormatConstants.ArchiveHeaderFlags archiveFlags = (FormatConstants.ArchiveHeaderFlags)await ReadULongAsync(archiveStream, cancellationToken).ConfigureAwait(false);
            long metadataLength = await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false);
            // TODO: Read and potentially decrypt/deserialize Archive Metadata
            if (metadataLength > 0)
            {
                // Read metadata bytes (but don't process yet, OpenReadAsync handles this)
                archiveStream.Seek(metadataLength, SeekOrigin.Current); // Skip metadata for now
            }
            // --- End Placeholder Header ---

            OnProgressChanged(new ProgressEventArgs(bytesProcessed, totalBytesToProcess, statusMessage: "Starting extraction..."));

            // --- Read Entries or Solid Block --- 
            if ((archiveFlags & FormatConstants.ArchiveHeaderFlags.SolidArchive) == 0)
            {
                // --- Non-Solid Archive: Read entries sequentially --- 
                long currentEntryPosition = archiveStream.Position;
                while (currentEntryPosition < archiveStream.Length) // Basic loop condition
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    archiveStream.Position = currentEntryPosition; // Ensure correct position

                    // Read Entry Header
                    FormatConstants.EntryHeaderFlags entryFlags = (FormatConstants.EntryHeaderFlags)await ReadUIntAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                    
                    // Stop reading if we encounter the solid block header prematurely (shouldn't happen in non-solid)
                    // or if flags are invalid (e.g., 0), indicating potential end or corruption.
                    if (entryFlags == FormatConstants.EntryHeaderFlags.None || 
                        (entryFlags & FormatConstants.EntryHeaderFlags.IsSolidBlock) != 0)
                    {
                        // Might indicate end of entries or corruption
                        System.Diagnostics.Debug.WriteLine("Warning: Encountered unexpected entry flags while reading non-solid archive. Stopping entry read.");
                        break;
                    }

                    ushort pathLength = await ReadUShortAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                    byte[] pathBytes = await ReadBytesAsync(archiveStream, pathLength, cancellationToken).ConfigureAwait(false);
                    string relativePath = Encoding.UTF8.GetString(pathBytes);
                    long lastWriteTimeTicks = await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false); // Read LastWriteTimeUtc.Ticks
                    long uncompressedSize = await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                    long compressedSize = await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                    long headerMetadataLength = await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false); // Read metadata length
                    long entryHeaderEndPosition = archiveStream.Position; // Position *after* reading header
                    long entryDataOffset = entryHeaderEndPosition; // Data starts immediately after header in simple format

                    // Determine Checksum length
                    int checksumLength = 0;
                    bool hasChecksum = (entryFlags & FormatConstants.EntryHeaderFlags.HasChecksum) != 0;
                    if (hasChecksum && options.Checksum?.Algorithm != ChecksumAlgorithm.None)
                    {
                        IChecksumProvider? tempChecksumProvider = null;
                        try
                        {
                            tempChecksumProvider = ProviderFactory.GetChecksumProvider(options.Checksum.Algorithm, options.StreamBufferSize);
                            checksumLength = tempChecksumProvider.ChecksumLengthBytes;
                        }
                        catch (NotSupportedException) { /* Algorithm 'None' or unsupported */ }
                        finally { (tempChecksumProvider as IDisposable)?.Dispose(); }
                    }

                    long metadataOffset = 0;
                    bool hasMetadata = (entryFlags & FormatConstants.EntryHeaderFlags.HasMetadata) != 0;
                    if (hasMetadata && headerMetadataLength > 0)
                    {
                        // Metadata is stored after data and checksum
                        metadataOffset = entryDataOffset + compressedSize + checksumLength;
                    }

                    // Create ArchiveEntry object
                    ArchiveEntry entry;
                    if ((entryFlags & FormatConstants.EntryHeaderFlags.IsDirectory) != 0)
                    {
                        entry = new DirectoryArchiveEntry(relativePath);
                        // Add LastWriteTime if available (useful even for directories)
                        if (lastWriteTimeTicks > 0)
                        {
                            entry.Metadata = new FileMetadata { LastWriteTimeUtc = new DateTimeOffset(lastWriteTimeTicks, TimeSpan.Zero) };
                        }
                    }
                    else
                    {
                        FileArchiveEntry fileEntry = new(relativePath)
                        {
                            DataOffset = entryDataOffset,
                            MetadataOffset = metadataOffset,
                            MetadataLength = headerMetadataLength,
                            Flags = entryFlags, // Assign the read flags
                            IsSolidEntry = false // Explicitly mark as non-solid
                        };
                        // Add LastWriteTime from header if available and FileMetadata doesn't exist yet
                        if (lastWriteTimeTicks > 0)
                        {
                            fileEntry.Metadata ??= new FileMetadata(); // Ensure metadata object exists
                            fileEntry.Metadata.LastWriteTimeUtc ??= new DateTimeOffset(lastWriteTimeTicks, TimeSpan.Zero);
                        }
                        entry = fileEntry;
                        // We don't read FileMetadata block itself here, only its location.
                        // ReadableArchive.ReadEntryMetadataAsync will handle reading it on demand.
                    }
                    entry.UncompressedSize = uncompressedSize;
                    entry.CompressedSize = compressedSize;
                    entries.Add(entry);

                    // Skip over entry data, checksum, and metadata to get to the next header
                    long totalEntryBlockLength = compressedSize + checksumLength + headerMetadataLength;
                    currentEntryPosition = entryHeaderEndPosition + totalEntryBlockLength;

                    // Basic check for potential infinite loop or corruption
                    if (totalEntryBlockLength <= 0 && currentEntryPosition == archiveStream.Position)
                    {
                       System.Diagnostics.Debug.WriteLine($"Warning: Zero or negative entry block length detected for entry '{relativePath}'. Archive might be corrupt. Stopping entry read.");
                       break; 
                    }

                    // Ensure we don't try to seek past the end of the stream
                    if (currentEntryPosition > archiveStream.Length)
                    {
                        // This indicates a corrupt archive or error in calculation
                        throw new InvalidDataException($"Error reading archive entries. Calculated next entry position ({currentEntryPosition}) exceeds stream length ({archiveStream.Length}). Archive might be truncated or corrupt near entry '{relativePath}'.");
                    }
                }
            }
            else
            {
                // --- Solid Archive: Read solid block header and manifest --- 
                long currentPosition = archiveStream.Position;
                
                // Read the single Solid Block Entry Header
                FormatConstants.EntryHeaderFlags solidEntryFlags = (FormatConstants.EntryHeaderFlags)await ReadUIntAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                if ((solidEntryFlags & FormatConstants.EntryHeaderFlags.IsSolidBlock) == 0)
                {
                    throw new InvalidDataException("Solid archive flag is set in header, but the first entry is not a solid block entry.");
                }
                await ReadUShortAsync(archiveStream, cancellationToken).ConfigureAwait(false); // Skip path length (should be 0)
                await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false); // Skip LastWriteTimeTicks
                long totalUncompressedSize = await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                long solidBlockCompressedSize = await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false); // Skip metadata length (should be 0)
                long solidBlockDataOffset = archiveStream.Position;

                // Store the solid block info (needed later for extraction)
                // We create a placeholder entry for the block itself, not added to the user-visible list.
                var solidBlockEntryInfo = new FileArchiveEntry("__SOLID_BLOCK__") // Special internal name
                {
                    Flags = solidEntryFlags,
                    DataOffset = solidBlockDataOffset,
                    CompressedSize = solidBlockCompressedSize,
                    UncompressedSize = totalUncompressedSize,
                    IsSolidEntry = true // Mark it as the solid block itself
                };

                // Position after the solid block data (and potential checksum)
                long positionAfterSolidBlock = solidBlockDataOffset + solidBlockCompressedSize;
                // TODO: Read solid block checksum if HasChecksum flag is set for the solid block

                // Read the Manifest
                archiveStream.Position = positionAfterSolidBlock;
                long manifestLength = await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                if (manifestLength <= 0)
                {
                    throw new InvalidDataException("Solid archive manifest length is missing or invalid.");
                }
                if (manifestLength > int.MaxValue) // Protect against huge length
                {
                    throw new InvalidDataException($"Solid archive manifest length ({manifestLength}) is too large.");
                }
                byte[] manifestBytes = await ReadBytesAsync(archiveStream, (int)manifestLength, cancellationToken).ConfigureAwait(false);

                // Deserialize Manifest
                List<SolidManifestEntry> manifestEntries;
                try
                {
                    using MemoryStream manifestStream = new(manifestBytes);
                    manifestEntries = await JsonSerializer.DeserializeAsync<List<SolidManifestEntry>>(manifestStream, cancellationToken: cancellationToken) ?? new List<SolidManifestEntry>();
                }
                catch (JsonException jsonEx)
                {
                    throw new InvalidDataException("Failed to deserialize solid archive manifest.", jsonEx);
                }

                // Create ArchiveEntry list from Manifest
                foreach (var manifestEntry in manifestEntries)
                {
                    // TODO: Handle directories stored in manifest if necessary (currently assumes only files)
                    var fileEntry = new FileArchiveEntry(manifestEntry.RelativePath)
                    {
                        UncompressedSize = manifestEntry.UncompressedSize,
                        IsSolidEntry = true,
                        SolidOffset = manifestEntry.Offset,
                        // Other properties like CompressedSize, DataOffset are irrelevant for individual solid entries
                        // Flags might need to be inferred or stored in manifest if needed (e.g., per-file metadata flag?)
                        Flags = FormatConstants.EntryHeaderFlags.None // Or potentially flags from manifest? 
                    };
                    entries.Add(fileEntry);
                }
                 // The loop condition `while (currentEntryPosition < archiveStream.Length)` will naturally terminate
                 // after reading the manifest length and data in this solid block scenario.
            }

            success = true;
            // Create the readable archive instance inside the try block after success is confirmed.
            // Pass the effective options used to open the archive.
            // Pass solid block info if applicable
            FileArchiveEntry? solidBlockInfo = (archiveFlags & FormatConstants.ArchiveHeaderFlags.SolidArchive) != 0 
                ? solidBlockEntryInfo // We captured this earlier
                : null;
            return new ReadableArchive(archiveStream, entries, archiveMetadata, leaveOpen, effectiveOptions, solidBlockInfo);
        }
        finally
        {
            // If we are not leaving the stream open and the operation failed or didn't transfer ownership,
            // ensure the stream is disposed.
            if (!leaveOpen && !success)
            {
                await archiveStream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public void ExtractToDirectory(string archiveFilePath, string destinationDirectoryPath, ArchiveOptions options, IProgress<ProgressEventArgs>? progress = null, CancellationToken cancellationToken = default)
    {
        // Synchronous version of ExtractToDirectoryAsync
        ExtractToDirectoryAsync(archiveFilePath, destinationDirectoryPath, options, progress, cancellationToken).GetAwaiter().GetResult();
    }

    public async Task<IReadableArchive> OpenReadAsync(Stream archiveStream, ArchiveOptions options, bool leaveOpen = false, CancellationToken cancellationToken = default)
    {
        // 1. Validate stream and options
        if (archiveStream is null)
        {
            throw new ArgumentNullException(nameof(archiveStream));
        }

        if (!archiveStream.CanRead)
        {
            throw new ArgumentException("Archive stream must be readable.", nameof(archiveStream));
        }

        if (!archiveStream.CanSeek)
        {
            throw new ArgumentException("Archive stream must be seekable.", nameof(archiveStream));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        // Declare variables outside the try block to be accessible in finally and return
        List<ArchiveEntry> entries = new();
        ArchiveMetadata? archiveMetadata = null;
        FileArchiveEntry? solidBlockEntryInfo = null; // Holds solid block info if applicable
        bool success = false;
        ArchiveOptions effectiveOptions = options; // Use a copy or the original options

        // Ensure non-null defaults for sub-options within the effective options
        effectiveOptions.Compression ??= new CompressionOptions();
        effectiveOptions.Encryption ??= new EncryptionOptions();
        effectiveOptions.Checksum ??= new ChecksumOptions();
        effectiveOptions.ErrorCorrection ??= new ErrorCorrectionOptions();
        effectiveOptions.ArchiveMetadata ??= new ArchiveMetadata(); // Even if we read it later

        try
        {
            // 2. Read Archive Header
            // Ensure stream position is at the beginning if possible (or handle cases where it's not)
            if (archiveStream.Position != 0)
            {
                // Log warning or throw? Depends on expected behavior.
                // For now, assuming it should start at 0. Resetting position.
                archiveStream.Position = 0;
            }

            byte[] magic = await ReadBytesAsync(archiveStream, FormatConstants.MagicBytes.Length, cancellationToken).ConfigureAwait(false);
            if (!magic.SequenceEqual(FormatConstants.MagicBytes))
            {
                throw new InvalidDataException("File is not a valid Fragile archive (invalid magic bytes).");
            }

            ushort versionMajor = await ReadUShortAsync(archiveStream, cancellationToken).ConfigureAwait(false);
            ushort versionMinor = await ReadUShortAsync(archiveStream, cancellationToken).ConfigureAwait(false);

            // Check version compatibility based on options
            bool versionSupported = versionMajor == FormatConstants.FormatVersionMajor && versionMinor <= FormatConstants.FormatVersionMinor;
            if (!versionSupported && effectiveOptions.VerifyArchiveSignature)
            {
                throw new NotSupportedException($"Unsupported archive format version: {versionMajor}.{versionMinor}. Expected {FormatConstants.FormatVersionMajor}.{FormatConstants.FormatVersionMinor} or earlier minor version.");
            }

            FormatConstants.ArchiveHeaderFlags archiveFlags = (FormatConstants.ArchiveHeaderFlags)await ReadULongAsync(archiveStream, cancellationToken).ConfigureAwait(false);
            long metadataLength = await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false);

            // 3. Read Archive Metadata (if present)
            if (metadataLength > 0)
            {
                if (metadataLength > int.MaxValue) // Basic sanity check
                {
                    throw new InvalidDataException($"Archive metadata length ({metadataLength}) is too large.");
                }

                byte[] metadataBytes = await ReadBytesAsync(archiveStream, (int)metadataLength, cancellationToken).ConfigureAwait(false);

                // TODO: Handle potential encryption of archive metadata based on archiveFlags
                // if ((archiveFlags & FormatConstants.ArchiveHeaderFlags.ArchiveMetadataEncrypted) != 0) { ... decrypt ... }

                try
                {
                    using MemoryStream metaStream = new(metadataBytes);
                    archiveMetadata = await JsonSerializer.DeserializeAsync<ArchiveMetadata>(metaStream, cancellationToken: cancellationToken).ConfigureAwait(false);
                    // Assign the read metadata back to effectiveOptions for consistency?
                    // effectiveOptions.ArchiveMetadata = archiveMetadata; // Overwrite if needed
                }
                catch (JsonException jsonEx)
                {
                    // Log error or throw? Depends on policy.
                    System.Diagnostics.Debug.WriteLine($"Warning: Failed to deserialize archive metadata. Error: {jsonEx.Message}");
                    // Continue without archive metadata?
                    archiveMetadata = null; // Ensure it's null if deserialization failed
                }
            }
            else
            {
                archiveMetadata = null; // No metadata stored
            }


            // 4. Read Entries or Solid Block Manifest
            FileArchiveEntry? currentSolidBlockEntryInfo = null; // Temporary holder for solid block info

            if ((archiveFlags & FormatConstants.ArchiveHeaderFlags.SolidArchive) == 0)
            {
                // --- Non-Solid Archive: Read entries sequentially ---
                long currentEntryPosition = archiveStream.Position;
                while (currentEntryPosition < archiveStream.Length) // Basic loop condition
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    archiveStream.Position = currentEntryPosition; // Ensure correct position

                    // Read Entry Header
                    FormatConstants.EntryHeaderFlags entryFlags = (FormatConstants.EntryHeaderFlags)await ReadUIntAsync(archiveStream, cancellationToken).ConfigureAwait(false);

                    // Stop reading if we encounter the solid block header prematurely (shouldn't happen in non-solid)
                    // or if flags are invalid (e.g., 0), indicating potential end or corruption.
                    if (entryFlags == FormatConstants.EntryHeaderFlags.None ||
                        (entryFlags & FormatConstants.EntryHeaderFlags.IsSolidBlock) != 0)
                    {
                        // Might indicate end of entries or corruption
                        System.Diagnostics.Debug.WriteLine("Warning: Encountered unexpected entry flags while reading non-solid archive. Stopping entry read.");
                        break;
                    }

                    ushort pathLength = await ReadUShortAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                    byte[] pathBytes = await ReadBytesAsync(archiveStream, pathLength, cancellationToken).ConfigureAwait(false);
                    string relativePath = Encoding.UTF8.GetString(pathBytes);
                    long lastWriteTimeTicks = await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false); // Read LastWriteTimeUtc.Ticks
                    long uncompressedSize = await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                    long compressedSize = await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                    long headerMetadataLength = await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false); // Read metadata length
                    long entryHeaderEndPosition = archiveStream.Position; // Position *after* reading header
                    long entryDataOffset = entryHeaderEndPosition; // Data starts immediately after header in simple format

                    // Determine Checksum length
                    int checksumLength = 0;
                    bool hasChecksum = (entryFlags & FormatConstants.EntryHeaderFlags.HasChecksum) != 0;
                    if (hasChecksum && effectiveOptions.Checksum?.Algorithm != ChecksumAlgorithm.None)
                    {
                        IChecksumProvider? tempChecksumProvider = null;
                        try
                        {
                            tempChecksumProvider = ProviderFactory.GetChecksumProvider(effectiveOptions.Checksum.Algorithm, effectiveOptions.StreamBufferSize);
                            checksumLength = tempChecksumProvider.ChecksumLengthBytes;
                        }
                        catch (NotSupportedException) { /* Algorithm 'None' or unsupported */ }
                        finally { (tempChecksumProvider as IDisposable)?.Dispose(); }
                    }

                    long metadataOffset = 0;
                    bool hasMetadata = (entryFlags & FormatConstants.EntryHeaderFlags.HasMetadata) != 0;
                    if (hasMetadata && headerMetadataLength > 0)
                    {
                        // Metadata is stored after data and checksum
                        metadataOffset = entryDataOffset + compressedSize + checksumLength;
                    }

                    // Create ArchiveEntry object
                    ArchiveEntry entry;
                    if ((entryFlags & FormatConstants.EntryHeaderFlags.IsDirectory) != 0)
                    {
                        entry = new DirectoryArchiveEntry(relativePath);
                        // Add LastWriteTime if available (useful even for directories)
                        if (lastWriteTimeTicks > 0)
                        {
                            entry.Metadata = new FileMetadata { LastWriteTimeUtc = new DateTimeOffset(lastWriteTimeTicks, TimeSpan.Zero) };
                        }
                    }
                    else
                    {
                        FileArchiveEntry fileEntry = new(relativePath)
                        {
                            DataOffset = entryDataOffset,
                            MetadataOffset = metadataOffset,
                            MetadataLength = headerMetadataLength,
                            Flags = entryFlags, // Assign the read flags
                            IsSolidEntry = false // Explicitly mark as non-solid
                        };
                        // Add LastWriteTime from header if available and FileMetadata doesn't exist yet
                        if (lastWriteTimeTicks > 0)
                        {
                            fileEntry.Metadata ??= new FileMetadata(); // Ensure metadata object exists
                            fileEntry.Metadata.LastWriteTimeUtc ??= new DateTimeOffset(lastWriteTimeTicks, TimeSpan.Zero);
                        }
                        entry = fileEntry;
                        // We don't read FileMetadata block itself here, only its location.
                        // ReadableArchive.ReadEntryMetadataAsync will handle reading it on demand.
                    }
                    entry.UncompressedSize = uncompressedSize;
                    entry.CompressedSize = compressedSize;
                    entries.Add(entry);

                    // Skip over entry data, checksum, and metadata to get to the next header
                    long totalEntryBlockLength = compressedSize + checksumLength + headerMetadataLength;
                    currentEntryPosition = entryHeaderEndPosition + totalEntryBlockLength;

                    // Basic check for potential infinite loop or corruption
                    if (totalEntryBlockLength <= 0 && currentEntryPosition == archiveStream.Position && archiveStream.Length > currentEntryPosition)
                    {
                       System.Diagnostics.Debug.WriteLine($"Warning: Zero or negative entry block length detected for entry '{relativePath}'. Archive might be corrupt. Stopping entry read.");
                       break;
                    }

                    // Ensure we don't try to seek past the end of the stream
                    if (currentEntryPosition > archiveStream.Length)
                    {
                        // This indicates a corrupt archive or error in calculation
                        throw new InvalidDataException($"Error reading archive entries. Calculated next entry position ({currentEntryPosition}) exceeds stream length ({archiveStream.Length}). Archive might be truncated or corrupt near entry '{relativePath}'.");
                    }
                }
            }
            else
            {
                // --- Solid Archive: Read solid block header and manifest ---
                long currentPosition = archiveStream.Position;

                // Read the single Solid Block Entry Header
                FormatConstants.EntryHeaderFlags solidEntryFlags = (FormatConstants.EntryHeaderFlags)await ReadUIntAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                if ((solidEntryFlags & FormatConstants.EntryHeaderFlags.IsSolidBlock) == 0)
                {
                    throw new InvalidDataException("Solid archive flag is set in header, but the first entry is not a solid block entry.");
                }
                await ReadUShortAsync(archiveStream, cancellationToken).ConfigureAwait(false); // Skip path length (should be 0)
                await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false); // Skip LastWriteTimeTicks
                long totalUncompressedSize = await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                long solidBlockCompressedSize = await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false); // Skip metadata length (should be 0)
                long solidBlockDataOffset = archiveStream.Position;

                // Store the solid block info (needed later for extraction)
                // We create a placeholder entry for the block itself, not added to the user-visible list.
                currentSolidBlockEntryInfo = new FileArchiveEntry("__SOLID_BLOCK__") // Special internal name
                {
                    Flags = solidEntryFlags,
                    DataOffset = solidBlockDataOffset,
                    CompressedSize = solidBlockCompressedSize,
                    UncompressedSize = totalUncompressedSize,
                    IsSolidEntry = true // Mark it as the solid block itself
                };

                // Position after the solid block data (and potential checksum)
                long positionAfterSolidBlock = solidBlockDataOffset + solidBlockCompressedSize;
                // TODO: Read solid block checksum if HasChecksum flag is set for the solid block

                // Read the Manifest
                archiveStream.Position = positionAfterSolidBlock;
                long manifestLength = await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                if (manifestLength <= 0)
                {
                    throw new InvalidDataException("Solid archive manifest length is missing or invalid.");
                }
                if (manifestLength > int.MaxValue) // Protect against huge length
                {
                    throw new InvalidDataException($"Solid archive manifest length ({manifestLength}) is too large.");
                }
                byte[] manifestBytes = await ReadBytesAsync(archiveStream, (int)manifestLength, cancellationToken).ConfigureAwait(false);

                // Deserialize Manifest
                List<SolidManifestEntry> manifestEntries;
                try
                {
                    using MemoryStream manifestStream = new(manifestBytes);
                    manifestEntries = await JsonSerializer.DeserializeAsync<List<SolidManifestEntry>>(manifestStream, cancellationToken: cancellationToken).ConfigureAwait(false) ?? new List<SolidManifestEntry>();
                }
                catch (JsonException jsonEx)
                {
                    throw new InvalidDataException("Failed to deserialize solid archive manifest.", jsonEx);
                }

                // Create ArchiveEntry list from Manifest
                foreach (var manifestEntry in manifestEntries)
                {
                    // TODO: Handle directories stored in manifest if necessary (currently assumes only files)
                    var fileEntry = new FileArchiveEntry(manifestEntry.RelativePath)
                    {
                        UncompressedSize = manifestEntry.UncompressedSize,
                        IsSolidEntry = true,
                        SolidOffset = manifestEntry.Offset,
                        // Other properties like CompressedSize, DataOffset are irrelevant for individual solid entries
                        // Flags might need to be inferred or stored in manifest if needed (e.g., per-file metadata flag?)
                        Flags = FormatConstants.EntryHeaderFlags.None // Or potentially flags from manifest?
                    };
                    entries.Add(fileEntry);
                }
                 // The loop condition `while (currentEntryPosition < archiveStream.Length)` will naturally terminate
                 // after reading the manifest length and data in this solid block scenario.
            }
            
            solidBlockEntryInfo = currentSolidBlockEntryInfo; // Assign the potentially read solid block info
            success = true; // Mark operation as successful before returning
            // Create the readable archive instance inside the try block after success is confirmed.
            // Pass the effective options used to open the archive.
            // Pass solid block info if applicable
            return new ReadableArchive(archiveStream, entries, archiveMetadata, leaveOpen, effectiveOptions, solidBlockEntryInfo);
        }
        finally
        {
            // If we are not leaving the stream open and the operation failed or didn't transfer ownership,
            // ensure the stream is disposed.
            if (!leaveOpen && !success)
            {
                await archiveStream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    // --- Sync Stream-based OpenRead Implementation ---
    public IReadableArchive OpenRead(Stream archiveStream, ArchiveOptions options, bool leaveOpen = false)
    {
        // Synchronous version of OpenReadAsync(Stream...)
        // Basic sync-over-async. Warning: Can cause deadlocks in some contexts.
        return OpenReadAsync(archiveStream, options, leaveOpen).GetAwaiter().GetResult();
    }

    // --- Async FilePath-based OpenRead Implementation ---
    public async Task<IReadableArchive> OpenReadAsync(string archiveFilePath, ArchiveOptions options, CancellationToken cancellationToken = default)
    {
        // 1. Validate path and options
        if (string.IsNullOrWhiteSpace(archiveFilePath))
        {
            throw new ArgumentNullException(nameof(archiveFilePath));
        }

        if (!File.Exists(archiveFilePath))
        {
            throw new FileNotFoundException("Archive file not found.", archiveFilePath);
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        // 2. Open archive file stream
        FileStream fileStream = new(archiveFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, options.StreamBufferSize, useAsync: true);

        // Call the stream-based overload, ensuring the stream is disposed unless leaveOpen is true (which it isn't here)
        try
        {
            // Pass leaveOpen: false explicitly
            return await OpenReadAsync(fileStream, options, false, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // If OpenReadAsync(Stream...) fails, ensure the stream we opened is disposed.
            await fileStream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    // --- Sync FilePath-based OpenRead Implementation ---
    public IReadableArchive OpenRead(string archiveFilePath, ArchiveOptions options)
    {
        // Synchronous version of OpenReadAsync(string...)
        return OpenReadAsync(archiveFilePath, options).GetAwaiter().GetResult();
    }

    // --- VerifyArchiveAsync Implementation ---
    // ... (VerifyArchiveAsync seems mostly correct, may need minor adjustments based on the above changes if it uses similar patterns) ...

    // --- VerifyArchive Implementation ---
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
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(buffer); // Assume Little Endian format
        }

        await stream.WriteAsync(buffer, 0, 2, cancellationToken).ConfigureAwait(false);
    }
    private async Task WriteUIntAsync(Stream stream, uint value, CancellationToken cancellationToken)
    {
        byte[] buffer = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(buffer);
        }

        await stream.WriteAsync(buffer, 0, 4, cancellationToken).ConfigureAwait(false);
    }
    private async Task WriteLongAsync(Stream stream, long value, CancellationToken cancellationToken)
    {
        byte[] buffer = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(buffer);
        }

        await stream.WriteAsync(buffer, 0, 8, cancellationToken).ConfigureAwait(false);
    }
    private async Task WriteULongAsync(Stream stream, ulong value, CancellationToken cancellationToken)
    {
        byte[] buffer = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(buffer);
        }

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
            {
                throw new EndOfStreamException("Unexpected end of stream while reading archive data.");
            }

            offset += read;
        }
        return buffer;
    }
    private async Task<ushort> ReadUShortAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] buffer = await ReadBytesAsync(stream, 2, cancellationToken).ConfigureAwait(false);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(buffer);
        }

        return BitConverter.ToUInt16(buffer, 0);
    }
    private async Task<uint> ReadUIntAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] buffer = await ReadBytesAsync(stream, 4, cancellationToken).ConfigureAwait(false);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(buffer);
        }

        return BitConverter.ToUInt32(buffer, 0);
    }
    private async Task<long> ReadLongAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] buffer = await ReadBytesAsync(stream, 8, cancellationToken).ConfigureAwait(false);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(buffer);
        }

        return BitConverter.ToInt64(buffer, 0);
    }
    private async Task<ulong> ReadULongAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] buffer = await ReadBytesAsync(stream, 8, cancellationToken).ConfigureAwait(false);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(buffer);
        }

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
            if (bytesRead == 0)
            {
                break; // End of source stream
            }

            await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
            remaining -= bytesRead;
        }
        if (remaining > 0)
        {
            throw new EndOfStreamException("Unexpected end of entry data stream.");
        }
    }
    #endregion

    #region Helper Classes (Placeholder)
    // Simple Stream wrapper to read a subsection of a larger stream
    // A more robust implementation would handle edge cases better.
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

            if (startPosition + length > parent.Length)
            {
                throw new ArgumentOutOfRangeException("Substream exceeds parent stream length.");
            }

            _parent = parent;
            _startPosition = startPosition;
            _length = length;
            _leaveParentOpen = leaveParentOpen;
            _currentPosition = 0;
            _parent.Position = _startPosition; // Seek parent to start
        }

        public override bool CanRead => _parent.CanRead;
        public override bool CanSeek => true; // We can seek within our bounds
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position
        {
            get => _currentPosition;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long remainingInSubstream = _length - _currentPosition;
            if (remainingInSubstream <= 0)
            {
                return 0; // End of substream
            }

            int bytesToRead = (int)Math.Min(count, remainingInSubstream);

            // Ensure parent stream is at the correct position
            _parent.Position = _startPosition + _currentPosition;

            int bytesRead = _parent.Read(buffer, offset, bytesToRead);
            _currentPosition += bytesRead;
            return bytesRead;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            long remainingInSubstream = _length - _currentPosition;
            if (remainingInSubstream <= 0)
            {
                return 0;
            }

            int bytesToRead = (int)Math.Min(count, remainingInSubstream);

            // Ensure parent stream is at the correct position
            _parent.Position = _startPosition + _currentPosition;

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
            // We don't need to seek the parent stream until the next Read
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
            await base.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }
    #endregion

    #region Cryptography Helpers (Placeholder - Move to dedicated class?)
    // Simplified helper to avoid direct dependency on AesEncryptionProviderBase internals here
    private byte[] DeriveKeyFromPassword(string password, byte[] salt, int keySizeBits)
    {
        int keySizeBytes = keySizeBits / 8;
        // Use Rfc2898DeriveBytes (PBKDF2) for key derivation
        using Rfc2898DeriveBytes kdf = new(password, salt, AesEncryptionProviderBase.DefaultPbkdf2Iterations, AesEncryptionProviderBase.DefaultPbkdf2HashAlgorithm);
        return kdf.GetBytes(keySizeBytes); // Derive key of the required size
    }

    private Aes CreateAesInstance(int keySizeBits)
    {
        Aes? aes = Aes.Create();
        if (aes is null)
        {
            throw new PlatformNotSupportedException("AES algorithm is not supported on this platform.");
        }

        aes.KeySize = keySizeBits;
        aes.BlockSize = 128; // AES block size is always 128 bits
        return aes;
    }

    // Helper needed by DeflateCompressionProvider
    private static System.IO.Compression.CompressionLevel MapCompressionLevel(Core.Enums.CompressionLevel level)
    {
        return level switch
        {
            Core.Enums.CompressionLevel.Fastest => System.IO.Compression.CompressionLevel.NoCompression,
            Core.Enums.CompressionLevel.Fast => System.IO.Compression.CompressionLevel.NoCompression,
            Core.Enums.CompressionLevel.Normal => System.IO.Compression.CompressionLevel.Fastest,
            Core.Enums.CompressionLevel.High => System.IO.Compression.CompressionLevel.Fastest,
            Core.Enums.CompressionLevel.Ultra => System.IO.Compression.CompressionLevel.Optimal,
            _ => System.IO.Compression.CompressionLevel.Optimal,
        };
    }
    #endregion
}