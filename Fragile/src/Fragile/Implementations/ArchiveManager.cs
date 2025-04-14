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
using System.Security.Cryptography;
using System.IO.Compression;
using Fragile.Implementations.Providers.Encryption;

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
                                if (checksumProvider is IDisposable disposableChecksum) disposableProviders.Add(disposableChecksum);
                                checksum = await checksumProvider.ComputeChecksumAsync(streamToProcess, options.Checksum, cancellationToken).ConfigureAwait(false);
                                if (streamToProcess.CanSeek) streamToProcess.Position = 0; 
                                else throw new InvalidOperationException("Source stream must be seekable for checksum calculation.");
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
                            Stream finalOutputStream = archiveStream; // Target stream to write to
                            List<IDisposable> processingStreams = new List<IDisposable>(); // Track wrappers for disposal

                            try
                            {
                                Stream currentInputForWrapper = streamToProcess; // Start with the source file stream
                                
                                // 1. Compression Layer (if enabled)
                                if (compressionProvider != null)
                                {
                                    // Create a temporary stream to hold compressed data before potential encryption
                                    // Alternatively, chain directly if CryptoStream accepts another stream efficiently.
                                    // Chaining directly is generally preferred.
                                    
                                    // We need a stream that compressionProvider writes to, and the next layer reads from.
                                    // DeflateStream *writes* compressed data to the stream passed to its constructor.
                                    // So, the stream passed here must eventually lead to the archiveStream.

                                    // Let's refine the conceptual pipeline: 
                                    // Copy from: sourceStream
                                    // Copy to: innermostWrapperStream (which eventually writes to archiveStream)
                                    
                                    // Create the compression stream that writes to the *next* layer (or archiveStream)
                                    var compressionStream = new DeflateStream(finalOutputStream, MapCompressionLevel(options.Compression.Level), leaveOpen: true);
                                    processingStreams.Add(compressionStream);
                                    finalOutputStream = compressionStream; // Subsequent layer writes to this compression stream
                                }

                                // 2. Encryption Layer (if enabled)
                                if (encryptionProvider != null)
                                {
                                     // AesEncryptionProviderBase writes salt and IV *directly* to the stream passed to EncryptAsync.
                                     // This means we need to write salt/IV *before* creating the CryptoStream wrapper.
                                     // Let's assume the provider handles this detail, or we extract that logic.
                                     // For now, assume provider writes salt/IV *then* encrypts the rest. 
                                     // This is complex with standard CryptoStream. Needs custom provider logic or careful stream management.
                                     
                                     // Let's simulate the base provider logic here for clarity (Write salt/IV first):
                                    byte[] salt = RandomNumberGenerator.GetBytes(AesEncryptionProviderBase.DefaultSaltSizeBytes); // Need access to const
                                    byte[] iv = RandomNumberGenerator.GetBytes(AesEncryptionProviderBase.DefaultIvSizeBytes); // Need access to const
                                    byte[] key = DeriveKeyFromPassword(options.Encryption.Password!, salt, encryptionProvider is Aes256EncryptionProvider ? 256 : 128); // Simplified key derivation call

                                    await finalOutputStream.WriteAsync(salt, 0, salt.Length, cancellationToken).ConfigureAwait(false);
                                    await finalOutputStream.WriteAsync(iv, 0, iv.Length, cancellationToken).ConfigureAwait(false);
                                    
                                    using (var aes = CreateAesInstance(encryptionProvider is Aes256EncryptionProvider ? 256 : 128))
                                    {
                                        aes.Key = key;
                                        aes.IV = iv;
                                        aes.Mode = CipherMode.CBC;
                                        aes.Padding = PaddingMode.PKCS7;

                                        // CryptoStream writes encrypted data to the stream passed to its constructor (finalOutputStream)
                                        var cryptoStream = new CryptoStream(finalOutputStream, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true);
                                        processingStreams.Add(cryptoStream);
                                        finalOutputStream = cryptoStream; // Subsequent layer (CopyToAsync) writes to this crypto stream
                                    }
                                }

                                // 3. Copy Data through the Pipeline
                                // Source stream is copied TO the innermost wrapper stream (finalOutputStream)
                                await streamToProcess.CopyToAsync(finalOutputStream, bufferSize, cancellationToken).ConfigureAwait(false);

                            }
                            finally
                            {
                                // Dispose wrapper streams in reverse order. This ensures flushing through the chain.
                                processingStreams.Reverse();
                                foreach (var stream in processingStreams)
                                {
                                    // DisposeAsync if available, otherwise Dispose
                                    if (stream is IAsyncDisposable asyncDisposable) 
                                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                                    else 
                                        stream.Dispose();
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
                            // Pipeline: Archive -> SubStream -> Decryption? -> Decompression? -> Destination
                            Stream streamToReadFrom = new SubStream(archiveStream, archiveStream.Position, compressedSize, leaveParentOpen: true);
                            List<IDisposable> processingStreams = new List<IDisposable> { streamToReadFrom }; // Track streams to dispose

                            try {
                                // 1. Decryption Layer (if enabled)
                                if (decryptionProvider != null)
                                {
                                    // Need to read Salt/IV first from the *start* of the SubStream
                                    byte[] salt = await ReadBytesAsync(streamToReadFrom, AesEncryptionProviderBase.DefaultSaltSizeBytes, cancellationToken).ConfigureAwait(false); 
                                    byte[] iv = await ReadBytesAsync(streamToReadFrom, AesEncryptionProviderBase.DefaultIvSizeBytes, cancellationToken).ConfigureAwait(false);
                                    byte[] key = DeriveKeyFromPassword(options.Encryption.Password!, salt, decryptionProvider is Aes256EncryptionProvider ? 256 : 128);

                                    using (var aes = CreateAesInstance(decryptionProvider is Aes256EncryptionProvider ? 256 : 128))
                                    {
                                        aes.Key = key;
                                        aes.IV = iv;
                                        aes.Mode = CipherMode.CBC;
                                        aes.Padding = PaddingMode.PKCS7;

                                        // CryptoStream reads from underlying stream (streamToReadFrom), decrypts
                                        var cryptoStream = new CryptoStream(streamToReadFrom, aes.CreateDecryptor(), CryptoStreamMode.Read, leaveOpen: true);
                                        processingStreams.Add(cryptoStream);
                                        streamToReadFrom = cryptoStream; // Next layer reads from cryptoStream
                                    }
                                }

                                // 2. Decompression Layer (if enabled)
                                if (decompressionProvider != null)
                                {
                                    // DeflateStream reads compressed data from the underlying stream (streamToReadFrom), decompresses
                                    var decompressionStream = new DeflateStream(streamToReadFrom, CompressionMode.Decompress, leaveOpen: true);
                                    processingStreams.Add(decompressionStream);
                                    streamToReadFrom = decompressionStream; // Next layer (CopyToAsync) reads from decompressionStream
                                }

                                // 3. Copy final processed data to Destination
                                // The stream we copy *from* is the outermost wrapper (or SubStream if no wrappers)
                                // The stream we copy *to* is the destination file stream.
                                await streamToReadFrom.CopyToAsync(destinationStream, bufferSize, cancellationToken).ConfigureAwait(false);
                            }
                            finally {
                                // Dispose wrapper streams in reverse order
                                processingStreams.Reverse();
                                foreach (var stream in processingStreams)
                                {
                                    if (stream is IAsyncDisposable asyncDisposable) 
                                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                                    else 
                                        stream.Dispose();
                                }
                            }
                            
                            // No need to manually advance archiveStream, SubStream handled reading the correct amount.
                            // However, the main loop condition needs to be smarter than just Position < Length if seeking back is needed.
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

    public async Task<IReadableArchive> OpenReadAsync(string archiveFilePath, ArchiveOptions options, CancellationToken cancellationToken = default)
    {
        // 1. Validate path and options
        if (string.IsNullOrWhiteSpace(archiveFilePath))
            throw new ArgumentNullException(nameof(archiveFilePath));
        if (!File.Exists(archiveFilePath))
            throw new FileNotFoundException("Archive file not found.", archiveFilePath); 
        if (options is null)
            throw new ArgumentNullException(nameof(options));
        
        // 2. Open archive file stream
        // Open with Read access and ReadWrite share to allow reading while potentially being written (though not ideal)
        // Consider FileShare.Read for stricter read-only access.
        var archiveStream = new FileStream(archiveFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, options.StreamBufferSize, useAsync: true);
        
        // Call the stream-based overload, ensuring the stream is disposed unless leaveOpen is true (which it isn't here)
        try
        {
            return await OpenReadAsync(archiveStream, options, leaveOpen: false, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // If OpenReadAsync(Stream...) fails, ensure the stream we opened is disposed.
            await archiveStream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public IReadableArchive OpenRead(string archiveFilePath, ArchiveOptions options)
    {
        // Synchronous version of OpenReadAsync
        // Note: True sync implementation is complex with async-only providers.
        // This basic sync-over-async can lead to deadlocks in some contexts (e.g., UI threads).
        // Consider using a dedicated sync path or libraries like AsyncEx if true sync is needed.
        return OpenReadAsync(archiveFilePath, options).GetAwaiter().GetResult();
    }

    public async Task<IReadableArchive> OpenReadAsync(Stream archiveStream, ArchiveOptions options, bool leaveOpen = false, CancellationToken cancellationToken = default)
    {
        // Validate stream properties (CanRead, CanSeek)
        if (archiveStream is null)
            throw new ArgumentNullException(nameof(archiveStream));
        if (!archiveStream.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(archiveStream));
        // Seeking is required for robust format reading (e.g., central directory, skipping data)
        if (!archiveStream.CanSeek)
            throw new ArgumentException("Stream must be seekable.", nameof(archiveStream)); 
        if (options is null)
            throw new ArgumentNullException(nameof(options));

        List<ArchiveEntry> entries = new List<ArchiveEntry>();
        ArchiveMetadata? archiveMetadata = null;
        bool success = false; // Flag to control disposal on exception

        try
        { 
             // --- Read Archive Header (Placeholder Implementation) ---
            long initialPosition = archiveStream.Position; // Store initial position
            byte[] magic = await ReadBytesAsync(archiveStream, FormatConstants.MagicBytes.Length, cancellationToken).ConfigureAwait(false);
            if (!magic.SequenceEqual(FormatConstants.MagicBytes))
                throw new InvalidDataException("File is not a valid Fragile archive.");
            
            ushort versionMajor = await ReadUShortAsync(archiveStream, cancellationToken).ConfigureAwait(false);
            ushort versionMinor = await ReadUShortAsync(archiveStream, cancellationToken).ConfigureAwait(false);
            // TODO: Perform version compatibility checks
            if (versionMajor != FormatConstants.FormatVersionMajor) // Basic check
                 throw new NotSupportedException($"Archive format version {versionMajor}.{versionMinor} is not supported.");

            FormatConstants.ArchiveHeaderFlags archiveFlags = (FormatConstants.ArchiveHeaderFlags)await ReadULongAsync(archiveStream, cancellationToken).ConfigureAwait(false);
            long metadataLength = await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false);
            
            // TODO: Read actual Archive Metadata
            archiveMetadata = new ArchiveMetadata { ApplicationName = $"Fragile Archive Reader (v{versionMajor}.{versionMinor})" }; // Placeholder
            if(metadataLength > 0)
            {
                // byte[] metadataBytes = await ReadBytesAsync(archiveStream, (int)metadataLength, cancellationToken); 
                // TODO: Decrypt if ArchiveMetadataEncrypted flag is set
                // TODO: Deserialize metadata (e.g., using System.Text.Json)
                // archiveMetadata = JsonSerializer.Deserialize<ArchiveMetadata>(metadataBytes);
                archiveStream.Seek(metadataLength, SeekOrigin.Current); // Skip for now
            }
             // --- End Placeholder Header ---

            // --- Read Entries (Placeholder - assumes sequential reading) ---
            // TODO: Implement proper entry reading, potentially using a central directory if HasCentralDirectory flag is set.
            long currentEntryPosition = archiveStream.Position;
            while (currentEntryPosition < archiveStream.Length) // Basic loop condition
            {
                cancellationToken.ThrowIfCancellationRequested();
                archiveStream.Position = currentEntryPosition; // Ensure correct position

                // Read Entry Header
                FormatConstants.EntryHeaderFlags entryFlags = (FormatConstants.EntryHeaderFlags)await ReadUIntAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                ushort pathLength = await ReadUShortAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                byte[] pathBytes = await ReadBytesAsync(archiveStream, pathLength, cancellationToken).ConfigureAwait(false);
                string relativePath = Encoding.UTF8.GetString(pathBytes);
                long uncompressedSize = await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                long compressedSize = await ReadLongAsync(archiveStream, cancellationToken).ConfigureAwait(false);
                // TODO: Read actual FileMetadata if HasMetadata flag is set
                FileMetadata? fileMetadata = null;
                if ((entryFlags & FormatConstants.EntryHeaderFlags.HasMetadata) != 0) {
                    // Placeholder: Read metadata length, read bytes, deserialize
                }
                
                // Create ArchiveEntry object
                ArchiveEntry entry;
                if ((entryFlags & FormatConstants.EntryHeaderFlags.IsDirectory) != 0)
                {
                    entry = new DirectoryArchiveEntry(relativePath);
                }
                else
                {
                    entry = new FileArchiveEntry(relativePath);
                }
                entry.UncompressedSize = uncompressedSize;
                entry.CompressedSize = compressedSize;
                entry.Metadata = fileMetadata;
                // TODO: Store entry data offset within the ArchiveEntry if needed for extraction
                // entry.DataOffset = archiveStream.Position; 
                entries.Add(entry);

                // Skip over entry data, checksum, ECC data to get to the next header
                long dataAndFooterLength = compressedSize; 
                // TODO: Add checksum length if HasChecksum flag is set
                // TODO: Add ECC data length if HasErrorCorrection flag is set
                // TODO: Add FileMetadata length if HasMetadata flag is set and stored *after* data
                currentEntryPosition = archiveStream.Position + dataAndFooterLength; 
                // Add checksum length to skip
                if ((entryFlags & FormatConstants.EntryHeaderFlags.HasChecksum) != 0) {
                    // TODO: Determine actual checksum length based on algorithm stored in format
                    // For now, assume size based on option (inaccurate)
                    if(options.Checksum?.Algorithm != ChecksumAlgorithm.None) {
                       // Need a way to get length without creating provider instance
                       // int checksumLen = ProviderFactory.GetChecksumProvider(options.Checksum.Algorithm, bufferSize).ChecksumLengthBytes;
                       // currentEntryPosition += checksumLen;
                    }
                }
            }
            // --- End Placeholder Entry Reading ---
            
            success = true;
            // Create the readable archive instance. Pass the stream if it shouldn't be disposed by ReadableArchive.
            return new ReadableArchive(success && leaveOpen ? archiveStream : null, entries, archiveMetadata, leaveOpen);
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
            if (!parent.CanRead) throw new ArgumentException("Parent stream must be readable.", nameof(parent));
            if (!parent.CanSeek) throw new ArgumentException("Parent stream must be seekable.", nameof(parent));
            if (startPosition < 0) throw new ArgumentOutOfRangeException(nameof(startPosition));
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            if (startPosition + length > parent.Length) throw new ArgumentOutOfRangeException("Substream exceeds parent stream length.");

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
            if (remainingInSubstream <= 0) return 0; // End of substream

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
            if (remainingInSubstream <= 0) return 0; 

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
                throw new ArgumentOutOfRangeException(nameof(offset), "Seek position is outside the bounds of the substream.");

            _currentPosition = newPosition;
            // We don't need to seek the parent stream until the next Read
            return _currentPosition;
        }

        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
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
        using (var kdf = new Rfc2898DeriveBytes(password, salt, AesEncryptionProviderBase.DefaultPbkdf2Iterations, AesEncryptionProviderBase.DefaultPbkdf2HashAlgorithm))
        {
            return kdf.GetBytes(keySizeBytes); // Derive key of the required size
        }
    }
    
    private Aes CreateAesInstance(int keySizeBits)
    {
        var aes = Aes.Create(); 
        if (aes is null)
            throw new PlatformNotSupportedException("AES algorithm is not supported on this platform.");
        aes.KeySize = keySizeBits;
        aes.BlockSize = 128; // AES block size is always 128 bits
        return aes;
    }
    
    // Helper needed by DeflateCompressionProvider
    private static System.IO.Compression.CompressionLevel MapCompressionLevel(Core.Enums.CompressionLevel level)
    {
        return level switch
        {
            Core.Enums.CompressionLevel.Fastest => System.IO.Compression.CompressionLevel.Fastest,
            Core.Enums.CompressionLevel.Fast => System.IO.Compression.CompressionLevel.Fastest, 
            Core.Enums.CompressionLevel.Normal => System.IO.Compression.CompressionLevel.Optimal,
            Core.Enums.CompressionLevel.High => System.IO.Compression.CompressionLevel.Optimal,
            Core.Enums.CompressionLevel.Ultra => System.IO.Compression.CompressionLevel.SmallestSize,
            _ => System.IO.Compression.CompressionLevel.Optimal,
        };
    }
    #endregion
} 