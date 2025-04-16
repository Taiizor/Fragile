using Fragile.Compression;
using Fragile.Encryption;
using Fragile.ErrorCorrection;
using Fragile.Metadata;
using Fragile.Models;
using Fragile.Verification;
using System.Diagnostics;
using System.Text;

namespace Fragile.Core
{
    /// <summary>
    /// Main class of the Fragile archiving library
    /// </summary>
    public class FragileArchive : IDisposable
    {
        private readonly Dictionary<string, FragileArchiveEntry> _entries = [];
        private readonly FragileArchiveMode _mode;
        private const ushort VersionMajor = 1;
        private const ushort VersionMinor = 0;
        private FileStream? _fileStream;
        private FragileOptions _options;
        private bool _disposed = false;

        // Archive metadata storage field
        private ArchiveMetadata _archiveMetadata = new();

        // Collection storing entry metadata
        private readonly Dictionary<string, EntryMetadata> _entryMetadata = [];

        /// <summary>
        /// List of all files in the archive
        /// </summary>
        public IReadOnlyCollection<FragileArchiveEntry> Entries => _entries.Values;

        /// <summary>
        /// Path to the archive file
        /// </summary>
        public string ArchivePath { get; }

        /// <summary>
        /// Archive metadata
        /// </summary>
        public ArchiveMetadata Metadata
        {
            get => _archiveMetadata;
            set => _archiveMetadata = value ?? new ArchiveMetadata();
        }

        /// <summary>
        /// Creates a new Fragile archive or opens an existing one
        /// </summary>
        /// <param name="archivePath">Path to the archive file</param>
        /// <param name="mode">Opening mode</param>
        public FragileArchive(string archivePath, FragileArchiveMode mode = FragileArchiveMode.Read) : this(archivePath, mode, new FragileOptions())
        {
        }

