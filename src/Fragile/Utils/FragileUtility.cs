using Fragile.Core;
using Fragile.Helpers;
using Fragile.Models;

namespace Fragile.Utils
{
    /// <summary>
    /// Helper class for Fragile archiving operations
    /// </summary>
    public static class FragileUtility
    {
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

            options ??= new FragileOptions();

            // Source file/directory name + .frgl extension
            archivePath ??= FragilePath.CreateArchivePathFromDirectory(sourcePath, options);

            // Ensure the archive path has the correct extension
            archivePath = FragilePath.EnsureArchiveExtension(archivePath, options);

            using FragileArchive archive = new(archivePath, FragileArchiveMode.Create, options);

            int count = 0;

            if (FragilePath.IsFile(sourcePath))
            {
                archive.AddFile(sourcePath);
                count = 1;
            }
            else if (FragilePath.IsDirectory(sourcePath))
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
        /// Asynchronously adds a file or directory to a Fragile archive
        /// </summary>
        /// <param name="sourcePath">Path to the file or directory to archive</param>
        /// <param name="archivePath">Path to the archive file to create (if null, source name + .frgl is used)</param>
        /// <param name="recursive">If archiving a directory, include subdirectories?</param>
        /// <returns>Total number of files added</returns>
        public static async Task<int> CreateArchiveAsync(string sourcePath, string? archivePath = null, bool recursive = true)
        {
            return await CreateArchiveAsync(sourcePath, archivePath, recursive, new FragileOptions());
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

            options ??= new FragileOptions();

            // Source file/directory name + .frgl extension
            archivePath ??= FragilePath.CreateArchivePathFromDirectory(sourcePath, options);

            // Ensure the archive path has the correct extension
            archivePath = FragilePath.EnsureArchiveExtension(archivePath, options);

            using FragileArchive archive = await FragileArchive.CreateAsync(archivePath, options);

            int count = 0;

            if (FragilePath.IsFile(sourcePath))
            {
                await archive.AddFileAsync(sourcePath);
                count = 1;
            }
            else if (FragilePath.IsDirectory(sourcePath))
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

            if (!FragilePath.IsFile(archivePath))
            {
                throw new FileNotFoundException($"Archive file not found: {archivePath}");
            }

            options ??= new FragileOptions();

            // Use the created destination directory path
            destinationPath = FragilePath.CreateExtractionPath(archivePath, destinationPath);

            // Ensure the archive path has the correct extension
            archivePath = FragilePath.EnsureArchiveExtension(archivePath, options);

            using FragileArchive archive = new(archivePath, FragileArchiveMode.Read, options);

            archive.ExtractAll(destinationPath);
        }

        /// <summary>
        /// Asynchronously extracts a Fragile archive to the specified target directory
        /// </summary>
        /// <param name="archivePath">Path to the archive file</param>
        /// <param name="destinationPath">Target directory (if null, archive name is used)</param>
        /// <returns>Task representing the asynchronous operation</returns>
        public static async Task ExtractArchiveAsync(string archivePath, string? destinationPath = null)
        {
            await ExtractArchiveAsync(archivePath, destinationPath, new FragileOptions());
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

            if (!FragilePath.IsFile(archivePath))
            {
                throw new FileNotFoundException($"Archive file not found: {archivePath}");
            }

            options ??= new FragileOptions();

            // Use the created destination directory path
            destinationPath = FragilePath.CreateExtractionPath(archivePath, destinationPath);

            // Ensure the archive path has the correct extension
            archivePath = FragilePath.EnsureArchiveExtension(archivePath, options);

            using FragileArchive archive = await FragileArchive.OpenAsync(archivePath, options);

            await archive.ExtractAllAsync(destinationPath);
        }

        /// <summary>
        /// Checks if the given file is a Fragile archive by checking the file extension
        /// </summary>
        /// <param name="filePath">Path to the file to check</param>
        /// <param name="options">Archive options for checking</param>
        /// <returns>True if it's a Fragile archive</returns>
        public static bool IsFragileArchive(string filePath, FragileOptions? options = null)
        {
            if (string.IsNullOrEmpty(filePath) || !FragilePath.IsFile(filePath))
            {
                return false;
            }

            options ??= new FragileOptions();

            // Extension check
            if (!FragilePath.IsValidArchivePath(filePath, options))
            {
                return false;
            }

            // Signature check
            return FragileSignature.CheckArchiveSignature(filePath, options);
        }

