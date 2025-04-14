using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Fragile.Core
{
    /// <summary>
    /// Represents a collection of split archive parts
    /// </summary>
    public class FragileArchivePartCollection : IReadOnlyCollection<FragileArchivePart>
    {
        private readonly List<FragileArchivePart> _parts = new List<FragileArchivePart>();
        
        /// <summary>
        /// Gets the number of parts in the collection
        /// </summary>
        public int Count => _parts.Count;
        
        /// <summary>
        /// Gets the part at the specified index
        /// </summary>
        public FragileArchivePart this[int index] => _parts[index];
        
        /// <summary>
        /// Adds a part to the collection
        /// </summary>
        /// <param name="part">The part to add</param>
        public void Add(FragileArchivePart part)
        {
            if (part == null)
            {
                throw new ArgumentNullException(nameof(part));
            }
            
            _parts.Add(part);
            
            // Sort by part index to ensure correct order
            _parts.Sort((a, b) => a.PartIndex.CompareTo(b.PartIndex));
        }
        
        /// <summary>
        /// Gets an enumerator for the collection
        /// </summary>
        public IEnumerator<FragileArchivePart> GetEnumerator()
        {
            return _parts.GetEnumerator();
        }
        
        /// <summary>
        /// Gets a non-generic enumerator for the collection
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        
        /// <summary>
        /// Combines all parts into a single file
        /// </summary>
        /// <param name="outputPath">Path to the output file</param>
        /// <param name="progress">Optional progress reporting</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the combine operation</returns>
        public async Task CombinePartsAsync(string outputPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (_parts.Count == 0)
            {
                throw new InvalidOperationException("No parts to combine");
            }
            
            // Validate that all parts are present
            int expectedTotalParts = _parts[0].TotalParts;
            if (_parts.Count != expectedTotalParts)
            {
                throw new InvalidOperationException($"Missing parts. Expected {expectedTotalParts} parts, but found {_parts.Count}");
            }
            
            // Check if parts are in sequence
            for (int i = 0; i < _parts.Count; i++)
            {
                if (_parts[i].PartIndex != i + 1)
                {
                    throw new InvalidOperationException($"Missing part {i + 1}");
                }
            }
            
            // Calculate total size for progress reporting
            long totalSize = _parts.Sum(p => p.Size);
            long processedSize = 0;
            
            // Create output file
            using (var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                // Combine parts
                foreach (var part in _parts)
                {
                    using (var partStream = new FileStream(part.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        byte[] buffer = new byte[81920]; // 80 KB buffer
                        int bytesRead;
                        
                        while ((bytesRead = await partStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                        {
                            await outputStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                            
                            // Update progress
                            processedSize += bytesRead;
                            progress?.Report((double)processedSize / totalSize);
                            
                            // Check for cancellation
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Finds all split archive parts for a given base file name pattern
        /// </summary>
        /// <param name="basePath">The base archive path</param>
        /// <returns>A collection of archive parts, or empty if not found</returns>
        public static FragileArchivePartCollection FindParts(string basePath)
        {
            var result = new FragileArchivePartCollection();
            
            if (string.IsNullOrEmpty(basePath))
            {
                return result;
            }
            
            string directory = Path.GetDirectoryName(basePath) ?? "";
            string fileName = Path.GetFileName(basePath);
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(basePath);
            string extension = Path.GetExtension(basePath);
            
            // Search for part files matching the pattern [filename].partXXX[extension]
            if (Directory.Exists(directory))
            {
                string searchPattern = $"{fileNameWithoutExt}.part*{extension}";
                string[] partFiles = Directory.GetFiles(directory, searchPattern);
                
                foreach (var partFile in partFiles)
                {
                    string partFileName = Path.GetFileName(partFile);
                    
                    // Extract part number from filename
                    // Pattern: [filename].partXXX[extension]
                    string partIndexStr = partFileName.Substring(
                        fileNameWithoutExt.Length + ".part".Length,
                        partFileName.Length - fileNameWithoutExt.Length - ".part".Length - extension.Length
                    );
                    
                    if (int.TryParse(partIndexStr, out int partIndex))
                    {
                        var fileInfo = new FileInfo(partFile);
                        
                        var part = new FragileArchivePart
                        {
                            PartIndex = partIndex,
                            Path = partFile,
                            Size = fileInfo.Length
                            // TotalParts will be set later once we have all parts
                        };
                        
                        result.Add(part);
                    }
                }
                
                // Set TotalParts for all parts
                if (result.Count > 0)
                {
                    int totalParts = result.Count;
                    foreach (var part in result._parts)
                    {
                        part.TotalParts = totalParts;
                    }
                }
            }
            
            return result;
        }
    }
} 