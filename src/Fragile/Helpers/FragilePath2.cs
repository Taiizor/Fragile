using Fragile.Models;
using System.Text;

namespace Fragile.Helpers
{
    /// <summary>
    /// Helper class that manages file and folder path operations for the Fragile archive system
    /// </summary>
    public static class FragilePath2
    {
        /// <summary>
        /// Checks if the specified path is a file
        /// </summary>
        /// <param name="path">Path to check</param>
        /// <returns>True if it's a file, false otherwise</returns>
        public static bool IsFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
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
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            return Directory.Exists(path);
        }

        /// <summary>
        /// Validates that the specified path is a file and exists
        /// </summary>
        /// <param name="path">File path to check</param>
        /// <exception cref="ArgumentException">If path is empty</exception>
        /// <exception cref="FileNotFoundException">If file is not found</exception>
        public static void ValidateFileExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("File path cannot be empty.", nameof(path));
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Specified file not found: {path}");
            }
        }

        /// <summary>
        /// Validates that the specified path is a directory and exists
        /// </summary>
        /// <param name="path">Directory path to check</param>
        /// <exception cref="ArgumentException">If path is empty</exception>
        /// <exception cref="DirectoryNotFoundException">If directory is not found</exception>
        public static void ValidateDirectoryExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Directory path cannot be empty.", nameof(path));
            }

            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException($"Specified directory not found: {path}");
            }
        }

        /// <summary>
        /// Validates that the specified directory is empty (must not contain files or subdirectories)
        /// </summary>
        /// <param name="path">Directory path to check</param>
        /// <exception cref="ArgumentException">If path is empty</exception>
        /// <exception cref="DirectoryNotFoundException">If directory is not found</exception>
        /// <exception cref="InvalidOperationException">If directory is not empty</exception>
        public static void ValidateDirectoryEmpty(string path)
        {
            ValidateDirectoryExists(path);

            // Directory is empty if it has no files and no subdirectories
            if (Directory.GetFiles(path).Length > 0 || Directory.GetDirectories(path).Length > 0)
            {
                throw new InvalidOperationException($"Directory is not empty: {path}");
            }
        }

        /// <summary>
        /// Validates that the directory path exists and tries to create it if it doesn't
        /// </summary>
        /// <param name="path">Directory path</param>
        /// <exception cref="ArgumentException">If path is empty</exception>
        /// <exception cref="IOException">If directory cannot be created</exception>
        public static void EnsureDirectoryExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Directory path cannot be empty.", nameof(path));
            }

            if (!Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (Exception ex)
                {
                    throw new IOException($"Could not create directory: {path}", ex);
                }
            }
        }

        /// <summary>
        /// Validates the archive path and checks if a file with the same name exists in the target directory
        /// </summary>
        /// <param name="directoryPath">Archive directory path</param>
        /// <param name="options">Archive options</param>
        /// <exception cref="ArgumentException">If parameters are invalid</exception>
        /// <exception cref="DirectoryNotFoundException">If directory doesn't exist</exception>
        /// <exception cref="IOException">If file already exists</exception>
        /// <returns>Full archive path (directory/file.extension)</returns>
        public static string ValidateAndGetArchivePath(string directoryPath, FragileOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            ValidateDirectoryExists(directoryPath);
            
            // Create full path using filename and extension
            string fileName = options.FileName + options.Extension;
            string fullPath = Path.Combine(directoryPath, fileName);
            
            // Check if a file with the same name exists
            if (File.Exists(fullPath))
            {
                throw new IOException($"A file with the same name already exists in the target directory: {fullPath}");
            }
            
            return fullPath;
        }

        /// <summary>
        /// Validates the extraction directory path and checks if it's empty if it exists
        /// </summary>
        /// <param name="directoryPath">Directory path</param>
        /// <param name="createIfNotExists">Create directory if it doesn't exist</param>
        /// <exception cref="ArgumentException">If path is empty</exception>
        /// <exception cref="DirectoryNotFoundException">If directory doesn't exist and cannot be created</exception>
        /// <exception cref="InvalidOperationException">If directory is not empty</exception>
        public static void ValidateExtractionDirectory(string directoryPath, bool createIfNotExists = false)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException("Directory path cannot be empty.", nameof(directoryPath));
            }

            if (!Directory.Exists(directoryPath))
            {
                if (createIfNotExists)
                {
                    try
                    {
                        Directory.CreateDirectory(directoryPath);
                    }
                    catch (Exception ex)
                    {
                        throw new DirectoryNotFoundException($"Directory not found and could not be created: {directoryPath}", ex);
                    }
                }
                else
                {
                    throw new DirectoryNotFoundException($"Specified directory not found: {directoryPath}");
                }
            }
            else
            {
                // If directory exists, validate that it's empty
                ValidateDirectoryEmpty(directoryPath);
            }
        }

        /// <summary>
        /// Normalizes a given path according to internal archive format
        /// </summary>
        /// <param name="path">Path to normalize</param>
        /// <returns>Normalized path</returns>
        public static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
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
        /// <param name="sourcePath">Source file or directory path</param>
        /// <param name="entryPath">Target path in archive (uses file/directory name if null)</param>
        /// <returns>Normalized archive entry path</returns>
        public static string CreateEntryPath(string sourcePath, string? entryPath = null)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("Source path cannot be empty.", nameof(sourcePath));
            }

            entryPath ??= Path.GetFileName(sourcePath);
            return NormalizePath(entryPath);
        }

        /// <summary>
        /// Creates a temporary file path for archive location
        /// </summary>
        /// <param name="options">Archive options</param>
        /// <returns>Temporary file path</returns>
        public static string CreateTempFilePath(FragileOptions? options = null)
        {
            options ??= new FragileOptions();
            string tempDir = options.TempDirectory;
            
            // Ensure temp directory exists
            EnsureDirectoryExists(tempDir);
            
            return Path.Combine(tempDir, $"Fragile_{Guid.NewGuid()}{options.Extension}");
        }

        /// <summary>
        /// Creates a temporary directory path for archive location
        /// </summary>
        /// <param name="options">Archive options</param>
        /// <returns>Temporary directory path</returns>
        public static string CreateTempDirectoryPath(FragileOptions? options = null)
        {
            options ??= new FragileOptions();
            string tempDir = options.TempDirectory;
            
            // Ensure temp directory exists
            EnsureDirectoryExists(tempDir);
            
            string path = Path.Combine(tempDir, $"Fragile_{Guid.NewGuid()}");
            Directory.CreateDirectory(path);
            return path;
        }
    }
}