        /// <summary>
        /// Creates a new Fragile archive or opens an existing one with specified options
        /// </summary>
        /// <param name="archivePath">Path to the archive file</param>
        /// <param name="mode">Opening mode</param>
        /// <param name="options">Archive options</param>
        public FragileArchive(string archivePath, FragileArchiveMode mode, FragileOptions options)
        {
            ArchivePath = archivePath ?? throw new ArgumentNullException(nameof(archivePath));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _mode = mode;

            // Ensure the archive path has the correct extension
            if (!ArchivePath.EndsWith(_options.Extension))
            {
                ArchivePath = Path.ChangeExtension(ArchivePath, _options.Extension);
            }

            if (mode == FragileArchiveMode.Create)
            {
                _fileStream = new FileStream(ArchivePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            }
            else if (mode == FragileArchiveMode.Read)
            {
                if (!File.Exists(ArchivePath))
                {
                    throw new FileNotFoundException($"Archive file not found: {ArchivePath}");
                }

                _fileStream = new FileStream(ArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                LoadArchiveEntries();
            }
            else if (mode == FragileArchiveMode.Update)
            {
                if (!File.Exists(archivePath))
                {
                    _fileStream = new FileStream(ArchivePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                }
                else
                {
                    _fileStream = new FileStream(ArchivePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    LoadArchiveEntries();
                }
            }
        }

        /// <summary>
        /// Creates a new Fragile archive asynchronously
        /// </summary>
        /// <param name="archivePath">Path to the archive file</param>
        /// <param name="options">Archive options</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public static async Task<FragileArchive> CreateAsync(string archivePath, FragileOptions? options = null)
        {
            options ??= new FragileOptions();
            FragileArchive archive = new(archivePath, FragileArchiveMode.Create, options);
            return archive;
        }

        /// <summary>
        /// Opens an existing Fragile archive asynchronously
        /// </summary>
        /// <param name="archivePath">Path to the archive file</param>
        /// <param name="options">Archive options</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public static async Task<FragileArchive> OpenAsync(string archivePath, FragileOptions? options = null)
        {
            options ??= new FragileOptions();
            FragileArchive archive = new(archivePath, FragileArchiveMode.Read, options);
            return archive;
        }

        /// <summary>
        /// Adds a file to the archive
        /// </summary>
        /// <param name="filePath">Path to the file to add</param>
        /// <param name="entryPath">Path inside the archive (if not specified, the file name is used)</param>
        /// <returns>The added archive entry</returns>
        public FragileArchiveEntry AddFile(string filePath, string? entryPath = null)
        {
            if (_mode == FragileArchiveMode.Read)
            {
                throw new InvalidOperationException("Cannot add file to archive in read-only mode");
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            entryPath ??= Path.GetFileName(filePath);
            entryPath = NormalizePath(entryPath);

            FileInfo fileInfo = new(filePath);
            FragileArchiveEntry entry = new()
            {
                Path = entryPath,
                Size = fileInfo.Length,
                CompressedSize = 0,
                LastModified = fileInfo.LastWriteTimeUtc,
                SourcePath = filePath,
                IsDirectory = false
            };

            _entries[entryPath] = entry;
            return entry;
        }

        /// <summary>
        /// Adds a file to the archive asynchronously
        /// </summary>
        /// <param name="filePath">Path to the file to add</param>
        /// <param name="entryPath">Path inside the archive (if not specified, the file name is used)</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task<FragileArchiveEntry> AddFileAsync(string filePath, string? entryPath = null)
        {
            // No special async operation here, just a wrapper for API consistency
            return AddFile(filePath, entryPath);
        }

        /// <summary>
        /// Adds a directory to the archive
        /// </summary>
        /// <param name="directoryPath">Path to the directory to add</param>
        /// <param name="entryPath">Root path inside the archive (if not specified, the directory name is used)</param>
        /// <param name="recursive">Add subdirectories as well?</param>
        /// <returns>Number of added files</returns>
        public int AddDirectory(string directoryPath, string? entryPath = null, bool recursive = true)
        {
            if (_mode == FragileArchiveMode.Read)
            {
                throw new InvalidOperationException("Cannot add directory to archive in read-only mode");
            }

            if (!Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
            }

            entryPath ??= Path.GetFileName(directoryPath);
            entryPath = NormalizePath(entryPath);

            // Add the directory itself
            DirectoryInfo dirInfo = new(directoryPath);
            FragileArchiveEntry dirEntry = new()
            {
                Path = entryPath,
                Size = 0,
                CompressedSize = 0,
                LastModified = dirInfo.LastWriteTimeUtc,
                IsDirectory = true
            };

            _entries[entryPath] = dirEntry;

            // Add files
            int count = 1; // The directory itself
            foreach (string? file in Directory.GetFiles(directoryPath))
            {
                string fileName = Path.GetFileName(file);
                string fileEntryPath = string.IsNullOrEmpty(entryPath)
                    ? fileName
                    : Path.Combine(entryPath, fileName).Replace('\\', '/');

                AddFile(file, fileEntryPath);
                count++;
            }

            // Add subdirectories
            if (recursive)
            {
                foreach (string? subDir in Directory.GetDirectories(directoryPath))
                {
                    string subDirName = Path.GetFileName(subDir);
                    string subDirEntryPath = string.IsNullOrEmpty(entryPath)
                        ? subDirName
                        : Path.Combine(entryPath, subDirName).Replace('\\', '/');

                    count += AddDirectory(subDir, subDirEntryPath, true);
                }
            }

            return count;
        }

        /// <summary>
        /// Adds a directory to the archive asynchronously
        /// </summary>
        /// <param name="directoryPath">Path to the directory to add</param>
        /// <param name="entryPath">Root path inside the archive (if not specified, the directory name is used)</param>
        /// <param name="recursive">Add subdirectories as well?</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task<int> AddDirectoryAsync(string directoryPath, string? entryPath = null, bool recursive = true)
        {
            // No special async operation here, just a wrapper for API consistency
            return AddDirectory(directoryPath, entryPath, recursive);
        }

        /// <summary>
        /// Extracts the specified file from the archive to the target directory
        /// </summary>
        /// <param name="entryPath">Path of the file/directory to extract inside the archive</param>
        /// <param name="destinationPath">Target file/directory path</param>
        public void Extract(string entryPath, string destinationPath)
        {
            if (_mode == FragileArchiveMode.Create)
            {
                throw new InvalidOperationException("Cannot extract in create mode");
            }

            entryPath = NormalizePath(entryPath);

            if (!_entries.TryGetValue(entryPath, out FragileArchiveEntry? entry))
            {
                throw new FileNotFoundException($"Entry not found in archive: {entryPath}");
            }

            if (entry.IsDirectory)
            {
                // Create directory
                Directory.CreateDirectory(destinationPath);

                // Extract all files and subdirectories in this directory
                foreach (FragileArchiveEntry? childEntry in _entries.Values.Where(e => e.Path.StartsWith(entryPath + "/")))
                {
#if NET48_OR_GREATER || NETSTANDARD2_0
                    string relativePath = childEntry.Path.Substring(entryPath.Length + 1);
#else
                    string relativePath = childEntry.Path[(entryPath.Length + 1)..];
#endif
                    string childDestPath = Path.Combine(destinationPath, relativePath);

                    if (childEntry.IsDirectory)
                    {
                        Directory.CreateDirectory(childDestPath);
                    }
                    else
                    {
                        ExtractFile(childEntry, childDestPath);
                    }
                }
            }
            else
            {
                ExtractFile(entry, destinationPath);
            }
        }

        /// <summary>
        /// Extracts the specified file from the archive to the target directory asynchronously
        /// </summary>
        /// <param name="entryPath">Path of the file/directory to extract inside the archive</param>
        /// <param name="destinationPath">Target file/directory path</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task ExtractAsync(string entryPath, string destinationPath)
        {
            if (_mode == FragileArchiveMode.Create)
            {
                throw new InvalidOperationException("Cannot extract in create mode");
            }

            entryPath = NormalizePath(entryPath);

            if (!_entries.TryGetValue(entryPath, out FragileArchiveEntry? entry))
            {
                throw new FileNotFoundException($"Entry not found in archive: {entryPath}");
            }

            if (entry.IsDirectory)
            {
                // Create directory
                Directory.CreateDirectory(destinationPath);

                // Extract all files and subdirectories in this directory
                foreach (FragileArchiveEntry? childEntry in _entries.Values.Where(e => e.Path.StartsWith(entryPath + "/")))
                {
#if NET48_OR_GREATER || NETSTANDARD2_0
                    string relativePath = childEntry.Path.Substring(entryPath.Length + 1);
#else
                    string relativePath = childEntry.Path[(entryPath.Length + 1)..];
#endif
                    string childDestPath = Path.Combine(destinationPath, relativePath);

                    if (childEntry.IsDirectory)
                    {
                        Directory.CreateDirectory(childDestPath);
                    }
                    else
                    {
                        await ExtractFileAsync(childEntry, childDestPath);
                    }
                }
            }
            else
            {
                await ExtractFileAsync(entry, destinationPath);
            }
        }

        /// <summary>
        /// Extracts all files from the archive to the target directory
        /// </summary>
        /// <param name="destinationPath">Target directory path</param>
        public void ExtractAll(string destinationPath)
        {
            if (_mode == FragileArchiveMode.Create)
            {
                throw new InvalidOperationException("Cannot extract in create mode");
            }

            // Create the target directory if it doesn't exist
            Directory.CreateDirectory(destinationPath);

            // Extract all entries
            foreach (FragileArchiveEntry entry in _entries.Values)
            {
                string targetPath = Path.Combine(destinationPath, entry.Path);

                if (entry.IsDirectory)
                {
                    Directory.CreateDirectory(targetPath);
                }
                else
                {
                    // Ensure the directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

                    // Extract the file
                    ExtractFile(entry, targetPath);
                }
            }
        }

        /// <summary>
        /// Extracts all files from the archive to the target directory asynchronously
        /// </summary>
        /// <param name="destinationPath">Target directory path</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task ExtractAllAsync(string destinationPath)
        {
            if (_mode == FragileArchiveMode.Create)
            {
                throw new InvalidOperationException("Cannot extract in create mode");
            }

            // If the target path is empty or null, throw an error
            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                throw new ArgumentException("Destination path cannot be null or empty", nameof(destinationPath));
            }

            // Create the target directory if it doesn't exist
            Directory.CreateDirectory(destinationPath);

            // Extract all entries
            foreach (FragileArchiveEntry entry in _entries.Values)
            {
                // Skip invalid paths
                if (string.IsNullOrWhiteSpace(entry.Path) || entry.Path.Contains("\0"))
                {
                    Debug.WriteLine($"Skipping entry with invalid path: '{entry.Path}'");
                    continue;
                }

                string targetPath = Path.Combine(destinationPath, entry.Path);

                if (entry.IsDirectory)
                {
                    Directory.CreateDirectory(targetPath);
                }
                else
                {
                    try
                    {
                        // Ensure the directory exists
                        string? directory = Path.GetDirectoryName(targetPath);
                        if (!string.IsNullOrEmpty(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        // Extract the file
                        await ExtractFileAsync(entry, targetPath);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to extract file {entry.Path}", ex);
                    }
                }
            }
        }

        /// <summary>
        /// Saves the archive and compresses all file contents to the archive
        /// </summary>
        public void Save()
        {
            if (_mode == FragileArchiveMode.Read)
            {
                throw new InvalidOperationException("Cannot save archive in read-only mode");
            }

            SaveAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Saves the archive and compresses all file contents to the archive asynchronously
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task SaveAsync()
        {
            if (_mode == FragileArchiveMode.Read)
            {
                throw new InvalidOperationException("Cannot save archive in read-only mode");
            }

            EnsureFileStream();

            // If error correction is enabled, create a temporary file first
            string tempFilePath = null;
            Stream outputStream;

            if (_options.EnableErrorCorrection && _options.ErrorCorrectionLevel > 0)
            {
                tempFilePath = $"Fragile_{Guid.NewGuid()}";
                outputStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            }
            else
            {
                // Recreate the archive directly
                _fileStream!.SetLength(0);
                _fileStream.Position = 0;
                outputStream = _fileStream;
            }

            try
            {
                // Write file header
                using (BinaryWriter writer = new(outputStream, Encoding.UTF8, true))
                {
                    // File signature
                    writer.Write(Encoding.ASCII.GetBytes(_options.Signature));

                    // Version
                    writer.Write(VersionMajor);
                    writer.Write(VersionMinor);

                    // Options
                    byte optionFlags = 0;

                    // Set option flags based on enabled features
                    if (_options.EnableEncryption)
                    {
                        optionFlags |= 0x01;
                    }

                    if (_options.EnableChecksumVerification)
                    {
                        optionFlags |= 0x02;
                    }

                    if (_options.EnableErrorCorrection)
                    {
                        optionFlags |= 0x04;
                    }

                    if (_options.IncludeMetadata)
                    {
                        optionFlags |= 0x08;
                    }

                    if (_options.UseSolidCompression)
                    {
                        optionFlags |= 0x10;
                    }

                    writer.Write(optionFlags);

                    // Compression algorithm
                    writer.Write((byte)_options.CompressionAlgorithm);

                    // Reserve space for metadata offset
                    long metadataOffsetPosition = outputStream.Position;
                    writer.Write((long)0);

                    // Number of entries
                    writer.Write(_entries.Count);

                    // Reserve space for central directory offset
                    long centralDirOffsetPosition = outputStream.Position;
                    writer.Write((long)0);

                    // Process each entry
                    foreach (FragileArchiveEntry entry in _entries.Values)
                    {
                        // Record position for this entry
                        entry.HeaderOffset = outputStream.Position;

                        // Entry path - Write length first
                        byte[] pathBytes = Encoding.UTF8.GetBytes(entry.Path);
                        writer.Write(pathBytes.Length); // Write path length
                        writer.Write(pathBytes); // Write path bytes

                        // Entry metadata
                        writer.Write(entry.Size);
                        writer.Write(entry.LastModified.ToBinary());
                        writer.Write(entry.IsDirectory);

                        // Reserve space for compressed size and data offset
                        long compressedSizePosition = outputStream.Position;
                        writer.Write((long)0); // Placeholder for CompressedSize
                        long dataOffsetPosition = outputStream.Position;
                        writer.Write((long)0); // Placeholder for PositionOffset

                        // Now, write the actual data if it's a file and not already in memory
                        if (!entry.IsDirectory && entry.Data == null)
                        {
                            // Data will be written later in this loop for files
                        }
                        else if (entry.Data != null)
                        {
                            // Data is already in memory, will be written later
                            // We still needed the header above
                        }
                        // No data needed for directories in this section
                    }

                    // Now write the actual file data
                    foreach (FragileArchiveEntry entry in _entries.Values)
                    {
                        if (entry.IsDirectory)
                        {
                            continue; // Skip directories for data writing
                        }

                        // Get current position as the start of data for this entry
                        entry.PositionOffset = outputStream.Position;

                        // Update PositionOffset in the header we wrote earlier
                        long currentPosition = outputStream.Position;
                        outputStream.Position = entry.HeaderOffset + sizeof(int) + Encoding.UTF8.GetByteCount(entry.Path) + sizeof(long) + sizeof(long) + sizeof(bool) + sizeof(long);
                        writer.Write(entry.PositionOffset);
                        outputStream.Position = currentPosition; // Restore position

                        // Write data
                        if (entry.Data != null)
                        {
                            // Write data from memory
                            entry.CompressedSize = entry.Data.Length; // Assume data in memory is already compressed/processed
#if NET48_OR_GREATER || NETSTANDARD2_0
                            await outputStream.WriteAsync(entry.Data, 0, entry.Data.Length);
#else
                            await outputStream.WriteAsync(entry.Data.AsMemory(0, entry.Data.Length));
#endif
                        }
                        else if (!string.IsNullOrEmpty(entry.SourcePath) && File.Exists(entry.SourcePath))
                        {
                            // Compress and write from file
                            long fileDataStartPosition = outputStream.Position;
                            using FileStream fileStream = new(entry.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                            CompressionProvider compressionProvider = CompressionProvider.Create(_options.CompressionAlgorithm, _options.CompressionLevel, _options.UseParallelProcessing, _options.MaxThreads);

                            // --- BEGIN RE-INSERTED COMPRESSION/ENCRYPTION LOGIC ---
                            if (_options.EnableEncryption)
                            {
                                // Create encryption provider
                                EncryptionProvider encryptionProvider = EncryptionProvider.Create(_options.EncryptionMethod, _options.Password);

                                // Set encryption properties on the entry
                                entry.IsEncrypted = true;
                                entry.EncryptionMethod = _options.EncryptionMethod;

                                // Compress first, then encrypt
                                using MemoryStream compressedStream = new();

                                // Compress the file to temporary stream
                                await compressionProvider.CompressAsync(fileStream, compressedStream, _options.Progress, _options.CancellationToken);

                                // Reset position for reading
                                compressedStream.Position = 0;

                                // Encrypt the compressed data to the output
                                await encryptionProvider.EncryptAsync(compressedStream, outputStream, _options.Progress, _options.CancellationToken);
                            }
                            else
                            {
                                // No encryption, just compress the file
                                entry.IsEncrypted = false;
                                entry.EncryptionMethod = EncryptionMethod.None;

                                await compressionProvider.CompressAsync(fileStream, outputStream, _options.Progress, _options.CancellationToken);
                            }
                            // Ensure data is written before calculating size based on position
                            await outputStream.FlushAsync(_options.CancellationToken);
                            // --- END RE-INSERTED COMPRESSION/ENCRYPTION LOGIC ---

                            // After compression/encryption:
                            entry.CompressedSize = outputStream.Position - fileDataStartPosition; // Calculate actual compressed size written
                        }
                        else
                        {
                            throw new FileNotFoundException($"Source file not found for entry {entry.Path}: {entry.SourcePath}");
                        }

                        // Update CompressedSize in the header
                        currentPosition = outputStream.Position;
                        outputStream.Position = entry.HeaderOffset + sizeof(int) + Encoding.UTF8.GetByteCount(entry.Path) + sizeof(long) + sizeof(long) + sizeof(bool);
                        writer.Write(entry.CompressedSize);
                        outputStream.Position = currentPosition; // Restore position

                        // Write checksum if enabled
                        if (_options.EnableChecksumVerification)
                        {
                            long positionAfterData = outputStream.Position;
                            VerificationProvider verificationProvider = VerificationProvider.Create(_options.ChecksumAlgorithm);
                            byte[] dataForChecksum = new byte[entry.CompressedSize];
                            outputStream.Position = entry.PositionOffset;
                            // Use ReadExactlyAsync to ensure all bytes are read
                            await ReadExactlyAsync(outputStream, dataForChecksum, 0, dataForChecksum.Length, _options.CancellationToken);
                            using MemoryStream checksumStream = new(dataForChecksum);
                            byte[] checksumBytes = await verificationProvider.CalculateChecksumAsync(checksumStream, _options.Progress, _options.CancellationToken);
                            outputStream.Position = positionAfterData; // Seek back to end
#if NET48_OR_GREATER || NETSTANDARD2_0
                            await outputStream.WriteAsync(checksumBytes, 0, checksumBytes.Length);
#else
                            await outputStream.WriteAsync(checksumBytes);
#endif
                        }
                    }

                    // Write central directory
                    long centralDirOffset = outputStream.Position;

                    // Update central directory offset
                    outputStream.Position = centralDirOffsetPosition;
                    writer.Write(centralDirOffset);
                    outputStream.Position = centralDirOffset;

                    // Write each entry's info in the central directory
                    foreach (FragileArchiveEntry entry in _entries.Values)
                    {
                        byte[] pathBytes = Encoding.UTF8.GetBytes(entry.Path);
                        writer.Write(pathBytes.Length); // Path Length
                        writer.Write(pathBytes);       // Path Bytes
                        writer.Write(entry.HeaderOffset); // Header Offset
                        writer.Write(entry.PositionOffset); // Position Offset (final value)
                        writer.Write(entry.Size);            // Original Size
                        writer.Write(entry.CompressedSize); // Compressed Size (final value)
                        writer.Write(entry.IsDirectory);     // Is Directory
                        writer.Write(entry.IsEncrypted);     // Is Encrypted
                        if (entry.IsEncrypted)
                        {
                            writer.Write((byte)entry.EncryptionMethod); // Encryption Method
                        }
                        else
                        {
                            writer.Write((byte)0); // No encryption method
                        }
                    }

                    // Write metadata section if enabled
                    if (_options.IncludeMetadata)
                    {
                        long metadataOffset = outputStream.Position;

                        // Update metadata offset in the header
                        outputStream.Position = metadataOffsetPosition;
                        writer.Write(metadataOffset);
                        outputStream.Position = metadataOffset;

                        // Update archive metadata with current date
                        _archiveMetadata.LastModifiedTime = DateTime.UtcNow;

                        // Serialize and write archive metadata
                        string archiveMetadataJson = _archiveMetadata.ToJson();
                        byte[] archiveMetadataBytes = Encoding.UTF8.GetBytes(archiveMetadataJson);
                        writer.Write(archiveMetadataBytes.Length);
                        writer.Write(archiveMetadataBytes);

                        // Write entry metadata count
                        writer.Write(_entryMetadata.Count);

                        // Write each entry metadata
                        foreach (KeyValuePair<string, EntryMetadata> kvp in _entryMetadata)
                        {
                            // Entry path
                            byte[] pathBytes = Encoding.UTF8.GetBytes(kvp.Key);
                            writer.Write(pathBytes.Length);
                            writer.Write(pathBytes);

                            // Entry metadata
                            string entryMetadataJson = kvp.Value.ToJson();
                            byte[] entryMetadataBytes = Encoding.UTF8.GetBytes(entryMetadataJson);
                            writer.Write(entryMetadataBytes.Length);
                            writer.Write(entryMetadataBytes);
                        }
                    }
                    else
                    {
                        // No metadata, write 0 for metadata offset
                        outputStream.Position = metadataOffsetPosition;
                        writer.Write((long)0);
                        outputStream.Position = outputStream.Length;
                    }
                }

                // If error correction is enabled, apply it
                if (_options.EnableErrorCorrection && _options.ErrorCorrectionLevel > 0)
                {
                    // Create a error correction provider
                    ErrorCorrectionProvider errorCorrection = ErrorCorrectionProvider.Create(_options);

                    // Reset the temp file position
                    outputStream.Position = 0;

                    // Reset the archive file
                    _fileStream!.SetLength(0);
                    _fileStream.Position = 0;

                    // Add error correction data
                    await errorCorrection.AddErrorCorrectionAsync(outputStream, _fileStream, _options.Progress, _options.CancellationToken);

                    // Cleanup
                    outputStream.Close();
                    File.Delete(tempFilePath);
                }

                // Flush to disk
                await _fileStream!.FlushAsync(_options.CancellationToken);
            }
            catch
            {
                // Cleanup in case of error
                if (tempFilePath != null)
                {
                    if (outputStream != null && outputStream != _fileStream)
                    {
                        outputStream.Close();
                    }

                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                    }
                }

                throw;
            }
        }

        /// <summary>
        /// Releases resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _fileStream?.Dispose();
                }

                _fileStream = null;
                _disposed = true;
            }
        }

        private void LoadArchiveEntries()
        {
            EnsureFileStream();

            _entries.Clear();
            _entryMetadata.Clear();
            _fileStream!.Position = 0;

            using BinaryReader reader = new(_fileStream, Encoding.UTF8, true);
            // Check file signature
            string signature = Encoding.ASCII.GetString(reader.ReadBytes(_options.Signature.Length));

            if (signature != _options.Signature)
            {
                throw new InvalidDataException("Invalid Fragile archive file signature");
            }

            // Version check
            ushort majorVersion = reader.ReadUInt16();
            ushort minorVersion = reader.ReadUInt16();

            if (majorVersion > VersionMajor)
            {
                throw new InvalidDataException($"Unsupported Fragile archive file version: {majorVersion}.{minorVersion}");
            }

            // Read options flags
            byte optionFlags = reader.ReadByte();

            // Extract settings from flags
            bool isEncrypted = (optionFlags & 0x01) != 0;
            bool hasChecksum = (optionFlags & 0x02) != 0;
            bool hasErrorCorrection = (optionFlags & 0x04) != 0;
            bool hasMetadata = (optionFlags & 0x08) != 0;
            bool useSolidCompression = (optionFlags & 0x10) != 0;

            // Update options from file flags
            if (isEncrypted)
            {
                _options.EnableEncryption = true;
            }

            if (hasChecksum)
            {
                _options.EnableChecksumVerification = true;
            }

            if (hasErrorCorrection)
            {
                _options.EnableErrorCorrection = true;
            }

            if (hasMetadata)
            {
                _options.IncludeMetadata = true;
            }

            if (useSolidCompression)
            {
                _options.UseSolidCompression = true;
            }

            // Read compression algorithm
            byte compressionAlgorithm = reader.ReadByte();

            // Read metadata offset
            long metadataOffset = reader.ReadInt64();

            // Entry count
            int entryCount = reader.ReadInt32();

            // Data start position
            long dataPosition = reader.ReadInt64();

            // Read file entries
            for (int i = 0; i < entryCount; i++)
            {
                // Read path length first, then path bytes
                int pathLength = reader.ReadInt32();
                byte[] pathBytes = reader.ReadBytes(pathLength);
                string entryPath = Encoding.UTF8.GetString(pathBytes);

                FragileArchiveEntry entry = new()
                {
                    Path = entryPath, // 1. Path
                    Size = reader.ReadInt64(), // 2. Size
                    LastModified = TryParseDateTime(reader.ReadInt64()), // 3. LastModified
                    IsDirectory = reader.ReadBoolean(), // 4. IsDirectory

                    // Read placeholders for CompressedSize and PositionOffset (these will be updated from Central Directory)
                    CompressedSize = reader.ReadInt64(), // 5. CompressedSize (placeholder)
                    PositionOffset = reader.ReadInt64() // 6. PositionOffset (placeholder)
                };

                _entries[entryPath] = entry;
            }

            // Read central directory
            _fileStream!.Position = dataPosition; // dataPosition should point to the Central Directory start

            // Update each entry with its specific encryption details from the central directory
            for (int i = 0; i < entryCount; i++)
            {
                // Read path length first, then path bytes
                int pathLength = reader.ReadInt32();
                byte[] pathBytes = reader.ReadBytes(pathLength);
                string path = Encoding.UTF8.GetString(pathBytes);

                if (!_entries.TryGetValue(path, out FragileArchiveEntry? entry))
                {
                    // Skip this entry if not found (shouldn't happen)
                    // Need to read the remaining data for this entry to advance the stream correctly
                    reader.ReadInt64(); // HeaderOffset
                    reader.ReadInt64(); // PositionOffset
                    reader.ReadInt64(); // Size
                    reader.ReadInt64(); // CompressedSize
                    reader.ReadBoolean(); // IsDirectory
                    reader.ReadBoolean(); // IsEncrypted
                    reader.ReadByte(); // EncryptionMethod
                    continue;
                }

                // Update entry properties from central directory
                entry.HeaderOffset = reader.ReadInt64(); // 1. HeaderOffset
                entry.PositionOffset = reader.ReadInt64(); // 2. PositionOffset (Update from placeholder)
                entry.Size = reader.ReadInt64(); // 3. Size (Can optionally verify against header value)
                entry.CompressedSize = reader.ReadInt64(); // 4. CompressedSize (Update from placeholder)
                entry.IsDirectory = reader.ReadBoolean(); // 5. IsDirectory (Can optionally verify)

                // Read encryption info
                bool isEntryEncrypted = reader.ReadBoolean(); // 6. IsEncrypted
                byte encryptionMethodByte = reader.ReadByte(); // 7. EncryptionMethod

                // Update entry encryption info
                entry.IsEncrypted = isEntryEncrypted;
                if (isEntryEncrypted)
                {
                    entry.EncryptionMethod = (EncryptionMethod)encryptionMethodByte;
                }
                else
                {
                    entry.EncryptionMethod = EncryptionMethod.None;
                }
            }

            // Read metadata if included and offset is valid
            if (hasMetadata && metadataOffset > 0)
            {
                try
                {
                    // Position at metadata section
                    _fileStream!.Position = metadataOffset;

                    // Read archive metadata
                    int archiveMetadataLength = reader.ReadInt32();
                    byte[] archiveMetadataBytes = reader.ReadBytes(archiveMetadataLength);
                    string archiveMetadataJson = Encoding.UTF8.GetString(archiveMetadataBytes);
                    _archiveMetadata = ArchiveMetadata.FromJson(archiveMetadataJson);

                    // Read entry metadata count
                    int entryMetadataCount = reader.ReadInt32();

                    // Read each entry metadata
                    for (int i = 0; i < entryMetadataCount; i++)
                    {
                        // Entry path
                        int pathLength = reader.ReadInt32();
                        byte[] pathBytes = reader.ReadBytes(pathLength);
                        string path = Encoding.UTF8.GetString(pathBytes);

                        // Entry metadata
                        int metadataLength = reader.ReadInt32();
                        byte[] metadataBytes = reader.ReadBytes(metadataLength);
                        string metadataJson = Encoding.UTF8.GetString(metadataBytes);
                        EntryMetadata metadata = EntryMetadata.FromJson(metadataJson);

                        // Add to metadata dictionary
                        _entryMetadata[path] = metadata;
                    }
                }
                catch (Exception ex)
                {
                    // If metadata reading fails, log error but continue
                    Debug.WriteLine($"Error reading metadata: {ex.Message}");

                    // Reset metadata to defaults
                    _archiveMetadata = new ArchiveMetadata();
                    _entryMetadata.Clear();
                }
            }
        }

        private void ExtractFile(FragileArchiveEntry entry, string destinationPath)
        {
            ExtractFileAsync(entry, destinationPath).GetAwaiter().GetResult();
        }

        private async Task ExtractFileAsync(FragileArchiveEntry entry, string destinationPath)
        {
            EnsureFileStream();

            try
            {
                // Throw an error if the destination path is invalid
                if (string.IsNullOrWhiteSpace(destinationPath) || destinationPath.Contains("\0"))
                {
                    throw new ArgumentException($"Invalid destination path: '{destinationPath}'", nameof(destinationPath));
                }

                // Create directory if it doesn't exist
                string? directory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Position stream at file data using PositionOffset
                _fileStream!.Position = entry.PositionOffset;

                // Read compressed data
                byte[] compressedData = new byte[entry.CompressedSize];
                // Use ReadExactlyAsync to ensure all bytes are read
                await ReadExactlyAsync(_fileStream, compressedData, 0, (int)entry.CompressedSize, _options.CancellationToken);

                // Create a memory stream with the compressed data
                using MemoryStream compressedStream = new(compressedData);

                // Check if checksum verification is enabled
                if (_options.EnableChecksumVerification)
                {
                    // Verify the integrity of the compressed data
                    VerificationProvider verificationProvider = VerificationProvider.Create(_options.ChecksumAlgorithm);

                    // Read the stored checksum (assuming it's stored right after the compressed data)
                    byte[] storedChecksum = new byte[verificationProvider.GetChecksumSize()];

#if NET48_OR_GREATER || NETSTANDARD2_0
                    await _fileStream.ReadAsync(storedChecksum, 0, storedChecksum.Length);
#else
                    await _fileStream.ReadAsync(storedChecksum);
#endif

                    // Verify the checksum
                    bool isValid = await verificationProvider.VerifyChecksumAsync(compressedStream, storedChecksum, _options.Progress, _options.CancellationToken);

                    if (!isValid)
                    {
                        throw new InvalidDataException($"Checksum verification failed for {entry.Path}");
                    }

                    // Reset stream position
                    compressedStream.Position = 0;
                }

                // Create the destination file
                using FileStream outputFile = new(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

                // Create an appropriate decompressor
                CompressionProvider compressionProvider = CompressionProvider.Create(_options.CompressionAlgorithm, _options.CompressionLevel, _options.UseParallelProcessing, _options.MaxThreads);

                // Check if the entry is encrypted
                bool isEncrypted = entry.IsEncrypted;

                // If entry is encrypted but no password is provided, throw an exception
                if (isEncrypted && string.IsNullOrEmpty(_options.Password))
                {
                    throw new InvalidOperationException($"Entry {entry.Path} is encrypted but no password was provided. Set the Password property in FragileOptions.");
                }

                // If entry is encrypted but EnableEncryption is false, still attempt to decrypt
                // using the provided encryption method and password
                if (isEncrypted && !_options.EnableEncryption)
                {
                    _options.EnableEncryption = true;

                    // If the encryption method is not set in options, use the one from the entry
                    if (_options.EncryptionMethod == EncryptionMethod.None)
                    {
                        _options.EncryptionMethod = entry.EncryptionMethod;
                    }
                }

                if (isEncrypted && _options.EnableEncryption)
                {
                    // Create encryption provider for decryption
                    EncryptionProvider encryptionProvider = EncryptionProvider.Create(entry.EncryptionMethod != EncryptionMethod.None ? entry.EncryptionMethod : _options.EncryptionMethod, _options.Password);

                    // First decrypt, then decompress
                    using MemoryStream decryptedStream = new();

                    // Decrypt the data
                    await encryptionProvider.DecryptAsync(compressedStream, decryptedStream, _options.Progress, _options.CancellationToken);

                    // Reset position for reading
                    decryptedStream.Position = 0;

                    // Decompress the decrypted data
                    await compressionProvider.DecompressAsync(decryptedStream, outputFile, _options.Progress, _options.CancellationToken);
                }
                else
                {
                    // No encryption, just decompress the data
                    await compressionProvider.DecompressAsync(compressedStream, outputFile, _options.Progress, _options.CancellationToken);
                }
            }
            catch (Exception)
            {
                // If error correction is enabled, try to repair
                if (_options.EnableErrorCorrection && _options.ErrorCorrectionLevel > 0)
                {
                    await TryRepairAndExtractFileAsync(entry, destinationPath);
                }
                else
                {
                    throw;
                }
            }
        }

        private async Task TryRepairAndExtractFileAsync(FragileArchiveEntry entry, string destinationPath)
        {
            // Create error correction provider
            ErrorCorrectionProvider errorCorrection = ErrorCorrectionProvider.Create(_options);

            // Open the archive file for temporary reading
            using FileStream archiveStream = new(ArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            // Create output file
            using FileStream outputFile = new(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

            // Try to correct errors and extract
            (long bytesWritten, int bytesRepaired) = await errorCorrection.CorrectErrorsAsync(
                archiveStream,
                outputFile,
                (position, count) =>
                {
                    // Report repair progress if needed
                    _options.Progress?.Report(0.5); // Simple progress indication
                },
                _options.Progress,
                _options.CancellationToken);

            // If no bytes repaired, the file is still corrupted
            if (bytesRepaired == 0 && bytesWritten == 0)
            {
                throw new InvalidDataException($"Unable to repair file: {entry.Path}");
            }
        }

        private void EnsureFileStream()
        {
            if (_fileStream == null)
            {
                throw new ObjectDisposedException(nameof(FragileArchive), "Archive has been disposed");
            }
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            // Remove null characters
            path = path.Replace("\0", string.Empty);

            // Remove invalid characters
            char[] invalidChars = Path.GetInvalidPathChars();
            foreach (char c in invalidChars)
            {
                path = path.Replace(c.ToString(), string.Empty);
            }

            // Replace Windows path separators with forward slashes
            path = path.Replace('\\', '/');

            // Remove leading slashes
            while (path.StartsWith("/"))
            {
#if NET48_OR_GREATER || NETSTANDARD2_0
                path = path.Substring(1);
#else
                path = path[1..];
#endif
            }

            return path;
        }

        /// <summary>
        /// Splits archive into multiple parts based on the SplitSize option
        /// </summary>
        /// <param name="outputDirectory">Directory to save the split parts (if null, the same directory as the archive is used)</param>
        /// <returns>Collection of archive parts</returns>
        public async Task<FragileArchivePartCollection> SplitAsync(string? outputDirectory = null)
        {
            if (_options.SplitSize <= 0)
            {
                throw new InvalidOperationException("SplitSize must be greater than zero to split an archive");
            }

            EnsureFileStream();

            // Make sure we have a valid output directory
            outputDirectory ??= Path.GetDirectoryName(ArchivePath) ?? ".";

            Directory.CreateDirectory(outputDirectory);

            // Create part collection
            FragileArchivePartCollection partCollection = new(_options);

            // Create a temporary copy of the archive with the current state if in create/update mode
            string tempArchivePath = ArchivePath;
            bool useTemporaryFile = _mode != FragileArchiveMode.Read;

            if (useTemporaryFile)
            {
                tempArchivePath = Path.Combine(_options.TempDirectory, $"Fragile_{Guid.NewGuid()}{_options.Extension}");
                await SaveAsync(); // Make sure the current state is saved
                File.Copy(ArchivePath, tempArchivePath);
            }

            try
            {
                // Open the source archive (or its copy)
                using FileStream sourceStream = new(tempArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                long fileSize = sourceStream.Length;

                // Calculate how many parts we need
                int totalParts = (int)Math.Ceiling((double)fileSize / _options.SplitSize);
                if (totalParts <= 1)
                {
                    throw new InvalidOperationException($"Archive size ({fileSize} bytes) is smaller than the split size ({_options.SplitSize} bytes)");
                }

                _options.Progress?.Report(0);

                // Check if we should use parallel processing
                if (_options.UseParallelProcessing && totalParts > 1 && fileSize > 50 * 1024 * 1024) // Only for files > 50MB
                {
                    await SplitParallelAsync(sourceStream, outputDirectory, totalParts, partCollection);
                }
                else
                {
                    // Split into parts sequentially
                    await SplitSequentialAsync(sourceStream, outputDirectory, totalParts, partCollection);
                }

                _options.Progress?.Report(1.0);
                return partCollection;
            }
            finally
            {
                // Clean up temporary file if created
                if (useTemporaryFile && File.Exists(tempArchivePath))
                {
                    File.Delete(tempArchivePath);
                }
            }
        }

        /// <summary>
        /// Splits archive into multiple parts sequentially
        /// </summary>
        private async Task SplitSequentialAsync(FileStream sourceStream, string outputDirectory, int totalParts, FragileArchivePartCollection partCollection)
        {
            long fileSize = sourceStream.Length;
            long partSize = _options.SplitSize;
            byte[] buffer = new byte[81920]; // 80 KB buffer
            long totalProcessed = 0;

            for (int partIndex = 1; partIndex <= totalParts; partIndex++)
            {
                // Calculate part size (last part may be smaller)
                long currentPartSize = Math.Min(partSize, fileSize - ((partIndex - 1) * partSize));

                // Create part file
                string partPath = Path.Combine(outputDirectory, FragileArchivePart.GetPartFileName(ArchivePath, partIndex, totalParts, _options.SplitName));

                // Create part object
                FragileArchivePart part = new()
                {
                    PartIndex = partIndex,
                    TotalParts = totalParts,
                    Path = partPath,
                    Size = currentPartSize,
                    Offset = (partIndex - 1) * partSize
                };

                // Add to collection
                partCollection.Add(part);

                // Write part data
                using FileStream partStream = new(partPath, FileMode.Create, FileAccess.Write, FileShare.None);

                // Position source stream
                sourceStream.Position = part.Offset;

                // Copy data
                long bytesRemaining = currentPartSize;
                while (bytesRemaining > 0)
                {
                    int bytesToRead = (int)Math.Min(buffer.Length, bytesRemaining);
#if NET48_OR_GREATER || NETSTANDARD2_0
                    int bytesRead = await sourceStream.ReadAsync(buffer, 0, bytesToRead, _options.CancellationToken);
#else
                    int bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, bytesToRead), _options.CancellationToken);
#endif

                    if (bytesRead == 0)
                    {
                        break; // End of stream
                    }

#if NET48_OR_GREATER || NETSTANDARD2_0
                    await partStream.WriteAsync(buffer, 0, bytesRead, _options.CancellationToken);
#else
                    await partStream.WriteAsync(buffer.AsMemory(0, bytesRead), _options.CancellationToken);
#endif

                    bytesRemaining -= bytesRead;
                    totalProcessed += bytesRead;

                    // Report progress
                    _options.Progress?.Report((double)totalProcessed / fileSize);
                }
            }
        }

        /// <summary>
        /// Splits archive into multiple parts using parallel processing
        /// </summary>
        private async Task SplitParallelAsync(FileStream sourceStream, string outputDirectory, int totalParts, FragileArchivePartCollection partCollection)
        {
            long fileSize = sourceStream.Length;
            long partSize = _options.SplitSize;
            int maxThreads = Math.Min(_options.MaxThreads, Environment.ProcessorCount);

            // Create SemaphoreSlim to limit concurrent operations
            using SemaphoreSlim semaphore = new(maxThreads);
            using SemaphoreSlim sourceStreamLock = new(1, 1);
            List<Task> partTasks = [];
            long totalProcessed = 0;
            object lockObj = new();

            // Create part objects
            for (int partIndex = 1; partIndex <= totalParts; partIndex++)
            {
                // Calculate part size (last part may be smaller)
                long currentPartSize = Math.Min(partSize, fileSize - ((partIndex - 1) * partSize));

                // Create part file path
                string partPath = Path.Combine(outputDirectory, FragileArchivePart.GetPartFileName(ArchivePath, partIndex, totalParts, _options.SplitName));

                // Create part object
                FragileArchivePart part = new()
                {
                    PartIndex = partIndex,
                    TotalParts = totalParts,
                    Path = partPath,
                    Size = currentPartSize,
                    Offset = (partIndex - 1) * partSize
                };

                // Add to collection
                partCollection.Add(part);

                // Capture variables for use in task
                int currentPartIndex = partIndex;
                long offset = part.Offset;
                long size = currentPartSize;

                // Wait for a thread to be available
                await semaphore.WaitAsync(_options.CancellationToken);

                // Process part in parallel
                partTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        // Read part data from source
                        byte[] partData = new byte[size];

                        // Use async read with semaphore
                        await sourceStreamLock.WaitAsync(_options.CancellationToken);
                        try
                        {
                            sourceStream.Position = offset;
#if NET48_OR_GREATER || NETSTANDARD2_0
                            await sourceStream.ReadAsync(partData, 0, (int)size, _options.CancellationToken);
#else
                            await sourceStream.ReadAsync(partData.AsMemory(0, (int)size), _options.CancellationToken);
#endif
                        }
                        finally
                        {
                            sourceStreamLock.Release();
                        }

                        // Write to part file
                        using FileStream partStream = new(partPath, FileMode.Create, FileAccess.Write, FileShare.None);

#if NET48_OR_GREATER || NETSTANDARD2_0
                        await partStream.WriteAsync(partData, 0, partData.Length, _options.CancellationToken);
#else
                        await partStream.WriteAsync(partData, _options.CancellationToken);
#endif

                        // Update progress
                        lock (lockObj)
                        {
                            totalProcessed += size;
                            _options.Progress?.Report((double)totalProcessed / fileSize);
                        }
                    }
                    finally
                    {
                        // Release the semaphore
                        semaphore.Release();
                    }
                }, _options.CancellationToken));
            }

            // Wait for all part tasks to complete
            await Task.WhenAll(partTasks);
        }

        /// <summary>
        /// Gets metadata for a specific entry
        /// </summary>
        /// <param name="entryPath">Path of the entry</param>
        /// <returns>Entry metadata or a new instance if none exists</returns>
        public EntryMetadata GetEntryMetadata(string entryPath)
        {
            entryPath = NormalizePath(entryPath);

            if (_entryMetadata.TryGetValue(entryPath, out EntryMetadata? metadata))
            {
                return metadata;
            }

            return new EntryMetadata();
        }

        /// <summary>
        /// Sets metadata for a specific entry
        /// </summary>
        /// <param name="entryPath">Path of the entry</param>
        /// <param name="metadata">Metadata to set</param>
        public void SetEntryMetadata(string entryPath, EntryMetadata metadata)
        {
            if (!_options.IncludeMetadata)
            {
                // Metadata is disabled, do nothing
                return;
            }

            entryPath = NormalizePath(entryPath);

            if (!_entries.ContainsKey(entryPath))
            {
                throw new KeyNotFoundException($"Entry not found: {entryPath}");
            }

            _entryMetadata[entryPath] = metadata ?? new EntryMetadata();
        }

        /// <summary>
        /// Gets an extended entry with additional metadata
        /// </summary>
        /// <param name="entryPath">Path of the entry</param>
        /// <returns>Extended archive entry</returns>
        public FragileArchiveEntryExtended GetExtendedEntry(string entryPath)
        {
            entryPath = NormalizePath(entryPath);

            if (!_entries.TryGetValue(entryPath, out FragileArchiveEntry? entry))
            {
                throw new KeyNotFoundException($"Entry not found: {entryPath}");
            }

            // Create extended entry
            FragileArchiveEntryExtended extendedEntry = FragileArchiveEntryExtended.FromEntry(entry);

            // Set metadata if available
            if (_options.IncludeMetadata && _entryMetadata.TryGetValue(entryPath, out EntryMetadata? metadata))
            {
                extendedEntry.Metadata = metadata;
            }

            // Set compression algorithm from options
            extendedEntry.CompressionAlgorithm = _options.CompressionAlgorithm;

            // Error correction is based on archive level settings
            extendedEntry.HasErrorCorrection = _options.EnableErrorCorrection;

            return extendedEntry;
        }

        /// <summary>
        /// Update entry with additional metadata
        /// </summary>
        /// <param name="extendedEntry">Extended entry with metadata</param>
        public void UpdateExtendedEntry(FragileArchiveEntryExtended extendedEntry)
        {
#if NET48_OR_GREATER || NETSTANDARD2_0_OR_GREATER
            if (extendedEntry == null)
            {
                throw new ArgumentNullException(nameof(extendedEntry));
            }
#else
            ArgumentNullException.ThrowIfNull(extendedEntry);
#endif

            string entryPath = NormalizePath(extendedEntry.Path);

            if (!_entries.TryGetValue(entryPath, out _))
            {
                throw new KeyNotFoundException($"Entry not found: {entryPath}");
            }

            // Update metadata if enabled
            if (_options.IncludeMetadata)
            {
                _entryMetadata[entryPath] = extendedEntry.Metadata ?? new EntryMetadata();
            }
        }

        /// <summary>
        /// Gets all extended entries with additional metadata
        /// </summary>
        /// <returns>Collection of extended entries</returns>
        public IEnumerable<FragileArchiveEntryExtended> GetExtendedEntries()
        {
            foreach (FragileArchiveEntry entry in _entries.Values)
            {
                string entryPath = entry.Path;

                // Create extended entry
                FragileArchiveEntryExtended extendedEntry = FragileArchiveEntryExtended.FromEntry(entry);

                // Set metadata if available
                if (_options.IncludeMetadata && _entryMetadata.TryGetValue(entryPath, out EntryMetadata? metadata))
                {
                    extendedEntry.Metadata = metadata;
                }

                // Set compression algorithm from options
                extendedEntry.CompressionAlgorithm = _options.CompressionAlgorithm;

                // Error correction is based on archive level settings
                extendedEntry.HasErrorCorrection = _options.EnableErrorCorrection;

                yield return extendedEntry;
            }
        }

        /// <summary>
        /// Safely creates a DateTime from binary data.
        /// Returns the current time in case of invalid values.
        /// </summary>
        private DateTime TryParseDateTime(long ticks)
        {
            try
            {
                return DateTime.FromBinary(ticks);
            }
            catch (ArgumentException)
            {
                // Return current time for invalid DateTime value
                return DateTime.UtcNow;
            }
        }

        // Helper method for ReadExactlyAsync if not available directly on Stream (.NET Standard 2.0)
        private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int bytesRead = 0;
            while (bytesRead < count)
            {
#if NET48_OR_GREATER || NETSTANDARD2_0
                int read = await stream.ReadAsync(buffer, offset + bytesRead, count - bytesRead, cancellationToken);
#else
                int read = await stream.ReadAsync(buffer.AsMemory(offset + bytesRead, count - bytesRead), cancellationToken);
#endif

                if (read == 0)
                {
                    throw new EndOfStreamException("Unable to read exactly specified number of bytes.");
                }

                bytesRead += read;
            }
        }
    }
}