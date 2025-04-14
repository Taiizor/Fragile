using System;

namespace Fragile.Archive
{
    /// <summary>
    /// Defines the structure and constants for the Fragile archive format.
    /// </summary>
    public static class ArchiveFormat
    {
        /// <summary>
        /// The file extension used for Fragile archives.
        /// </summary>
        public const string FileExtension = ".frgl";

        /// <summary>
        /// The magic bytes at the beginning of every Fragile archive file.
        /// </summary>
        public static readonly byte[] MagicBytes = new byte[] { 0x46, 0x52, 0x47, 0x4C }; // FRGL

        /// <summary>
        /// The current version of the Fragile archive format.
        /// </summary>
        public const string FormatVersion = "1.0.0";

        /// <summary>
        /// The minimum supported version of the Fragile archive format.
        /// </summary>
        public const string MinimumSupportedVersion = "1.0.0";

        /// <summary>
        /// Validates if the provided bytes match the Fragile archive magic bytes.
        /// </summary>
        /// <param name="headerBytes">The bytes to check.</param>
        /// <returns>True if the bytes match the magic bytes; otherwise, false.</returns>
        public static bool ValidateMagicBytes(byte[] headerBytes)
        {
            if (headerBytes == null || headerBytes.Length < MagicBytes.Length)
                return false;

            for (int i = 0; i < MagicBytes.Length; i++)
            {
                if (headerBytes[i] != MagicBytes[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if the provided version is compatible with the current format.
        /// </summary>
        /// <param name="version">The version to check.</param>
        /// <returns>True if the version is compatible; otherwise, false.</returns>
        public static bool IsCompatibleVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return false;

            try
            {
                var versionToCheck = new Version(version);
                var minimumVersion = new Version(MinimumSupportedVersion);
                var currentVersion = new Version(FormatVersion);

                return versionToCheck >= minimumVersion && versionToCheck <= currentVersion;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
} 