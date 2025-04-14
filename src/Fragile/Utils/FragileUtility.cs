using Fragile.Core;
using Fragile.Models;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Fragile.Utils
{
    /// <summary>
    /// Helper class for Fragile archiving operations
    /// </summary>
    public static class FragileUtility
    {
        /// <summary>
        /// Default file extension specific to the Fragile archiving format
        /// </summary>
        public const string DefaultExtension = ".frgl";

        /// <summary>
        /// Adds a file or directory to a Fragile archive
        /// </summary>
        /// <param name="sourcePath">Path to the file or directory to archive</param>
        /// <param name="archivePath">Path to the archive file to create (if null, source name + .frgl is used)</param>
        /// <param name="recursive">If archiving a directory, include subdirectories?</param>
        /// <returns>Total number of files added</returns>
        public static int CreateArchive(string sourcePath, string? archivePath = null, bool recursive = true)
        {
            return CreateArchive(sourcePath, archivePath, recursive, new FragileOptions());
        }

        /// <summary>
        /// Adds a file or directory to a Fragile archive with specified options
        /// </summary>
        /// <param name="sourcePath">Path to the file or directory to archive</param>
        /// <param name="archivePath">Path to the archive file to create (if null, source name + .frgl is used)</param>
        /// <param name="recursive">If archiving a directory, include subdirectories?</param>
        /// <param name="options">Archive options for compression, encryption, etc.</param>
        /// <returns>Total number of files added</returns>
        public static int CreateArchive(string sourcePath, string? archivePath = null, bool recursive = true, FragileOptions? options = null)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                throw new ArgumentException("Source path cannot be empty", nameof(sourcePath));
            }

            if (archivePath == null)
            {
                // Source file/directory name + .frgl extension
                archivePath = Path.ChangeExtension(sourcePath, DefaultExtension);
            }

            options ??= new FragileOptions();

            using FragileArchive archive = new(archivePath, FragileArchiveMode.Create, options);
            int count = 0;

            if (File.Exists(sourcePath))
            {
                archive.AddFile(sourcePath);
                count = 1;
            }
            else if (Directory.Exists(sourcePath))
            {
                count = archive.AddDirectory(sourcePath, Path.GetFileName(sourcePath), recursive);
            }
            else
            {
                throw new FileNotFoundException($"Specified source path not found: {sourcePath}");
            }

            archive.Save();
            return count;
        }

        /// <summary>
        /// Asynchronously adds a file or directory to a Fragile archive with specified options
        /// </summary>
        /// <param name="sourcePath">Path to the file or directory to archive</param>
        /// <param name="archivePath">Path to the archive file to create (if null, source name + .frgl is used)</param>
        /// <param name="recursive">If archiving a directory, include subdirectories?</param>
        /// <param name="options">Archive options for compression, encryption, etc.</param>
        /// <returns>Task representing the asynchronous operation with total number of files added</returns>
        public static async Task<int> CreateArchiveAsync(string sourcePath, string? archivePath = null, bool recursive = true, FragileOptions? options = null)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                throw new ArgumentException("Source path cannot be empty", nameof(sourcePath));
            }

            if (archivePath == null)
            {
                // Source file/directory name + .frgl extension
                archivePath = Path.ChangeExtension(sourcePath, DefaultExtension);
            }

            options ??= new FragileOptions();

            using FragileArchive archive = await FragileArchive.CreateAsync(archivePath, options);
            int count = 0;

            if (File.Exists(sourcePath))
            {
                await archive.AddFileAsync(sourcePath);
                count = 1;
            }
            else if (Directory.Exists(sourcePath))
            {
                count = await archive.AddDirectoryAsync(sourcePath, Path.GetFileName(sourcePath), recursive);
            }
            else
            {
                throw new FileNotFoundException($"Specified source path not found: {sourcePath}");
            }

            await archive.SaveAsync();
            return count;
        }

        /// <summary>
        /// Extracts a Fragile archive to the specified target directory
        /// </summary>
        /// <param name="archivePath">Path to the archive file</param>
        /// <param name="destinationPath">Target directory (if null, archive name is used)</param>
        public static void ExtractArchive(string archivePath, string? destinationPath = null)
        {
            ExtractArchive(archivePath, destinationPath, new FragileOptions());
        }

        /// <summary>
        /// Extracts a Fragile archive to the specified target directory with specified options
        /// </summary>
        /// <param name="archivePath">Path to the archive file</param>
        /// <param name="destinationPath">Target directory (if null, archive name is used)</param>
        /// <param name="options">Archive options for decompression, decryption, etc.</param>
        public static void ExtractArchive(string archivePath, string? destinationPath = null, FragileOptions? options = null)
        {
            if (string.IsNullOrEmpty(archivePath))
            {
                throw new ArgumentException("Archive file path cannot be empty", nameof(archivePath));
            }

            if (!File.Exists(archivePath))
            {
                throw new FileNotFoundException($"Archive file not found: {archivePath}");
            }

            if (destinationPath == null)
            {
                // Use archive file name (without extension)
                string dirName = Path.GetFileNameWithoutExtension(archivePath);
                destinationPath = Path.Combine(Path.GetDirectoryName(archivePath) ?? "", dirName);
            }

            options ??= new FragileOptions();

            using FragileArchive archive = new(archivePath, FragileArchiveMode.Read, options);
            archive.ExtractAll(destinationPath);
        }

        /// <summary>
        /// Asynchronously extracts a Fragile archive to the specified target directory with specified options
        /// </summary>
        /// <param name="archivePath">Path to the archive file</param>
        /// <param name="destinationPath">Target directory (if null, archive name is used)</param>
        /// <param name="options">Archive options for decompression, decryption, etc.</param>
        /// <returns>Task representing the asynchronous operation</returns>
        public static async Task ExtractArchiveAsync(string archivePath, string? destinationPath = null, FragileOptions? options = null)
        {
            if (string.IsNullOrEmpty(archivePath))
            {
                throw new ArgumentException("Archive file path cannot be empty", nameof(archivePath));
            }

            if (!File.Exists(archivePath))
            {
                throw new FileNotFoundException($"Archive file not found: {archivePath}");
            }

            if (destinationPath == null)
            {
                // Use archive file name (without extension)
                string dirName = Path.GetFileNameWithoutExtension(archivePath);
                destinationPath = Path.Combine(Path.GetDirectoryName(archivePath) ?? "", dirName);
            }

            options ??= new FragileOptions();

            using FragileArchive archive = await FragileArchive.OpenAsync(archivePath, options);
            await archive.ExtractAllAsync(destinationPath);
        }

        /// <summary>
        /// Checks if the given file is a Fragile archive by checking the file extension
        /// </summary>
        /// <param name="filePath">Path to the file to check</param>
        /// <returns>True if it's a Fragile archive</returns>
        public static bool IsFragileArchive(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            // Check extension first
            if (!filePath.EndsWith(DefaultExtension, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                // Check file header
                using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
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
        /// Splits a Fragile archive into multiple parts
        /// </summary>
        /// <param name="archivePath">Path to the archive file</param>
        /// <param name="outputDirectory">Directory to save the split parts (if null, the same directory as the archive is used)</param>
        /// <param name="splitSize">Size of each part in bytes</param>
        /// <returns>Collection of archive parts</returns>
        public static async Task<FragileArchivePartCollection> SplitArchiveAsync(string archivePath, string? outputDirectory = null, long splitSize = 0)
        {
            return await SplitArchiveAsync(archivePath, outputDirectory, new FragileOptions { SplitSize = splitSize > 0 ? splitSize : 100 * 1024 * 1024 }); // Default 100MB if not specified
        }

        /// <summary>
        /// Splits a Fragile archive into multiple parts with specified options
        /// </summary>
        /// <param name="archivePath">Path to the archive file</param>
        /// <param name="outputDirectory">Directory to save the split parts (if null, the same directory as the archive is used)</param>
        /// <param name="options">Archive options including SplitSize</param>
        /// <returns>Collection of archive parts</returns>
        public static async Task<FragileArchivePartCollection> SplitArchiveAsync(string archivePath, string? outputDirectory = null, FragileOptions? options = null)
        {
            if (string.IsNullOrEmpty(archivePath))
            {
                throw new ArgumentException("Archive file path cannot be empty", nameof(archivePath));
            }

            if (!File.Exists(archivePath))
            {
                throw new FileNotFoundException($"Archive file not found: {archivePath}");
            }

            options ??= new FragileOptions { SplitSize = 100 * 1024 * 1024 }; // Default 100MB if not specified

            // Ensure SplitSize is set
            if (options.SplitSize <= 0)
            {
                options.SplitSize = 100 * 1024 * 1024; // 100MB default
            }

            using FragileArchive archive = await FragileArchive.OpenAsync(archivePath, options);
            return await archive.SplitAsync(outputDirectory);
        }

        /// <summary>
        /// Combines multiple archive parts into a single archive
        /// </summary>
        /// <param name="firstPartPath">Path to the first part file</param>
        /// <param name="outputPath">Path to the output archive (if null, first part name without part suffix is used)</param>
        /// <param name="options">Archive options</param>
        /// <returns>Task representing the asynchronous operation</returns>
        public static async Task CombinePartsAsync(string firstPartPath, string? outputPath = null, FragileOptions? options = null)
        {
            if (string.IsNullOrEmpty(firstPartPath))
            {
                throw new ArgumentException("First part path cannot be empty", nameof(firstPartPath));
            }

            if (!File.Exists(firstPartPath))
            {
                throw new FileNotFoundException($"Part file not found: {firstPartPath}");
            }

            options ??= new FragileOptions();

            // Find all parts based on the first part
            FragileArchivePartCollection parts = FragileArchivePartCollection.FindParts(firstPartPath, options);

            if (parts.Count == 0)
            {
                throw new InvalidOperationException($"No valid parts found for {firstPartPath}");
            }

            // If output path is not specified, use first part name without part suffix
            if (outputPath == null)
            {
                string directory = Path.GetDirectoryName(firstPartPath) ?? "";
                string fileName = Path.GetFileName(firstPartPath);

                // Extract base name by removing part suffix (like .part001)
                string baseName = fileName;
                int partIndex = fileName.IndexOf(".part", StringComparison.OrdinalIgnoreCase);
                if (partIndex > 0)
                {
                    baseName = fileName[..partIndex];
                }

                outputPath = Path.Combine(directory, baseName);
            }

            // Combine parts
            await parts.CombinePartsAsync(outputPath, options.Progress, options.CancellationToken);
        }
    }
}