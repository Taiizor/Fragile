using Fragile.Core;
using Fragile.Helpers;
using Fragile.Models;

namespace Fragile.Utils
{
    /// <summary>
    /// Helper class that manages Fragile archiving operations
    /// </summary>
    public static class FragileUtility
    {
        /// <summary>
        /// Creates an archive from a specified file or folder
        /// </summary>
        /// <param name="sourcePath">Path of the file or folder to be archived</param>
        /// <param name="outputDirectory">Path of the directory where the archive will be saved</param>
        /// <param name="recursive">Whether to include subdirectories when archiving a folder</param>
        /// <returns>Total number of files added to the archive</returns>
        public static int CreateArchive(string sourcePath, string outputDirectory, bool recursive = true)
        {
            return CreateArchive(sourcePath, outputDirectory, recursive, new FragileOptions());
        }

        /// <summary>
        /// Creates an archive from a specified file or folder using custom options
        /// </summary>
        /// <param name="sourcePath">Path of the file or folder to be archived</param>
        /// <param name="outputDirectory">Path of the directory where the archive will be saved</param>
        /// <param name="recursive">Whether to include subdirectories when archiving a folder</param>
        /// <param name="options">Archive options (compression, encryption etc.)</param>
        /// <returns>Total number of files added to the archive</returns>
        public static int CreateArchive(string sourcePath, string outputDirectory, bool recursive = true, FragileOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("Source path cannot be empty.", nameof(sourcePath));
            }

            // Check if source path exists
            if (!FragilePath2.IsFile(sourcePath) && !FragilePath2.IsDirectory(sourcePath))
            {
                throw new FileNotFoundException($"Specified source not found: {sourcePath}");
            }

            options ??= new FragileOptions();

            // Set source file/folder name for FileName
            options.FileName = Path.GetFileNameWithoutExtension(sourcePath);

            // Validate output directory and get full archive path
            string archivePath = FragilePath2.ValidateAndGetArchivePath(outputDirectory, options);

            // Create archive
            using FragileArchive archive = new(archivePath, FragileArchiveMode.Create, options);

            int count = 0;

            if (FragilePath2.IsFile(sourcePath))
            {
                archive.AddFile(sourcePath);
                count = 1;
            }
            else // Folder
            {
                count = archive.AddDirectory(sourcePath, Path.GetFileName(sourcePath), recursive);
            }

            archive.Save();

            return count;
        }

        /// <summary>
        /// Creates an archive from a specified file or folder (async)
        /// </summary>
        /// <param name="sourcePath">Path of the file or folder to be archived</param>
        /// <param name="outputDirectory">Path of the directory where the archive will be saved</param>
        /// <param name="recursive">Whether to include subdirectories when archiving a folder</param>
        /// <returns>Total number of files added to the archive</returns>
        public static async Task<int> CreateArchiveAsync(string sourcePath, string outputDirectory, bool recursive = true)
        {
            return await CreateArchiveAsync(sourcePath, outputDirectory, recursive, new FragileOptions());
        }

        /// <summary>
        /// Creates an archive from a specified file or folder using custom options (async)
        /// </summary>
        /// <param name="sourcePath">Path of the file or folder to be archived</param>
        /// <param name="outputDirectory">Path of the directory where the archive will be saved</param>
        /// <param name="recursive">Whether to include subdirectories when archiving a folder</param>
        /// <param name="options">Archive options (compression, encryption etc.)</param>
        /// <returns>Total number of files added to the archive</returns>
        public static async Task<int> CreateArchiveAsync(string sourcePath, string outputDirectory, bool recursive = true, FragileOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("Source path cannot be empty.", nameof(sourcePath));
            }

            // Check if source path exists
            if (!FragilePath2.IsFile(sourcePath) && !FragilePath2.IsDirectory(sourcePath))
            {
                throw new FileNotFoundException($"Specified source not found: {sourcePath}");
            }

            options ??= new FragileOptions();

            // Set source file/folder name for FileName
            options.FileName = Path.GetFileNameWithoutExtension(sourcePath);

