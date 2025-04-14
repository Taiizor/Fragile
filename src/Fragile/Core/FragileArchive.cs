using Fragile.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Fragile.Core
{
    /// <summary>
    /// Main class of the Fragile archiving library
    /// </summary>
    public class FragileArchive : IDisposable
    {
        private const string FileSignature = "FRGL";
        private const ushort VersionMajor = 1;
        private const ushort VersionMinor = 0;
        private readonly FragileArchiveMode _mode;
        private readonly Dictionary<string, FragileArchiveEntry> _entries = new();
        private bool _disposed = false;
        private FileStream? _fileStream;

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
        {
            ArchivePath = archivePath ?? throw new ArgumentNullException(nameof(archivePath));
            _mode = mode;

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
                    string relativePath = childEntry.Path.Substring(entryPath.Length + 1);
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
        /// Extracts all files from the archive to the target directory
        /// </summary>
        /// <param name="destinationPath">Target directory path</param>
        public void ExtractAll(string destinationPath)
        {
            if (_mode == FragileArchiveMode.Create)
            {
                throw new InvalidOperationException("Cannot extract in create mode");
            }

            Directory.CreateDirectory(destinationPath);

            // Create directories first
            foreach (FragileArchiveEntry? entry in _entries.Values.Where(e => e.IsDirectory))
            {
                Directory.CreateDirectory(Path.Combine(destinationPath, entry.Path));
            }

            // Then extract files
            foreach (FragileArchiveEntry? entry in _entries.Values.Where(e => !e.IsDirectory))
            {
                string destPath = Path.Combine(destinationPath, entry.Path);
                string destDir = Path.GetDirectoryName(destPath);

                if (!string.IsNullOrEmpty(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                ExtractFile(entry, destPath);
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

            EnsureFileStream();

            // Recreate the archive
            _fileStream!.SetLength(0);
            _fileStream.Position = 0;

            // Write file header
            using (BinaryWriter writer = new(_fileStream, Encoding.UTF8, true))
            {
                // File signature
                writer.Write(Encoding.ASCII.GetBytes(FileSignature));

                // Version
                writer.Write(VersionMajor);
                writer.Write(VersionMinor);

                // Entry count
                writer.Write(_entries.Count);

                // Header end - data start position (we'll update this later)
                long dataPositionOffset = _fileStream.Position;
                writer.Write((long)0); // Placeholder

                // Write file entries information
                foreach (FragileArchiveEntry entry in _entries.Values)
                {
                    // File path
                    writer.Write(entry.Path);

                    // Size and compressed size
                    writer.Write(entry.Size);

                    // Compressed size field (we'll update this later)
                    long compressedSizeOffset = _fileStream.Position;
                    writer.Write((long)0); // Placeholder

                    // File time
                    writer.Write(entry.LastModified.ToBinary());

                    // Is directory?
                    writer.Write(entry.IsDirectory);

                    // Position (we'll update this later)
                    long positionOffset = _fileStream.Position;
                    writer.Write((long)0); // Placeholder

                    // Save writing position
                    entry.HeaderOffset = compressedSizeOffset;
                    entry.PositionOffset = positionOffset;
                }

                // Update data start position
                long dataPosition = _fileStream.Position;
                _fileStream.Position = dataPositionOffset;
                writer.Write(dataPosition);
                _fileStream.Position = dataPosition;

                // Write compressed file contents
                foreach (FragileArchiveEntry entry in _entries.Values)
                {
                    if (entry.IsDirectory)
                    {
                        continue; // No content for directories
                    }

                    // Update file position
                    long filePosition = _fileStream.Position;
                    _fileStream.Position = entry.PositionOffset;
                    writer.Write(filePosition);
                    _fileStream.Position = filePosition;

                    if (!string.IsNullOrEmpty(entry.SourcePath) && File.Exists(entry.SourcePath))
                    {
                        // Compress and write the file
                        using FileStream fileStream = new(entry.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using MemoryStream compressStream = new();
                        using (DeflateStream deflateStream = new(compressStream, CompressionMode.Compress, true))
                        {
                            fileStream.CopyTo(deflateStream);
                        }

                        byte[] compressedData = compressStream.ToArray();
                        entry.CompressedSize = compressedData.Length;

                        // Update compressed size
                        _fileStream.Position = entry.HeaderOffset;
                        writer.Write(entry.CompressedSize);
                        _fileStream.Position = filePosition;

                        // Write compressed data
                        _fileStream.Write(compressedData, 0, compressedData.Length);
                    }
                    else if (entry.Data != null)
                    {
                        // Compress and write in-memory data
                        using MemoryStream dataStream = new(entry.Data);
                        using MemoryStream compressStream = new();
                        using (DeflateStream deflateStream = new(compressStream, CompressionMode.Compress, true))
                        {
                            dataStream.CopyTo(deflateStream);
                        }

                        byte[] compressedData = compressStream.ToArray();
                        entry.CompressedSize = compressedData.Length;

                        // Update compressed size
                        _fileStream.Position = entry.HeaderOffset;
                        writer.Write(entry.CompressedSize);
                        _fileStream.Position = filePosition;

                        // Write compressed data
                        _fileStream.Write(compressedData, 0, compressedData.Length);
                    }
                }
            }

            _fileStream.Flush();
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

            // Create target directory
            string destDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            using (FileStream output = new(destinationPath, FileMode.Create, FileAccess.Write))
            {
                _fileStream!.Position = entry.Position;

                byte[] compressedData = new byte[entry.CompressedSize];
                _fileStream.Read(compressedData, 0, (int)entry.CompressedSize);

                using MemoryStream compressedStream = new(compressedData);
                using DeflateStream decompressStream = new(compressedStream, CompressionMode.Decompress);
                decompressStream.CopyTo(output);
            }

            // Set file time
            File.SetLastWriteTimeUtc(destinationPath, entry.LastModified);
        }

        private void EnsureFileStream()
        {
            if (_fileStream == null || _disposed)
            {
                throw new ObjectDisposedException(nameof(FragileArchive));
            }
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/').Trim('/');
        }
    }
}