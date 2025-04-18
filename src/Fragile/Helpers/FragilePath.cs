using Fragile.Models;

namespace Fragile.Helpers
{
    /// <summary>
    /// Helper class for file and folder path operations in the Fragile archive system
    /// </summary>
    internal static class FragilePath
    {
        /// <summary>
        /// Checks if a file path is a valid Fragile archive file
        /// </summary>
        /// <param name="path">File path to check</param>
        /// <param name="options">Archive options</param>
        /// <returns>True if it's a valid archive file</returns>
        public static bool IsValidArchivePath(string path, FragileOptions? options = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            options ??= new FragileOptions();

            // Extension check
            return path.EndsWith(options.Extension, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the specified path is a file
        /// </summary>
        /// <param name="path">Path to check</param>
        /// <returns>True if it's a file, false otherwise</returns>
        public static bool IsFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            return File.Exists(path);
        }

        /// <summary>
        /// Checks if the specified path is a directory
        /// </summary>
        /// <param name="path">Path to check</param>
        /// <returns>True if it's a directory, false otherwise</returns>
        public static bool IsDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            return Directory.Exists(path);
        }

        /// <summary>
        /// Normalizes a given path as an archive path
        /// </summary>
        /// <param name="path">Path to normalize</param>
        /// <param name="options">Archive options</param>
        /// <returns>Normalized archive path</returns>
        public static string NormalizeArchivePath(string path, FragileOptions? options = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            options ??= new FragileOptions();

            // Normalize path
            path = NormalizePath(path);

            // Extension check and correction
            if (!path.EndsWith(options.Extension, StringComparison.OrdinalIgnoreCase))
            {
                path = Path.ChangeExtension(path, options.Extension);
            }

            return path;
        }

        /// <summary>
        /// Normalizes a given path to internal archive format
        /// (Cleans null characters and invalid characters, uses '/' instead of '\\')
        /// </summary>
        /// <param name="path">Path to normalize</param>
        /// <returns>Normalized path</returns>
        public static string NormalizePath(string path)
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

            // Use forward slashes instead of Windows path separators
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
        /// Creates an archive entry path for adding the specified location to the archive
        /// </summary>
        /// <param name="sourcePath">Source file or folder path</param>
        /// <param name="entryPath">Target path in archive (if null, file/folder name is used)</param>
        /// <returns>Normalized archive entry path</returns>
        public static string CreateEntryPath(string sourcePath, string? entryPath = null)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                return string.Empty;
            }

            entryPath ??= Path.GetFileName(sourcePath);

            return NormalizePath(entryPath);
        }

        /// <summary>
        /// Creates an archive file path from a directory path
        /// </summary>
        /// <param name="directoryPath">Directory path</param>
        /// <param name="options">Archive options</param>
        /// <returns>Archive file path</returns>
        public static string CreateArchivePathFromDirectory(string directoryPath, FragileOptions? options = null)
        {
            if (string.IsNullOrEmpty(directoryPath))
            {
                throw new ArgumentException("Directory path cannot be empty.", nameof(directoryPath));
            }

            options ??= new FragileOptions();
            return Path.ChangeExtension(directoryPath, options.Extension);
        }

        /// <summary>
        /// Creates a destination directory path for extraction from an archive path
        /// </summary>
        /// <param name="archivePath">Archive file path</param>
        /// <param name="destinationPath">Destination directory path (if null, archive name is used)</param>
        /// <returns>Destination directory path for extraction</returns>
        public static string CreateExtractionPath(string archivePath, string? destinationPath = null)
        {
            if (string.IsNullOrEmpty(archivePath))
            {
                throw new ArgumentException("Archive path cannot be empty.", nameof(archivePath));
            }

            if (destinationPath == null)
            {
                // Use archive filename without extension
                string dirName = Path.GetFileNameWithoutExtension(archivePath);
                destinationPath = Path.Combine(Path.GetDirectoryName(archivePath) ?? "", dirName);
            }

            return destinationPath;
        }

        /// <summary>
        /// Adds or changes the archive extension for the specified path
        /// </summary>
        /// <param name="path">Path to process</param>
        /// <param name="options">Archive options</param>
        /// <returns>Path with archive extension</returns>
        public static string EnsureArchiveExtension(string path, FragileOptions? options = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            options ??= new FragileOptions();

            // Extension check
            if (!path.EndsWith(options.Extension, StringComparison.OrdinalIgnoreCase))
            {
                path = Path.ChangeExtension(path, options.Extension);
            }

            return path;
        }

        /// <summary>
        /// Checks if the directory exists for the specified path, creates if not
        /// </summary>
        /// <param name="path">File or directory path</param>
        /// <returns>Directory path</returns>
        public static string EnsureDirectoryExists(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            string directory;

            if (IsDirectory(path))
            {
                directory = path;
            }
            else
            {
                // Get directory path from file path
                directory = Path.GetDirectoryName(path);
            }

            // Create directory if it doesn't exist and path is not empty
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return directory;
        }

        /// <summary>
        /// Creates a temporary file path for archive location
        /// </summary>
        /// <param name="options">Archive options</param>
        /// <returns>Temporary file path</returns>
        public static string CreateTempFilePath(FragileOptions? options = null)
        {
            options ??= new FragileOptions();

            return Path.Combine(options.TempDirectory, $"Fragile_{Guid.NewGuid()}{options.Extension}");
        }

        /// <summary>
        /// Creates a temporary directory path for archive location
        /// </summary>
        /// <param name="options">Archive options</param>
        /// <returns>Temporary directory path</returns>
        public static string CreateTempDirectoryPath(FragileOptions? options = null)
        {
            options ??= new FragileOptions();

            return Path.Combine(options.TempDirectory, $"Fragile_{Guid.NewGuid()}");
        }
    }
}