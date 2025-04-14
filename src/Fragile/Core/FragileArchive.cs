using Fragile.Compression;
using Fragile.ErrorCorrection;
using Fragile.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Fragile.Core
{
    /// <summary>
    /// Main class of the Fragile archiving library
    /// </summary>
    public class FragileArchive : IDisposable
    {
        private readonly Dictionary<string, FragileArchiveEntry> _entries = new();
        private const string FileSignature = "FRGL";
        private readonly FragileArchiveMode _mode;
        private const ushort VersionMajor = 1;
        private const ushort VersionMinor = 0;
        private FileStream? _fileStream;
        private FragileOptions _options;
        private bool _disposed = false;

        /// <summary>
        /// List of all files in the archive
        /// </summary>
        public IReadOnlyCollection<FragileArchiveEntry> Entries => _entries.Values;

        /// <summary>
        /// Path to the archive file
        /// </summary>
        public string ArchivePath { get; }

        /// <summary>
        /// Creates a new Fragile archive or opens an existing one
        /// </summary>
        /// <param name="archivePath">Path to the archive file</param>
        /// <param name="mode">Opening mode</param>
        public FragileArchive(string archivePath, FragileArchiveMode mode = FragileArchiveMode.Read)
            : this(archivePath, mode, new FragileOptions())
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
            _mode = mode;
            _options = options ?? throw new ArgumentNullException(nameof(options));

            if (mode == FragileArchiveMode.Create)
            {
                _fileStream = new FileStream(archivePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            }
            else if (mode == FragileArchiveMode.Read)
            {
                if (!File.Exists(archivePath))
                {
                    throw new FileNotFoundException($"Archive file not found: {archivePath}");
                }

                _fileStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                LoadArchiveEntries();
            }
            else // Update
            {
                if (!File.Exists(archivePath))
                {
                    _fileStream = new FileStream(archivePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                }
                else
                {
                    _fileStream = new FileStream(archivePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
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
                    string relativePath = childEntry.Path[(entryPath.Length + 1)..];
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
                    string relativePath = childEntry.Path[(entryPath.Length + 1)..];
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
                    await ExtractFileAsync(entry, targetPath);
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
                tempFilePath = Path.GetTempFileName();
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
                    writer.Write(Encoding.ASCII.GetBytes(FileSignature));

                    // Version
                    writer.Write(VersionMajor);
                    writer.Write(VersionMinor);

                    // Options
                    byte optionFlags = 0;
                    
                    // Set option flags based on enabled features
                    if (_options.EnableEncryption)
                        optionFlags |= 0x01;
                    
                    if (_options.EnableChecksumVerification)
                        optionFlags |= 0x02;
                    
                    if (_options.EnableErrorCorrection)
                        optionFlags |= 0x04;
                    
                    if (_options.IncludeMetadata)
                        optionFlags |= 0x08;
                    
                    if (_options.UseSolidCompression)
                        optionFlags |= 0x10;
                    
                    writer.Write(optionFlags);

                    // Compression algorithm
                    writer.Write((byte)_options.CompressionAlgorithm);

                    // Number of entries
                    writer.Write(_entries.Count);

                    // Reserve space for central directory offset
                    long centralDirOffsetPosition = outputStream.Position;
                    writer.Write((long)0);

                    // Process each entry
                    foreach (var entry in _entries.Values)
                    {
                        // Skip if already compressed or special handling is needed
                        if (entry.IsDirectory || entry.Data != null)
                            continue;

                        // Record position for this entry
                        entry.HeaderOffset = outputStream.Position;

                        // Entry path
                        byte[] pathBytes = Encoding.UTF8.GetBytes(entry.Path);
                        writer.Write(pathBytes.Length);
                        writer.Write(pathBytes);

                        // Entry metadata
                        writer.Write(entry.Size);
                        writer.Write(entry.LastModified.ToBinary());
                        writer.Write(entry.IsDirectory);

                        // Reserve space for compressed size
                        long sizePosition = outputStream.Position;
                        writer.Write((long)0);

                        if (entry.IsDirectory)
                        {
                            // No data for directories
                            entry.CompressedSize = 0;
                        }
                        else if (entry.Data != null)
                        {
                            // Entry data is already in memory
                            entry.PositionOffset = outputStream.Position;
                            entry.CompressedSize = entry.Data.Length;
                            
                            // Update compressed size
                            long temp = outputStream.Position;
                            outputStream.Position = sizePosition;
                            writer.Write(entry.CompressedSize);
                            outputStream.Position = temp;

                            // Write data directly
                            await outputStream.WriteAsync(entry.Data, 0, entry.Data.Length);
                        }
                        else if (!string.IsNullOrEmpty(entry.SourcePath) && File.Exists(entry.SourcePath))
                        {
                            // Compress from file
                            entry.PositionOffset = outputStream.Position;
                            long filePosition = outputStream.Position;

                            try
                            {
                                using FileStream fileStream = new(entry.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                                
                                // Create compression provider with options
                                CompressionProvider compressionProvider = CompressionProvider.Create(
                                    _options.CompressionAlgorithm, 
                                    _options.CompressionLevel,
                                    _options.UseParallelProcessing,
                                    _options.MaxThreads);
                                
                                // Report progress for this file if needed
                                IProgress<double>? fileProgress = null;
                                if (_options.Progress != null)
                                {
                                    double startPercentage = (double)outputStream.Position / (outputStream.Length + fileStream.Length);
                                    double endPercentage = (double)(outputStream.Position + fileStream.Length) / (outputStream.Length + fileStream.Length);
                                    double range = endPercentage - startPercentage;

                                    fileProgress = new Progress<double>(p => 
                                        _options.Progress.Report(startPercentage + p * range));
                                }

                                // Compress the file
                                entry.CompressedSize = await compressionProvider.CompressAsync(
                                    fileStream, 
                                    outputStream, 
                                    fileProgress, 
                                    _options.CancellationToken);
                                
                                // Update compressed size
                                outputStream.Position = sizePosition;
                                writer.Write(entry.CompressedSize);
                                outputStream.Position = filePosition + entry.CompressedSize;
                            }
                            catch (Exception ex)
                            {
                                throw new IOException($"Failed to compress file {entry.SourcePath}: {ex.Message}", ex);
                            }
                        }
                        else
                        {
                            throw new FileNotFoundException($"Source file not found: {entry.SourcePath}");
                        }
                    }

                    // Write central directory
                    long centralDirOffset = outputStream.Position;

                    // Update central directory offset
                    outputStream.Position = centralDirOffsetPosition;
                    writer.Write(centralDirOffset);
                    outputStream.Position = centralDirOffset;

                    // Write each entry's info in the central directory
                    foreach (var entry in _entries.Values)
                    {
                        // Entry path
                        byte[] pathBytes = Encoding.UTF8.GetBytes(entry.Path);
                        writer.Write(pathBytes.Length);
                        writer.Write(pathBytes);

                        // Position and sizes
                        writer.Write(entry.HeaderOffset);
                        writer.Write(entry.PositionOffset);
                        writer.Write(entry.Size);
                        writer.Write(entry.CompressedSize);
                        writer.Write(entry.IsDirectory);
                    }
                }

                // If error correction is enabled, apply it
                if (_options.EnableErrorCorrection && _options.ErrorCorrectionLevel > 0)
                {
                    // Create a error correction provider
                    ErrorCorrectionProvider errorCorrection = ErrorCorrectionProvider.Create(_options.ErrorCorrectionLevel);

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
            _fileStream!.Position = 0;

            using BinaryReader reader = new(_fileStream, Encoding.UTF8, true);
            // Check file signature
            string signature = Encoding.ASCII.GetString(reader.ReadBytes(4));

            if (signature != FileSignature)
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

            // Entry count
            int entryCount = reader.ReadInt32();

            // Data start position
            long dataPosition = reader.ReadInt64();

            // Read file entries
            for (int i = 0; i < entryCount; i++)
            {
                FragileArchiveEntry entry = new()
                {
                    Path = reader.ReadString(),
                    Size = reader.ReadInt64(),
                    CompressedSize = reader.ReadInt64(),
                    LastModified = DateTime.FromBinary(reader.ReadInt64()),
                    IsDirectory = reader.ReadBoolean(),
                    Position = reader.ReadInt64()
                };

                _entries[entry.Path] = entry;
            }
        }

        private void ExtractFile(FragileArchiveEntry entry, string destinationPath)
        {
            EnsureFileStream();

            // Create directory if it doesn't exist
            string? directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Position stream at file data
            _fileStream!.Position = entry.Position;

            // Read compressed data
            byte[] compressedData = new byte[entry.CompressedSize];
            _fileStream.Read(compressedData, 0, (int)entry.CompressedSize);

            // Decompress and write to the destination file
            using FileStream outputFile = new(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using MemoryStream compressedStream = new(compressedData);
            using DeflateStream deflateStream = new(compressedStream, CompressionMode.Decompress);

            deflateStream.CopyTo(outputFile);
        }

        private async Task ExtractFileAsync(FragileArchiveEntry entry, string destinationPath)
        {
            EnsureFileStream();

            try
            {
                // Create directory if it doesn't exist
                string? directory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Position stream at file data
                _fileStream!.Position = entry.Position;

                // Read compressed data
                byte[] compressedData = new byte[entry.CompressedSize];
                await _fileStream.ReadAsync(compressedData, 0, (int)entry.CompressedSize);

                // Create a memory stream with the compressed data
                using MemoryStream compressedStream = new(compressedData);
                
                // Check if checksum verification is enabled
                if (_options.EnableChecksumVerification)
                {
                    // Verify the integrity of the compressed data
                    var verificationProvider = Verification.VerificationProvider.Create(_options.ChecksumAlgorithm);
                    
                    // Read the stored checksum (assuming it's stored right after the compressed data)
                    byte[] storedChecksum = new byte[verificationProvider.GetChecksumSize()];
                    await _fileStream.ReadAsync(storedChecksum, 0, storedChecksum.Length);
                    
                    // Verify the checksum
                    bool isValid = await verificationProvider.VerifyChecksumAsync(
                        compressedStream, 
                        storedChecksum, 
                        _options.Progress, 
                        _options.CancellationToken);
                    
                    if (!isValid)
                    {
                        throw new InvalidDataException($"Checksum verification failed for {entry.Path}");
                    }
                    
                    // Reset stream position
                    compressedStream.Position = 0;
                }

                // Create the destination file
                using FileStream outputFile = new(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                
                // Select the appropriate compression provider based on the stored algorithm
                byte compressionAlgorithm = 1; // Default to Deflate if unknown
                
                // Create an appropriate decompressor
                CompressionProvider compressionProvider = CompressionProvider.Create(
                    (Compression.CompressionAlgorithm)compressionAlgorithm, 
                    _options.CompressionLevel,
                    _options.UseParallelProcessing,
                    _options.MaxThreads);
                
                // Decompress the data
                await compressionProvider.DecompressAsync(
                    compressedStream, 
                    outputFile, 
                    _options.Progress, 
                    _options.CancellationToken);
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
            ErrorCorrectionProvider errorCorrection = ErrorCorrectionProvider.Create(_options.ErrorCorrectionLevel);

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
                });

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

            // Replace Windows path separators with forward slashes
            path = path.Replace('\\', '/');

            // Remove leading slashes
            while (path.StartsWith('/'))
            {
                path = path[1..];
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
                tempArchivePath = Path.Combine(Path.GetTempPath(), $"fragile_temp_{Guid.NewGuid()}.frgl");
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
                long currentPartSize = Math.Min(partSize, fileSize - (partIndex - 1) * partSize);
                
                // Create part file
                string partPath = Path.Combine(outputDirectory, 
                    FragileArchivePart.GetPartFileName(ArchivePath, partIndex, totalParts));
                
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
                    int bytesRead = await sourceStream.ReadAsync(buffer, 0, bytesToRead, _options.CancellationToken);
                    
                    if (bytesRead == 0)
                        break; // End of stream
                        
                    await partStream.WriteAsync(buffer, 0, bytesRead, _options.CancellationToken);
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
            List<Task> partTasks = new();
            long totalProcessed = 0;
            object lockObj = new();
            
            // Create part objects
            for (int partIndex = 1; partIndex <= totalParts; partIndex++)
            {
                // Calculate part size (last part may be smaller)
                long currentPartSize = Math.Min(partSize, fileSize - (partIndex - 1) * partSize);
                
                // Create part file path
                string partPath = Path.Combine(outputDirectory, 
                    FragileArchivePart.GetPartFileName(ArchivePath, partIndex, totalParts));
                
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
                        
                        // Lock source stream for reading
                        lock (sourceStream)
                        {
                            sourceStream.Position = offset;
                            sourceStream.Read(partData, 0, (int)size);
                        }
                        
                        // Write to part file
                        using FileStream partStream = new(partPath, FileMode.Create, FileAccess.Write, FileShare.None);
                        await partStream.WriteAsync(partData, 0, partData.Length, _options.CancellationToken);
                        
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
    }
}