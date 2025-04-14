using Fragile.Core;
using Fragile.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Fragile.Formats
{
    /// <summary>
    /// Format provider for the native Fragile archive format
    /// </summary>
    internal class NativeFormatProvider : FormatProvider
    {
        private const string DefaultExtension = ".frgl";
        private readonly FragileOptions _options;

        /// <summary>
        /// The format compatibility mode provided
        /// </summary>
        public override FormatCompatibility Format => FormatCompatibility.Native;

        /// <summary>
        /// Creates a new native format provider with default options
        /// </summary>
        public NativeFormatProvider()
            : this(new FragileOptions())
        {
        }

        /// <summary>
        /// Creates a new native format provider with specified options
        /// </summary>
        /// <param name="options">Archive options</param>
        public NativeFormatProvider(FragileOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// No conversion needed for native format
        /// </summary>
        public override async Task ConvertAsync(string inputPath, string outputPath, FragileOptions? options = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            options ??= _options;
            progress ??= options.Progress;
            cancellationToken = options.CancellationToken != default ? options.CancellationToken : cancellationToken;

            // No conversion needed for native format, just copy the file
            using FileStream source = new(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using FileStream destination = new(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await CopyWithProgressAsync(source, destination, progress, cancellationToken);
        }

        /// <summary>
        /// Imports an external archive to a Fragile archive
        /// </summary>
        public override async Task ImportAsync(string inputPath, string outputPath, FragileOptions? options = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(inputPath))
            {
                throw new FileNotFoundException($"Input archive not found: {inputPath}");
            }

            options ??= _options;
            progress ??= options.Progress;
            cancellationToken = options.CancellationToken != default ? options.CancellationToken : cancellationToken;

            // Create a temporary directory for extraction
            string tempDir = Path.Combine(Path.GetTempPath(), "Fragile", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                // Detect the format and extract using appropriate method
                // For now, just assume we're dealing with a native format

                // Extract the input archive to the temp directory
                using (FragileArchive inputArchive = new(inputPath, FragileArchiveMode.Read, options))
                {
                    if (options.UseParallelProcessing)
                    {
                        await ExtractParallelAsync(inputArchive, tempDir, options, progress, cancellationToken);
                    }
                    else
                    {
                        inputArchive.ExtractAll(tempDir);
                        progress?.Report(0.5);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Create a new archive with the extracted files
                using FragileArchive outputArchive = new(outputPath, FragileArchiveMode.Create, options);

                if (options.UseParallelProcessing)
                {
                    await AddDirectoryParallelAsync(outputArchive, tempDir, options, progress, cancellationToken);
                }
                else
                {
                    outputArchive.AddDirectory(tempDir, "", true);
                    outputArchive.Save();
                    progress?.Report(1.0);
                }
            }
            finally
            {
                // Clean up temp directory
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        /// <summary>
        /// Exports a Fragile archive to the format-compatible output archive
        /// </summary>
        public override async Task ExportAsync(string inputPath, string outputPath, FragileOptions? options = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            options ??= _options;
            progress ??= options.Progress;
            cancellationToken = options.CancellationToken != default ? options.CancellationToken : cancellationToken;

            // For native format, this is just a copy operation
            await ConvertAsync(inputPath, outputPath, options, progress, cancellationToken);
        }

        /// <summary>
        /// Checks if the file is a valid Fragile archive
        /// </summary>
        public override bool CanRead(string archivePath)
        {
            if (!File.Exists(archivePath))
            {
                return false;
            }

            try
            {
                // Check file signature
                using FileStream stream = new(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (stream.Length < 4)
                {
                    return false;
                }

                byte[] signature = new byte[4];
                stream.Read(signature, 0, 4);
                return Encoding.ASCII.GetString(signature) == "FRGL";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the default extension for Fragile archives
        /// </summary>
        public override string GetDefaultExtension()
        {
            return DefaultExtension;
        }

        /// <summary>
        /// Helper method to copy a stream with progress reporting
        /// </summary>
        private static async Task CopyWithProgressAsync(Stream source, Stream destination, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[81920]; // 80 KB buffer

            // If source stream supports seeking, we can report progress
            bool canReportProgress = source.CanSeek;
            long totalBytes = canReportProgress ? source.Length : 0;
            long totalBytesRead = 0;

            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);

                // Report progress if possible
                if (canReportProgress && progress != null)
                {
                    totalBytesRead += bytesRead;
                    double progressValue = (double)totalBytesRead / totalBytes;
                    progress.Report(progressValue);
                }

                // Check for cancellation
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        /// <summary>
        /// Extracts files from archive in parallel
        /// </summary>
        private async Task ExtractParallelAsync(FragileArchive archive, string outputDir, FragileOptions options, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            List<FragileArchiveEntry> entries = archive.Entries.ToList();
            int totalEntries = entries.Count;
            int completedEntries = 0;

            // Create semaphore to limit concurrency based on options
            int maxThreads = Math.Min(options.MaxThreads, Environment.ProcessorCount);
            using SemaphoreSlim semaphore = new(maxThreads);
            List<Task> tasks = new();

            foreach (FragileArchiveEntry? entry in entries)
            {
                await semaphore.WaitAsync(cancellationToken);

                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        // Get the full path for the extraction destination
                        string fullPath = Path.Combine(outputDir, entry.Path);

                        // Create directory if it doesn't exist
                        string? directory = Path.GetDirectoryName(fullPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        // Extract the file
                        if (!entry.IsDirectory)
                        {
                            archive.Extract(entry.Path, fullPath);
                        }
                        else
                        {
                            Directory.CreateDirectory(fullPath);
                        }

                        // Update progress
                        int current = Interlocked.Increment(ref completedEntries);
                        progress?.Report(current / (double)totalEntries * 0.5);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Adds files to an archive in parallel
        /// </summary>
        private async Task AddDirectoryParallelAsync(FragileArchive archive, string sourceDir, FragileOptions options, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            string[] files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            int totalFiles = files.Length;
            int completedFiles = 0;

            // Create semaphore to limit concurrency based on options
            int maxThreads = Math.Min(options.MaxThreads, Environment.ProcessorCount);
            using SemaphoreSlim semaphore = new(maxThreads);
            List<Task> tasks = new();

            foreach (string? file in files)
            {
                await semaphore.WaitAsync(cancellationToken);

                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        // Calculate the relative path within the archive
                        string relativePath = file[sourceDir.Length..].TrimStart(Path.DirectorySeparatorChar);

                        // Add the file to the archive
                        archive.AddFile(file, relativePath);

                        // Update progress
                        int current = Interlocked.Increment(ref completedFiles);
                        progress?.Report(0.5 + (current / (double)totalFiles * 0.5));
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(tasks);

            // Save the archive after all files are added
            archive.Save();
        }
    }
}