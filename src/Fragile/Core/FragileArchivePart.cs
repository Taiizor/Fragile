namespace Fragile.Core
{
    /// <summary>
    /// Represents a split part of a Fragile archive
    /// </summary>
    public class FragileArchivePart
    {
        /// <summary>
        /// The size of the part in bytes
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// The offset of this part in the complete archive
        /// </summary>
        public long Offset { get; set; }

        /// <summary>
        /// CRC32 checksum of the part
        /// </summary>
        public uint Checksum { get; set; }

        /// <summary>
        /// The part index (1-based)
        /// </summary>
        public int PartIndex { get; set; }

        /// <summary>
        /// The total number of parts
        /// </summary>
        public int TotalParts { get; set; }

        /// <summary>
        /// The path to the part file
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Creates a FragileArchivePart from a file name
        /// </summary>
        /// <param name="fileName">Path to the part file</param>
        /// <param name="splitName">The name of the split parts</param>
        /// <returns>A new FragileArchivePart instance</returns>
        public static FragileArchivePart FromFileName(string fileName, string splitName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException("File name cannot be null or empty", nameof(fileName));
            }

            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException($"Part file not found: {fileName}");
            }

            // Get file info
            FileInfo fileInfo = new(fileName);

            // Extract the part index from the file name
            // Expected format: baseFileName.partXXX.extension
            string fileNameWithoutPath = System.IO.Path.GetFileName(fileName);
            int partStartIndex = fileNameWithoutPath.IndexOf(splitName, StringComparison.OrdinalIgnoreCase);

            if (partStartIndex < 0)
            {
                throw new FormatException($"Invalid part file name format: {fileName}");
            }

            // Extract part number
            string extension = System.IO.Path.GetExtension(fileName);
            int extensionIndex = fileNameWithoutPath.LastIndexOf(extension, StringComparison.OrdinalIgnoreCase);

            // If no extension or ".part" appears after the extension, it's invalid
            if (extensionIndex < 0 || extensionIndex < partStartIndex)
            {
                throw new FormatException($"Invalid part file name format: {fileName}");
            }

#if NET48_OR_GREATER || NETSTANDARD2_0
            // Extract the part number
            string partNumberStr = fileNameWithoutPath.Substring(partStartIndex + splitName.Length, extensionIndex - (partStartIndex + splitName.Length));
#else
            // Extract the part number
            string partNumberStr = fileNameWithoutPath[(partStartIndex + splitName.Length)..extensionIndex];
#endif

            if (!int.TryParse(partNumberStr, out int partIndex))
            {
                throw new FormatException($"Invalid part number in file name: {fileName}");
            }

            // Create the part
            FragileArchivePart part = new()
            {
                PartIndex = partIndex,
                Path = fileName,
                Size = fileInfo.Length,
                // TotalParts and Offset will be set later when all parts are processed
                TotalParts = 1 // Default value until updated
            };

            return part;
        }

        /// <summary>
        /// Gets the standard file name for a split archive part
        /// </summary>
        /// <param name="basePath">The base archive path</param>
        /// <param name="partIndex">The part index (1-based)</param>
        /// <param name="totalParts">The total number of parts</param>
        /// <param name="splitName">The name of the split parts</param>
        /// <returns>The formatted part file name</returns>
        public static string GetPartFileName(string basePath, int partIndex, int totalParts, string splitName)
        {
            if (string.IsNullOrEmpty(basePath))
            {
                throw new ArgumentException("Base path cannot be null or empty", nameof(basePath));
            }

            if (partIndex < 1 || partIndex > totalParts)
            {
                throw new ArgumentOutOfRangeException(nameof(partIndex), "Part index must be between 1 and the total number of parts");
            }

            if (totalParts < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(totalParts), "Total parts must be at least 1");
            }

            // Format: baseFileName.partXXX.frgl
            // Where XXX is the part number padded with zeros
            // The number of digits depends on the total number of parts
            int digits = totalParts.ToString().Length;
            string partSuffix = $"{splitName}{partIndex.ToString().PadLeft(digits, '0')}";

            // Handle file with extension
            string extension = System.IO.Path.GetExtension(basePath);
            string nameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(basePath) ?? "Fragile"; System.IO.Path.GetFileNameWithoutExtension(basePath);

            return nameWithoutExtension + partSuffix + extension;
        }
    }
}