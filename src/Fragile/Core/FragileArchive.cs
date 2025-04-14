using Fragile.ErrorCorrection;
using Fragile.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        private bool _disposed = false;
        private FileStream? _fileStream;
        private FragileOptions _options;

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

                    // Entry count
                    writer.Write(_entries.Count);

                    // Header end - data start position (we'll update this later)
                    long dataPositionOffset = outputStream.Position;
                    writer.Write((long)0); // Placeholder

                    // Write file entries information
                    foreach (FragileArchiveEntry entry in _entries.Values)
                    {
                        // File path
                        writer.Write(entry.Path);

                        // Size and compressed size
                        writer.Write(entry.Size);

                        // Compressed size field (we'll update this later)
                        long compressedSizeOffset = outputStream.Position;
                        writer.Write((long)0); // Placeholder

                        // File time
                        writer.Write(entry.LastModified.ToBinary());

                        // Is directory?
                        writer.Write(entry.IsDirectory);

                        // Position (we'll update this later)
                        long positionOffset = outputStream.Position;
                        writer.Write((long)0); // Placeholder

                        // Save writing position
                        entry.HeaderOffset = compressedSizeOffset;
                        entry.PositionOffset = positionOffset;
                    }

                    // Update data start position
                    long dataPosition = outputStream.Position;
                    outputStream.Position = dataPositionOffset;
                    writer.Write(dataPosition);
                    outputStream.Position = dataPosition;

                    // Write compressed file contents
                    foreach (FragileArchiveEntry entry in _entries.Values)
                    {
                        if (entry.IsDirectory)
                        {
                            continue; // No content for directories
                        }

                        // Update file position
                        long filePosition = outputStream.Position;
                        outputStream.Position = entry.PositionOffset;
                        writer.Write(filePosition);
                        outputStream.Position = filePosition;

                        if (!string.IsNullOrEmpty(entry.SourcePath) && File.Exists(entry.SourcePath))
                        {
                            // Compress and write the file
                            using FileStream fileStream = new(entry.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                            using MemoryStream compressStream = new();

                            // Use the appropriate compression level
                            System.IO.Compression.CompressionLevel compressionLevel = _options.CompressionLevel switch
                            {
                                Compression.CompressionLevel.Fastest => System.IO.Compression.CompressionLevel.Fastest,
                                Compression.CompressionLevel.Fast => System.IO.Compression.CompressionLevel.Fastest,
                                Compression.CompressionLevel.Normal => System.IO.Compression.CompressionLevel.Optimal,
                                Compression.CompressionLevel.High => System.IO.Compression.CompressionLevel.Optimal,
                                Compression.CompressionLevel.Ultra => System.IO.Compression.CompressionLevel.Optimal,
                                _ => System.IO.Compression.CompressionLevel.Optimal
                            };

                            using (DeflateStream deflateStream = new(compressStream, compressionLevel, true))
                            {
                                await fileStream.CopyToAsync(deflateStream);
                            }

                            byte[] compressedData = compressStream.ToArray();
                            entry.CompressedSize = compressedData.Length;

                            // Update compressed size
                            outputStream.Position = entry.HeaderOffset;
                            writer.Write(entry.CompressedSize);
                            outputStream.Position = filePosition;

                            // Write compressed data
                            await outputStream.WriteAsync(compressedData, 0, compressedData.Length);
                        }
                        else if (entry.Data != null)
                        {
                            // Compress and write in-memory data
                            using MemoryStream dataStream = new(entry.Data);
                            using MemoryStream compressStream = new();

                            // Use the appropriate compression level
                            System.IO.Compression.CompressionLevel compressionLevel = _options.CompressionLevel switch
                            {
                                Compression.CompressionLevel.Fastest => System.IO.Compression.CompressionLevel.Fastest,
                                Compression.CompressionLevel.Fast => System.IO.Compression.CompressionLevel.Fastest,
                                Compression.CompressionLevel.Normal => System.IO.Compression.CompressionLevel.Optimal,
                                Compression.CompressionLevel.High => System.IO.Compression.CompressionLevel.Optimal,
                                Compression.CompressionLevel.Ultra => System.IO.Compression.CompressionLevel.Optimal,
                                _ => System.IO.Compression.CompressionLevel.Optimal
                            };

                            using (DeflateStream deflateStream = new(compressStream, compressionLevel, true))
                            {
                                await dataStream.CopyToAsync(deflateStream);
                            }

                            byte[] compressedData = compressStream.ToArray();
                            entry.CompressedSize = compressedData.Length;

                            // Update compressed size
                            outputStream.Position = entry.HeaderOffset;
                            writer.Write(entry.CompressedSize);
                            outputStream.Position = filePosition;

                            // Write compressed data
                            await outputStream.WriteAsync(compressedData, 0, compressedData.Length);
                        }
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
                    await errorCorrection.AddErrorCorrectionAsync(outputStream, _fileStream, _options.Progress);

                    // Cleanup
                    outputStream.Close();
                    File.Delete(tempFilePath);
                }

                // Flush to disk
                await _fileStream!.FlushAsync();
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

                // Decompress and write to the destination file
                using FileStream outputFile = new(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using MemoryStream compressedStream = new(compressedData);
                using DeflateStream deflateStream = new(compressedStream, CompressionMode.Decompress);

                await deflateStream.CopyToAsync(outputFile);
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
    }
}