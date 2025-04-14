using Fragile.Core;
using Fragile.Models;
using System;
using System.IO;

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
            if (string.IsNullOrEmpty(sourcePath))
            {
                throw new ArgumentException("Source path cannot be empty", nameof(sourcePath));
            }

            if (archivePath == null)
            {
                // Source file/directory name + .frgl extension
                archivePath = Path.ChangeExtension(sourcePath, DefaultExtension);
            }

            using FragileArchive archive = new(archivePath, FragileArchiveMode.Create);
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
        /// Extracts a Fragile archive to the specified target directory
        /// </summary>
        /// <param name="archivePath">Path to the archive file</param>
        /// <param name="destinationPath">Target directory (if null, archive name is used)</param>
        public static void ExtractArchive(string archivePath, string? destinationPath = null)
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

            using FragileArchive archive = new(archivePath, FragileArchiveMode.Read);
            archive.ExtractAll(destinationPath);
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
                return System.Text.Encoding.ASCII.GetString(signature) == "FRGL";
            }
            catch
            {
                return false;
            }
        }
    }
}