            // Validate output directory and get full archive path
            string archivePath = FragilePath2.ValidateAndGetArchivePath(outputDirectory, options);

            // Create archive
            using FragileArchive archive = await FragileArchive.CreateAsync(archivePath, options);

            int count = 0;

            if (FragilePath2.IsFile(sourcePath))
            {
                await archive.AddFileAsync(sourcePath);
                count = 1;
            }
            else // Folder
            {
                count = await archive.AddDirectoryAsync(sourcePath, Path.GetFileName(sourcePath), recursive);
            }

            await archive.SaveAsync();

            return count;
        }

        /// <summary>
        /// Extracts a Fragile archive to the specified target directory
        /// </summary>
        /// <param name="archivePath">Path of the archive file</param>
        /// <param name="destinationPath">Path of the target directory</param>
        public static void ExtractArchive(string archivePath, string destinationPath)
        {
            ExtractArchive(archivePath, destinationPath, new FragileOptions());
        }

        /// <summary>
        /// Extracts a Fragile archive to the specified target directory using custom options
        /// </summary>
        /// <param name="archivePath">Path of the archive file</param>
        /// <param name="destinationPath">Path of the target directory</param>
        /// <param name="options">Archive options (decryption etc.)</param>
        public static void ExtractArchive(string archivePath, string destinationPath, FragileOptions? options = null)
        {
            // Validate archive file exists
            FragilePath2.ValidateFileExists(archivePath);

            options ??= new FragileOptions();

            // Validate target directory (should be empty or created if not exists)
            FragilePath2.ValidateExtractionDirectory(destinationPath, true);

            using FragileArchive archive = new(archivePath, FragileArchiveMode.Read, options);
            archive.ExtractAll(destinationPath);
        }

        /// <summary>
        /// Extracts a Fragile archive to the specified target directory (async)
        /// </summary>
        /// <param name="archivePath">Path of the archive file</param>
        /// <param name="destinationPath">Path of the target directory</param>
        /// <returns>Task representing the async operation</returns>
        public static async Task ExtractArchiveAsync(string archivePath, string destinationPath)
        {
            await ExtractArchiveAsync(archivePath, destinationPath, new FragileOptions());
        }

        /// <summary>
        /// Extracts a Fragile archive to the specified target directory using custom options (async)
        /// </summary>
        /// <param name="archivePath">Path of the archive file</param>
        /// <param name="destinationPath">Path of the target directory</param>
        /// <param name="options">Archive options (decryption etc.)</param>
        /// <returns>Task representing the async operation</returns>
        public static async Task ExtractArchiveAsync(string archivePath, string destinationPath, FragileOptions? options = null)
        {
            // Validate archive file exists
            FragilePath2.ValidateFileExists(archivePath);

            options ??= new FragileOptions();

            // Validate target directory (should be empty or created if not exists)
            FragilePath2.ValidateExtractionDirectory(destinationPath, true);

            using FragileArchive archive = await FragileArchive.OpenAsync(archivePath, options);
            await archive.ExtractAllAsync(destinationPath);
        }

        /// <summary>
        /// Checks if the given file is a Fragile archive
        /// </summary>
        /// <param name="filePath">Path of the file to check</param>
        /// <param name="options">Archive options</param>
        /// <returns>True if it's a Fragile archive</returns>
        public static bool IsFragileArchive(string filePath, FragileOptions? options = null)
        {
            if (!FragilePath2.IsFile(filePath))
            {
                return false;
            }

            options ??= new FragileOptions();

            // Extension check
            if (!filePath.EndsWith(options.Extension, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Signature check
            return FragileSignature.CheckArchiveSignature(filePath, options);
        }

        /// <summary>
        /// Checks if the given file is a Fragile archive (async)
        /// </summary>
        /// <param name="filePath">Path of the file to check</param>
        /// <param name="options">Archive options</param>
        /// <returns>True if it's a Fragile archive</returns>
        public static async Task<bool> IsFragileArchiveAsync(string filePath, FragileOptions? options = null)
        {
            if (!FragilePath2.IsFile(filePath))
            {
                return false;
            }

            options ??= new FragileOptions();

            // Extension check
            if (!filePath.EndsWith(options.Extension, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Signature check
            return await FragileSignature.CheckArchiveSignatureAsync(filePath, options);
        }

        /// <summary>
        /// Splits a Fragile archive into multiple parts
        /// </summary>
        /// <param name="archivePath">Path of the archive file</param>
        /// <param name="outputDirectory">Path of the directory where parts will be saved</param>
        /// <param name="splitSize">Size of each part (in bytes)</param>
        /// <returns>Collection of archive parts</returns>
        public static async Task<FragileArchivePartCollection> SplitArchiveAsync(string archivePath, string outputDirectory, long splitSize = 0)
        {
            return await SplitArchiveAsync(archivePath, outputDirectory, new FragileOptions { SplitSize = splitSize > 0 ? splitSize : 100 * 1024 * 1024 }); // Default 100MB if not specified
        }

        /// <summary>
        /// Splits a Fragile archive into multiple parts using custom options
        /// </summary>
        /// <param name="archivePath">Path of the archive file</param>
        /// <param name="outputDirectory">Path of the directory where parts will be saved</param>
        /// <param name="options">Archive options including SplitSize</param>
        /// <returns>Collection of archive parts</returns>
        public static async Task<FragileArchivePartCollection> SplitArchiveAsync(string archivePath, string outputDirectory, FragileOptions? options = null)
        {
            // Validate archive file exists
            FragilePath2.ValidateFileExists(archivePath);

            options ??= new FragileOptions { SplitSize = 100 * 1024 * 1024 }; // Default 100MB if not specified

            // Validate split size
            if (options.SplitSize <= 0)
            {
                throw new ArgumentException("Split size must be greater than zero.", nameof(options.SplitSize));
            }

            // Validate output directory exists and is empty
            FragilePath2.ValidateDirectoryExists(outputDirectory);
            FragilePath2.ValidateDirectoryEmpty(outputDirectory);

            using FragileArchive archive = await FragileArchive.OpenAsync(archivePath, options);
            return await archive.SplitAsync(outputDirectory);
        }

        /// <summary>
        /// Combines multiple archive parts into a single archive file
        /// </summary>
        /// <param name="firstPartPath">Path of the first part file</param>
        /// <param name="outputDirectory">Path of the directory where the combined archive will be saved</param>
        /// <param name="options">Archive options</param>
        /// <returns>Task representing the async operation</returns>
        public static async Task CombinePartsAsync(string firstPartPath, string outputDirectory, FragileOptions? options = null)
        {
            // Validate first part file exists
            FragilePath2.ValidateFileExists(firstPartPath);

            options ??= new FragileOptions();

            // Validate output directory exists and is empty
            FragilePath2.ValidateDirectoryExists(outputDirectory);

            // Find parts
            FragileArchivePartCollection parts = FragileArchivePartCollection.FindParts(firstPartPath, options);

            if (parts.Count == 0)
            {
                throw new InvalidOperationException($"No valid parts found for file: {firstPartPath}");
            }

            // Extract base name (remove part suffix like .part001)
            string baseName = Path.GetFileName(firstPartPath);
            int partIndex = baseName.IndexOf(options.SplitName, StringComparison.OrdinalIgnoreCase);
            if (partIndex > 0)
            {
#if NET48_OR_GREATER || NETSTANDARD2_0
                baseName = baseName.Substring(0, partIndex);
#else
                baseName = baseName[..partIndex];
#endif
            }
            else
            {
                // If part identifier not found, remove extension
                baseName = Path.GetFileNameWithoutExtension(firstPartPath);
            }

            // Set file name
            options.FileName = baseName;

            // Validate output path
            string outputPath = FragilePath2.ValidateAndGetArchivePath(outputDirectory, options);

            // Combine parts
            await parts.CombinePartsAsync(outputPath, options.Progress, options.CancellationToken);
        }

        /// <summary>
        /// Creates a Fragile archive directly split into multiple parts
        /// </summary>
        /// <param name="sourcePath">Path of the file or folder to be archived</param>
        /// <param name="outputDirectory">Path of the directory where archive parts will be saved</param>
        /// <param name="recursive">Whether to include subdirectories when archiving a folder</param>
        /// <param name="splitSize">Size of each part (in bytes)</param>
        /// <returns>Collection of archive parts</returns>
        public static async Task<FragileArchivePartCollection> CreateSplitArchiveAsync(string sourcePath, string outputDirectory, bool recursive = true, long splitSize = 0)
        {
            return await CreateSplitArchiveAsync(sourcePath, outputDirectory, recursive, new FragileOptions { SplitSize = splitSize > 0 ? splitSize : 100 * 1024 * 1024 }); // Default 100MB if not specified
        }

        /// <summary>
        /// Creates a Fragile archive directly split into multiple parts using custom options
        /// </summary>
        /// <param name="sourcePath">Path of the file or folder to be archived</param>
        /// <param name="outputDirectory">Path of the directory where archive parts will be saved</param>
        /// <param name="recursive">Whether to include subdirectories when archiving a folder</param>
        /// <param name="options">Archive options including SplitSize</param>
        /// <returns>Collection of archive parts</returns>
        public static async Task<FragileArchivePartCollection> CreateSplitArchiveAsync(string sourcePath, string outputDirectory, bool recursive = true, FragileOptions? options = null)
        {
            // Validate source path exists
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("Source path cannot be empty.", nameof(sourcePath));
            }

            if (!FragilePath2.IsFile(sourcePath) && !FragilePath2.IsDirectory(sourcePath))
            {
                throw new FileNotFoundException($"Specified source not found: {sourcePath}");
            }

            options ??= new FragileOptions { SplitSize = 100 * 1024 * 1024 }; // Default 100MB

            // Validate split size
            if (options.SplitSize <= 0)
            {
                throw new ArgumentException("Split size must be greater than zero.", nameof(options.SplitSize));
            }

            // Validate output directory exists and is empty
            FragilePath2.ValidateDirectoryExists(outputDirectory);
            FragilePath2.ValidateDirectoryEmpty(outputDirectory);

            // Set source file/folder name for FileName
            options.FileName = Path.GetFileNameWithoutExtension(sourcePath);

            // Create a directory for temporary files
            string tempDirectory = FragilePath2.CreateTempDirectoryPath(options);

            try
            {
                // Create temporary archive
                string tempArchivePath = Path.Combine(tempDirectory, options.FileName + options.Extension);

                // Create archive
                await CreateArchiveAsync(sourcePath, tempDirectory, recursive, options);

                // Check if temporary archive already exists (split and deleted)
                if (!FragilePath2.IsFile(tempArchivePath))
                {
                    // Archive is already split and deleted during creation
                    // We need to find created parts and move them to output directory
                    string tempSearchPattern = Path.GetFileNameWithoutExtension(tempArchivePath) + $"*{options.SplitName}*";
                    string[] partFiles = Directory.GetFiles(tempDirectory, tempSearchPattern);

                    if (partFiles.Length == 0)
                    {
                        throw new InvalidOperationException("No split archive parts found in temporary directory");
                    }

                    // Create a part collection
                    FragileArchivePartCollection partCollection = new(options);

                    // Move and add parts
                    foreach (string partFile in partFiles)
                    {
                        string partFileName = Path.GetFileName(partFile);
                        string destPartFile = Path.Combine(outputDirectory, partFileName);

                        // Move part
                        File.Move(partFile, destPartFile);

                        // Add part to collection
                        // We need to extract part details from filename
                        FragileArchivePart part = FragileArchivePart.FromFileName(destPartFile, options.SplitName);
                        partCollection.Add(part);
                    }

                    return partCollection;
                }
                else
                {
                    // Archive is created but not split yet
                    using FragileArchive archive = await FragileArchive.OpenAsync(tempArchivePath, options);
                    return await archive.SplitAsync(outputDirectory);
                }
            }
            finally
            {
                // Clean up temporary directory
                try
                {
                    if (Directory.Exists(tempDirectory))
                    {
                        Directory.Delete(tempDirectory, true);
                    }
                }
                catch (IOException) { }
            }
        }
    }
}