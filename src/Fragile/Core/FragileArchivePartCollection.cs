using Fragile.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Fragile.Core
{
    /// <summary>
    /// Represents a collection of split archive parts
    /// </summary>
    public class FragileArchivePartCollection : IReadOnlyCollection<FragileArchivePart>
    {
        private readonly List<FragileArchivePart> _parts = new();
        private FragileOptions _options;

        /// <summary>
        /// Gets the number of parts in the collection
        /// </summary>
        public int Count => _parts.Count;

        /// <summary>
        /// Gets the part at the specified index
        /// </summary>
        public FragileArchivePart this[int index] => _parts[index];

        /// <summary>
        /// Creates a new empty part collection with default options
        /// </summary>
        public FragileArchivePartCollection()
            : this(new FragileOptions())
        {
        }

        /// <summary>
        /// Creates a new empty part collection with specified options
        /// </summary>
        /// <param name="options">Archive options</param>
        public FragileArchivePartCollection(FragileOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Adds a part to the collection
        /// </summary>
        /// <param name="part">The part to add</param>
        public void Add(FragileArchivePart part)
        {
            if (part == null)
            {
                throw new ArgumentNullException(nameof(part));
            }

            _parts.Add(part);

            // Sort by part index to ensure correct order
            _parts.Sort((a, b) => a.PartIndex.CompareTo(b.PartIndex));
        }

        /// <summary>
        /// Gets an enumerator for the collection
        /// </summary>
        public IEnumerator<FragileArchivePart> GetEnumerator()
        {
            return _parts.GetEnumerator();
        }

        /// <summary>
        /// Gets a non-generic enumerator for the collection
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Combines all parts into a single file
        /// </summary>
        /// <param name="outputPath">Path to the output file</param>
        /// <param name="progress">Optional progress reporting</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the combine operation</returns>
        public async Task CombinePartsAsync(string outputPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (_parts.Count == 0)
            {
                throw new InvalidOperationException("No parts to combine");
            }

            // Validate that all parts are present
            int expectedTotalParts = _parts[0].TotalParts;
            if (_parts.Count != expectedTotalParts)
            {
                throw new InvalidOperationException($"Missing parts. Expected {expectedTotalParts} parts, but found {_parts.Count}");
            }

            // Check if parts are in sequence
            for (int i = 0; i < _parts.Count; i++)
            {
                if (_parts[i].PartIndex != i + 1)
                {
                    throw new InvalidOperationException($"Missing part {i + 1}");
                }
            }

            // Calculate total size for progress reporting
            long totalSize = _parts.Sum(p => p.Size);
            long processedSize = 0;

            // Create output file
            using FileStream outputStream = new(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);

            // Check if parallel processing should be used
            if (_options.UseParallelProcessing && _parts.Count > 1 && totalSize > 100 * 1024 * 1024) // Only for large archives > 100MB
            {
                await CombinePartsParallelAsync(outputStream, totalSize, progress, cancellationToken);
            }
            else
            {
                // Sequential combination of parts
                foreach (FragileArchivePart part in _parts)
                {
                    using FileStream partStream = new(part.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    byte[] buffer = new byte[81920]; // 80 KB buffer
                    int bytesRead;

                    while ((bytesRead = await partStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await outputStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);

                        // Update progress
                        processedSize += bytesRead;
                        progress?.Report((double)processedSize / totalSize);

                        // Check for cancellation
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            }

            // After combining is done, if encryption is enabled, encrypt the entire output file
            if (_options.EnableEncryption && !string.IsNullOrEmpty(_options.Password))
            {
                // Get current file position to remember the file size
                long fileSize = outputStream.Length;

                // Reset output stream for reading
                outputStream.Position = 0;

                // Create a temporary file for encrypted output
                string tempEncryptedFile = Path.GetTempFileName();
                using FileStream encryptedOutput = new(tempEncryptedFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

                try
                {
                    // Create encryption provider
                    Encryption.EncryptionProvider encryptionProvider = Encryption.EncryptionProvider.Create(
                        _options.EncryptionMethod,
                        _options.Password);

                    // Encrypt the combined file
                    await encryptionProvider.EncryptAsync(
                        outputStream,
                        encryptedOutput,
                        progress,
                        cancellationToken);

                    // Close streams
                    outputStream.Close();
                    encryptedOutput.Close();

                    // Replace the original file with encrypted one
                    File.Delete(outputPath);
                    File.Move(tempEncryptedFile, outputPath);
                }
                catch
                {
                    // Cleanup on error
                    if (File.Exists(tempEncryptedFile))
                    {
                        File.Delete(tempEncryptedFile);
                    }
                    throw;
                }
            }
        }

        /// <summary>
        /// Combines all parts into a single file using parallel processing
        /// </summary>
        private async Task CombinePartsParallelAsync(FileStream outputStream, long totalSize, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            // Prepare part data and offsets
            Dictionary<int, long> partOffsets = new();
            long currentOffset = 0;

            // Calculate offsets for each part
            foreach (FragileArchivePart part in _parts)
            {
                partOffsets[part.PartIndex] = currentOffset;
                currentOffset += part.Size;
            }

            // Set up semaphore to limit concurrent operations
            int maxThreads = Math.Min(_options.MaxThreads, Environment.ProcessorCount);
            using SemaphoreSlim semaphore = new(maxThreads);

            // Load parts in parallel
            List<Task> loadTasks = new();
            long totalProcessed = 0;
            object lockObj = new();

            foreach (FragileArchivePart part in _parts)
            {
                await semaphore.WaitAsync(cancellationToken);

                loadTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        using FileStream partStream = new(part.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                        byte[] partData = new byte[part.Size];
                        int bytesRead = 0;
                        int totalRead = 0;
                        byte[] buffer = new byte[81920]; // 80 KB buffer

                        // Read part data
                        while (totalRead < partData.Length &&
                               (bytesRead = await partStream.ReadAsync(buffer, 0, Math.Min(buffer.Length, partData.Length - totalRead), cancellationToken)) > 0)
                        {
                            Buffer.BlockCopy(buffer, 0, partData, totalRead, bytesRead);
                            totalRead += bytesRead;
                        }

                        // Write to output at the correct position
                        lock (outputStream)
                        {
                            outputStream.Position = partOffsets[part.PartIndex];
                            outputStream.Write(partData, 0, partData.Length);
                        }

                        // Update progress
                        lock (lockObj)
                        {
                            totalProcessed += partData.Length;
                            progress?.Report((double)totalProcessed / totalSize);
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            // Wait for all tasks to complete
            await Task.WhenAll(loadTasks);

            // Ensure we report 100% completion
            progress?.Report(1.0);

            // After parallel combination is done, if encryption is enabled, encrypt the entire output file
            if (_options.EnableEncryption && !string.IsNullOrEmpty(_options.Password))
            {
                // Need to close and reopen output stream for reading/writing
                long fileSize = outputStream.Length;
                outputStream.Flush();

                // Create a temporary file for encrypted output
                string tempEncryptedFile = Path.GetTempFileName();

                try
                {
                    // Reopen output stream for reading
                    outputStream.Position = 0;

                    using FileStream encryptedOutput = new(tempEncryptedFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

                    // Create encryption provider
                    Encryption.EncryptionProvider encryptionProvider = Encryption.EncryptionProvider.Create(
                        _options.EncryptionMethod,
                        _options.Password);

                    // Encrypt the combined file
                    await encryptionProvider.EncryptAsync(
                        outputStream,
                        encryptedOutput,
                        new Progress<double>(p => progress?.Report(0.5 + (p * 0.5))), // Scale progress from 50% to 100%
                        cancellationToken);

                    // Close streams
                    encryptedOutput.Close();
                    outputStream.Close();

                    // Determine the full path of the output
                    string fullOutputPath = (outputStream as FileStream)?.Name;

                    if (!string.IsNullOrEmpty(fullOutputPath))
                    {
                        // Replace the original file with encrypted one
                        File.Delete(fullOutputPath);
                        File.Move(tempEncryptedFile, fullOutputPath);
                    }
                }
                catch
                {
                    // Cleanup on error
                    if (File.Exists(tempEncryptedFile))
                    {
                        File.Delete(tempEncryptedFile);
                    }
                    throw;
                }
            }
        }

        /// <summary>
        /// Finds all split archive parts for a given base file name pattern
        /// </summary>
        /// <param name="basePath">The base archive path</param>
        /// <returns>A collection of archive parts, or empty if not found</returns>
        public static FragileArchivePartCollection FindParts(string basePath)
        {
            return FindParts(basePath, new FragileOptions());
        }

        /// <summary>
        /// Finds all split archive parts for a given base file name pattern with specified options
        /// </summary>
        /// <param name="basePath">The base archive path</param>
        /// <param name="options">Archive options</param>
        /// <returns>A collection of archive parts, or empty if not found</returns>
        public static FragileArchivePartCollection FindParts(string basePath, FragileOptions options)
        {
            FragileArchivePartCollection result = new(options);

            if (string.IsNullOrEmpty(basePath))
            {
                return result;
            }

            string directory = Path.GetDirectoryName(basePath) ?? "";
            string fileName = Path.GetFileName(basePath);
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(basePath);
            string extension = Path.GetExtension(basePath);

            // Search for part files matching the pattern [filename].partXXX[extension]
            if (Directory.Exists(directory))
            {
                string searchPattern = $"{fileNameWithoutExt}.part*{extension}";
                string[] partFiles = Directory.GetFiles(directory, searchPattern);

                foreach (string partFile in partFiles)
                {
                    string partFileName = Path.GetFileName(partFile);

                    // Extract part number from filename
                    // Pattern: [filename].partXXX[extension]
                    string partIndexStr = partFileName.Substring(
                        fileNameWithoutExt.Length + ".part".Length,
                        partFileName.Length - fileNameWithoutExt.Length - ".part".Length - extension.Length
                    );

                    if (int.TryParse(partIndexStr, out int partIndex))
                    {
                        FileInfo fileInfo = new(partFile);

                        FragileArchivePart part = new()
                        {
                            PartIndex = partIndex,
                            Path = partFile,
                            Size = fileInfo.Length
                            // TotalParts will be set later once we have all parts
                        };

                        result.Add(part);
                    }
                }

                // Set TotalParts for all parts
                if (result.Count > 0)
                {
                    int totalParts = result.Count;
                    foreach (FragileArchivePart part in result._parts)
                    {
                        part.TotalParts = totalParts;
                    }
                }
            }

            return result;
        }
    }
}