        /// <summary>
        /// Asynchronously checks if the given file is a Fragile archive by checking the file extension
        /// </summary>
        /// <param name="filePath">Path to the file to check</param>
        /// <param name="options">Archive options for checking</param>
        /// <returns>True if it's a Fragile archive</returns>
        public static async Task<bool> IsFragileArchiveAsync(string filePath, FragileOptions? options = null)
        {
            if (string.IsNullOrEmpty(filePath) || !FragilePath.IsFile(filePath))
            {
                return false;
            }

            options ??= new FragileOptions();

            // Extension check
            if (!FragilePath.IsValidArchivePath(filePath, options))
            {
                return false;
            }

            // Signature check
            return await FragileSignature.CheckArchiveSignatureAsync(filePath, options);
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

            if (!FragilePath.IsFile(archivePath))
            {
                throw new FileNotFoundException($"Archive file not found: {archivePath}");
            }

            options ??= new FragileOptions { SplitSize = 100 * 1024 * 1024 }; // Default 100MB if not specified

            // Ensure the archive path has the correct extension
            archivePath = FragilePath.EnsureArchiveExtension(archivePath, options);

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

            if (!FragilePath.IsFile(firstPartPath))
            {
                throw new FileNotFoundException($"Part file not found: {firstPartPath}");
            }

            options ??= new FragileOptions();

            // Ensure the first archive part path has the correct extension
            firstPartPath = FragilePath.EnsureArchiveExtension(firstPartPath, options);

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
                int partIndex = fileName.IndexOf(options.SplitName, StringComparison.OrdinalIgnoreCase);
                if (partIndex > 0)
                {
#if NET48_OR_GREATER || NETSTANDARD2_0
                    baseName = fileName.Substring(0, partIndex);
#else
                    baseName = fileName[..partIndex];
#endif
                }

                outputPath = Path.Combine(directory, baseName);

                // Ensure the combined archive path has the correct extension
                outputPath = FragilePath.EnsureArchiveExtension(outputPath, options);
            }

            // Combine parts
            await parts.CombinePartsAsync(outputPath, options.Progress, options.CancellationToken);
        }

        /// <summary>
        /// Creates a Fragile archive directly split into multiple parts
        /// </summary>
        /// <param name="sourcePath">Path to the file or directory to archive</param>
        /// <param name="archivePath">Path to the archive file to create (if null, source name + .frgl is used)</param>
        /// <param name="recursive">If archiving a directory, include subdirectories?</param>
        /// <param name="splitSize">Size of each part in bytes (must be greater than 0)</param>
        /// <returns>Collection of archive parts</returns>
        public static async Task<FragileArchivePartCollection> CreateSplitArchiveAsync(string sourcePath, string? archivePath = null, bool recursive = true, long splitSize = 0)
        {
            return await CreateSplitArchiveAsync(sourcePath, archivePath, recursive, new FragileOptions { SplitSize = splitSize > 0 ? splitSize : 100 * 1024 * 1024 }); // Default 100MB if not specified
        }

        /// <summary>
        /// Creates a Fragile archive directly split into multiple parts
        /// </summary>
        /// <param name="sourcePath">Path to the file or directory to archive</param>
        /// <param name="archivePath">Path to the archive file to create (if null, source name + .frgl is used)</param>
        /// <param name="recursive">If archiving a directory, include subdirectories?</param>
        /// <param name="options">Archive options including SplitSize (must be greater than 0)</param>
        /// <returns>Collection of archive parts</returns>
        public static async Task<FragileArchivePartCollection> CreateSplitArchiveAsync(string sourcePath, string? archivePath = null, bool recursive = true, FragileOptions? options = null)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                throw new ArgumentException("Source path cannot be empty", nameof(sourcePath));
            }

            options ??= new FragileOptions { SplitSize = 100 * 1024 * 1024 }; // Default to 100MB

            // Validate split size
            if (options.SplitSize <= 0)
            {
                throw new ArgumentException("Split size must be greater than zero", nameof(options.SplitSize));
            }

            // Source file/directory name + .frgl extension
            archivePath ??= FragilePath.CreateArchivePathFromDirectory(sourcePath, options);

            // Ensure the archive path has the correct extension
            archivePath = FragilePath.EnsureArchiveExtension(archivePath, options);

            // Create temp directory for the original archive
            string tempDirectory = FragilePath.CreateTempDirectoryPath(options);
            Directory.CreateDirectory(tempDirectory);

            try
            {
                // Create the temporary archive
                string tempArchivePath = Path.Combine(tempDirectory, Path.GetFileName(archivePath));

                // Create the archive
                await CreateArchiveAsync(sourcePath, tempArchivePath, recursive, options);

                // Split it with provided options
                string outputDirectory = Path.GetDirectoryName(archivePath) ?? "";

                // Check if the temporary archive was already split (and deleted)
                if (!FragilePath.IsFile(tempArchivePath))
                {
                    // The archive was already split during creation
                    // We need to find the generated parts and move them to the output directory
                    string tempSearchPattern = Path.GetFileNameWithoutExtension(tempArchivePath) + $"*{options.SplitName}*";
                    string[] partFiles = Directory.GetFiles(tempDirectory, tempSearchPattern);

                    if (partFiles.Length == 0)
                    {
                        throw new InvalidOperationException("Failed to find split archive parts in temporary directory");
                    }

                    // Create the output directory if it doesn't exist
                    FragilePath.EnsureDirectoryExists(outputDirectory);

                    // Create a part collection to return
                    FragileArchivePartCollection partCollection = new(options);

                    // Move and add the parts
                    foreach (string partFile in partFiles)
                    {
                        string partFileName = Path.GetFileName(partFile);
                        string destPartFile = Path.Combine(outputDirectory, partFileName);

                        // Move the part
                        File.Move(partFile, destPartFile);

                        // Create and add the part to the collection
                        // We need to parse the part details from the filename
                        FragileArchivePart part = FragileArchivePart.FromFileName(destPartFile, options.SplitName);
                        partCollection.Add(part);
                    }

                    return partCollection;
                }
                else
                {
                    // The archive was created but not split yet
                    using FragileArchive archive = await FragileArchive.OpenAsync(tempArchivePath, options);

                    return await archive.SplitAsync(outputDirectory);
                }
            }
            finally
            {
                // Clean up temp directory 
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