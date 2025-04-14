using System;

namespace Fragile.Core
{
    /// <summary>
    /// Represents a split part of a Fragile archive
    /// </summary>
    public class FragileArchivePart
    {
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
        /// Gets the standard file name for a split archive part
        /// </summary>
        /// <param name="basePath">The base archive path</param>
        /// <param name="partIndex">The part index (1-based)</param>
        /// <param name="totalParts">The total number of parts</param>
        /// <returns>The formatted part file name</returns>
        public static string GetPartFileName(string basePath, int partIndex, int totalParts)
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
            string partSuffix = $".part{partIndex.ToString().PadLeft(digits, '0')}";
            
            // Handle file with extension
            string extension = System.IO.Path.GetExtension(basePath);
            string nameWithoutExtension = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(basePath) ?? "",
                System.IO.Path.GetFileNameWithoutExtension(basePath)
            );
            
            return nameWithoutExtension + partSuffix + extension;
        }
    }
} 