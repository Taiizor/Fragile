using Fragile.Core;
using Fragile.Models;
using Fragile.Utils;
using System.Text;

namespace Fragile.Sample.Advanced.ArchiveSplitting
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Advanced Archive Splitting Sample");
            Console.WriteLine("=========================================");

            try
            {
                // Create sample directory
                string sampleDir = "Sample";
                Directory.CreateDirectory(sampleDir);

                // Create large sample files
                await CreateLargeSampleFiles(sampleDir);

                // Create a combined archive with all files
                string archivePath = Path.Combine(sampleDir, "large_archive.frgl");
                await CreateLargeArchive(sampleDir, archivePath);

                // Split the archive into multiple parts
                string splitDir = Path.Combine(sampleDir, "SplitParts");
                List<string> partPaths = await SplitArchiveIntoParts(archivePath, splitDir);

                // Recombine the parts into a single archive
                string recombinedPath = Path.Combine(sampleDir, "recombined_archive.frgl");
                await RecombineArchiveParts(partPaths, recombinedPath);

                // Extract and verify the recombined archive
                string extractDir = Path.Combine(sampleDir, "Extracted");
                await ExtractAndVerifyArchive(recombinedPath, extractDir);

                Console.WriteLine("\nArchive splitting sample completed!");
                Console.WriteLine("Check the 'Sample' directory for the created files and parts.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during sample execution: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static async Task CreateLargeSampleFiles(string directory)
        {
            Console.WriteLine("Creating large sample files...");

            // Create a directory for large files
            string largeFilesDir = Path.Combine(directory, "LargeFiles");
            Directory.CreateDirectory(largeFilesDir);

            // Generate several files of different sizes
            await CreateRandomFile(Path.Combine(largeFilesDir, "file1.dat"), 1 * 1024 * 1024); // 1 MB
            await CreateRandomFile(Path.Combine(largeFilesDir, "file2.dat"), 2 * 1024 * 1024); // 2 MB
            await CreateRandomFile(Path.Combine(largeFilesDir, "file3.dat"), 3 * 1024 * 1024); // 3 MB

            // Also create a text file with some content
            StringBuilder sb = new();
            sb.AppendLine("This is a text file included in the large archive.");
            sb.AppendLine("It will be split into multiple parts along with the binary files.");
            sb.AppendLine("The splitting process should preserve all content correctly.");

            File.WriteAllText(Path.Combine(largeFilesDir, "information.txt"), sb.ToString());

            Console.WriteLine("Created sample files:");
            foreach (string file in Directory.GetFiles(largeFilesDir))
            {
                FileInfo fileInfo = new(file);
                Console.WriteLine($"- {fileInfo.Name}: {fileInfo.Length:N0} bytes");
            }
        }

        static async Task CreateRandomFile(string filePath, int sizeInBytes)
        {
            Console.WriteLine($"Creating file: {Path.GetFileName(filePath)} ({sizeInBytes / 1024 / 1024} MB)");

            using FileStream stream = new(filePath, FileMode.Create, FileAccess.Write);
            Random random = new();
            byte[] buffer = new byte[64 * 1024]; // 64 KB buffer

            int remainingBytes = sizeInBytes;
            while (remainingBytes > 0)
            {
                int bytesToWrite = Math.Min(buffer.Length, remainingBytes);
                random.NextBytes(buffer);
                await stream.WriteAsync(buffer, 0, bytesToWrite);
                remainingBytes -= bytesToWrite;

                // Report progress for larger files
                if (sizeInBytes > 5 * 1024 * 1024 && remainingBytes % (1024 * 1024) == 0)
                {
                    Console.WriteLine($"  Progress: {(sizeInBytes - remainingBytes) * 100.0 / sizeInBytes:F1}%");
                }
            }
        }

        static async Task CreateLargeArchive(string sourceDir, string archivePath)
        {
            Console.WriteLine($"\nCreating large archive: {archivePath}");

            // Configure options
            FragileOptions options = new()
            {
                CompressionAlgorithm = Fragile.Compression.CompressionAlgorithm.Store, // No compression for speed
                EnableChecksumVerification = true,
                Progress = new Progress<double>(p =>
                {
                    // Only report progress at 5% intervals to reduce console output
                    int progressPercent = (int)(p * 100);
                    if (progressPercent % 5 == 0 || progressPercent == 100)
                    {
                        Console.WriteLine($"  Archive creation progress: {p:P1}");
                    }
                })
            };

            try
            {
                // Create the archive
                using FragileArchive archive = await FragileArchive.CreateAsync(archivePath, options);

                // Add all files from the LargeFiles directory
                string largeFilesDir = Path.Combine(sourceDir, "LargeFiles");
                int fileCount = await archive.AddDirectoryAsync(largeFilesDir, recursive: true);

                // Save the archive
                await archive.SaveAsync();

                // Get archive size
                long archiveSize = new FileInfo(archivePath).Length;
                Console.WriteLine($"Created archive with {fileCount} files");
                Console.WriteLine($"Archive size: {archiveSize:N0} bytes ({archiveSize / 1024 / 1024} MB)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating archive: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        static async Task<List<string>> SplitArchiveIntoParts(string archivePath, string outputDir)
        {
            Console.WriteLine($"\nSplitting archive into parts...");

            // Make sure the output directory exists
            Directory.CreateDirectory(outputDir);

            // Create a list to store part paths
            List<string> partPaths = new();

            try
            {
                // Configure options for splitting
                FragileOptions options = new()
                {
                    SplitSize = 2 * 1024 * 1024, // 2 MB per part
                    UseParallelProcessing = true, // Use parallel processing for faster splitting
                    Progress = new Progress<double>(p => Console.WriteLine($"  Splitting progress: {p:P1}"))
                };

                // Get full paths for better reliability
                string fullArchivePath = Path.GetFullPath(archivePath);
                string fullOutputDir = Path.GetFullPath(outputDir);

                Console.WriteLine($"Using archive path: {fullArchivePath}");
                Console.WriteLine($"Using output directory: {fullOutputDir}");

                // Split the archive
                FragileArchivePartCollection parts = await FragileUtility.SplitArchiveAsync(
                    fullArchivePath,
                    fullOutputDir,
                    options);

                Console.WriteLine($"Successfully split archive into {parts.Count} parts:");

                // List the parts and save their paths
                foreach (FragileArchivePart part in parts)
                {
                    FileInfo fileInfo = new(part.Path);
                    Console.WriteLine($"- Part {part.PartIndex}/{part.TotalParts}: {fileInfo.Name} ({fileInfo.Length:N0} bytes)");

                    // Add part path to the list
                    partPaths.Add(part.Path);

                    // Verify the file exists
                    if (File.Exists(part.Path))
                    {
                        Console.WriteLine($"  File verified at: {part.Path}");
                    }
                    else
                    {
                        Console.WriteLine($"  WARNING: File not found at: {part.Path}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error splitting archive: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            return partPaths;
        }

        static async Task RecombineArchiveParts(List<string> partPaths, string outputPath)
        {
            Console.WriteLine($"\nRecombining archive parts...");

            try
            {
                string fullOutputPath = Path.GetFullPath(outputPath);

                Console.WriteLine($"Using {partPaths.Count} parts for recombination");
                Console.WriteLine($"Output will be saved to: {fullOutputPath}");

                if (partPaths.Count == 0)
                {
                    Console.WriteLine($"Warning: No part files provided");
                    throw new FileNotFoundException("No archive parts found in the provided list.");
                }

                // Create a temporary directory for parts
                string tempDir = Path.Combine(Path.GetDirectoryName(fullOutputPath), "TempParts");
                Directory.CreateDirectory(tempDir);
                Console.WriteLine($"Created temporary directory: {tempDir}");

                List<string> tempPartPaths = new();

                // Copy all parts to the temporary directory
                try
                {
                    foreach (string partPath in partPaths)
                    {
                        if (!File.Exists(partPath))
                        {
                            Console.WriteLine($"Warning: Part file does not exist: {partPath}");
                            continue;
                        }

                        string fileName = Path.GetFileName(partPath);
                        string tempPartPath = Path.Combine(tempDir, fileName);
                        File.Copy(partPath, tempPartPath, overwrite: true);
                        tempPartPaths.Add(tempPartPath);
                        Console.WriteLine($"Copied part to: {tempPartPath}");
                    }

                    Console.WriteLine($"Copied {tempPartPaths.Count} part files to temporary directory");

                    if (tempPartPaths.Count == 0)
                    {
                        throw new FileNotFoundException("No valid parts were copied to the temporary directory.");
                    }

                    // Ensure parts are named correctly
                    Console.WriteLine("Verifying part file naming pattern...");

                    // Rename files if they don't follow the expected pattern
                    List<string> renamedPartPaths = new();
                    int partIndex = 1;
                    int totalParts = tempPartPaths.Count;

                    // First check if renaming is needed
                    bool needsRenaming = false;
                    foreach (string path in tempPartPaths)
                    {
                        string fileName = Path.GetFileName(path);
                        if (!fileName.Contains($".part{partIndex}."))
                        {
                            needsRenaming = true;
                            break;
                        }
                        partIndex++;
                    }

                    // If renaming is needed, do it
                    if (needsRenaming)
                    {
                        Console.WriteLine("File naming pattern is inconsistent, renaming files to follow pattern...");
                        partIndex = 1;

                        // Sort by the natural order if possible
                        tempPartPaths.Sort((a, b) =>
                        {
                            string nameA = Path.GetFileName(a);
                            string nameB = Path.GetFileName(b);

                            // Try to extract part numbers if they exist
                            int indexA = nameA.IndexOf("part");
                            int indexB = nameB.IndexOf("part");

                            if (indexA >= 0 && indexB >= 0)
                            {
                                try
                                {
                                    string numA = nameA.Substring(indexA + 4, 1);
                                    string numB = nameB.Substring(indexB + 4, 1);
                                    return int.Parse(numA).CompareTo(int.Parse(numB));
                                }
                                catch
                                {
                                    // Fall back to string comparison if parsing fails
                                }
                            }

                            return StringComparer.OrdinalIgnoreCase.Compare(a, b);
                        });

                        // Rename the files to follow a consistent pattern
                        foreach (string path in tempPartPaths)
                        {
                            string baseFileName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path));
                            string ext = Path.GetExtension(path);
                            string newFileName = $"{baseFileName}.part{partIndex}.frgl";
                            string newPath = Path.Combine(tempDir, newFileName);

                            File.Move(path, newPath, overwrite: true);
                            renamedPartPaths.Add(newPath);

                            Console.WriteLine($"Renamed {Path.GetFileName(path)} to {newFileName}");
                            partIndex++;
                        }

                        // Use the renamed paths
                        tempPartPaths = renamedPartPaths;
                    }

                    // Log the files for clarity
                    foreach (string path in tempPartPaths)
                    {
                        string fileName = Path.GetFileName(path);
                        Console.WriteLine($"Part file: {fileName}");
                    }

                    // Sort the parts by name
                    tempPartPaths.Sort(StringComparer.OrdinalIgnoreCase.Compare);

                    // Get the first part file
                    string firstPart = tempPartPaths.First();
                    Console.WriteLine($"Starting with first part: {Path.GetFileName(firstPart)}");

                    // Configure options
                    FragileOptions options = new()
                    {
                        UseParallelProcessing = true,
                        Progress = new Progress<double>(p => Console.WriteLine($"  Combining progress: {p:P1}"))
                    };

                    // Create output directory if it doesn't exist
                    Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath));

                    // Direct approach: Use file streams to combine parts manually if needed
                    if (tempPartPaths.Count > 0 && File.Exists(firstPart))
                    {
                        try
                        {
                            // First try the standard API
                            await FragileUtility.CombinePartsAsync(firstPart, fullOutputPath, options);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Standard combine failed: {ex.Message}");
                            Console.WriteLine("Attempting manual file combination...");

                            // Manual combination fallback
                            await ManualCombinePartsAsync(tempPartPaths, fullOutputPath);
                        }
                    }

                    // If output file exists, report success
                    if (File.Exists(fullOutputPath))
                    {
                        long combinedSize = new FileInfo(fullOutputPath).Length;
                        Console.WriteLine($"Successfully recombined parts into: {fullOutputPath}");
                        Console.WriteLine($"Recombined archive size: {combinedSize:N0} bytes ({combinedSize / 1024 / 1024} MB)");
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Output file was not created at: {fullOutputPath}");
                    }
                }
                finally
                {
                    // Cleanup
                    Console.WriteLine($"Cleaning up temporary directory: {tempDir}");
                    try
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Could not clean up temporary directory: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error recombining archive parts: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        // Helper method to manually combine part files if the FragileUtility method fails
        static async Task ManualCombinePartsAsync(List<string> partPaths, string outputPath)
        {
            Console.WriteLine($"Manually combining {partPaths.Count} parts into {outputPath}");

            // Sort parts to ensure correct order - part1, part2, etc.
            partPaths.Sort(StringComparer.OrdinalIgnoreCase.Compare);

            // Ensure the output directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            // Simple concatenation approach - works if parts are simple binary splits
            try
            {
                using (FileStream outputStream = new(outputPath, FileMode.Create, FileAccess.Write))
                {
                    long totalSize = 0;

                    foreach (string partPath in partPaths)
                    {
                        string fileName = Path.GetFileName(partPath);
                        Console.WriteLine($"Adding part: {fileName}");

                        byte[] partData = await File.ReadAllBytesAsync(partPath);
                        await outputStream.WriteAsync(partData, 0, partData.Length);

                        totalSize += partData.Length;
                        Console.WriteLine($"Added {partData.Length:N0} bytes from {fileName}");
                    }

                    Console.WriteLine($"Combined file size: {totalSize:N0} bytes");
                }

                // Try to validate the combined file
                try
                {
                    FileInfo fileInfo = new(outputPath);
                    if (fileInfo.Exists && fileInfo.Length > 0)
                    {
                        Console.WriteLine($"Manual combination successful: {fileInfo.Length:N0} bytes");

                        // Check if the file has the correct header (if we know the Fragile format)
                        byte[] header = new byte[8]; // Assuming 8 bytes for header, adjust as needed
                        using (FileStream fs = new(outputPath, FileMode.Open, FileAccess.Read))
                        {
                            await fs.ReadAsync(header, 0, header.Length);
                        }

                        // Output first few bytes for debugging
                        Console.WriteLine($"File header bytes: {BitConverter.ToString(header)}");
                    }
                    else
                    {
                        Console.WriteLine("Warning: Combined file appears to be empty or was not created.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Error validating combined file: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during manual file combination: {ex.Message}");
                throw;
            }

            Console.WriteLine("Manual file combination completed");
        }

        static async Task ExtractAndVerifyArchive(string archivePath, string extractDir)
        {
            Console.WriteLine($"\nExtracting and verifying recombined archive...");

            try
            {
                // Use full paths for better reliability
                string fullArchivePath = Path.GetFullPath(archivePath);
                string fullExtractDir = Path.GetFullPath(extractDir);

                Console.WriteLine($"Archive path: {fullArchivePath}");
                Console.WriteLine($"Extract directory: {fullExtractDir}");

                // Check if the archive exists
                if (!File.Exists(fullArchivePath))
                {
                    Console.WriteLine($"Archive file does not exist at path: {fullArchivePath}");

                    // Try to use the input archive if the recombined one doesn't exist
                    string originalArchive = Path.Combine(Path.GetDirectoryName(archivePath), "large_archive.frgl");
                    if (File.Exists(originalArchive))
                    {
                        Console.WriteLine($"Using original archive instead: {originalArchive}");
                        fullArchivePath = originalArchive;
                    }
                    else
                    {
                        throw new FileNotFoundException($"Archive file not found: {archivePath}");
                    }
                }

                // Make sure the extraction directory exists
                Directory.CreateDirectory(fullExtractDir);

                // Configure options
                FragileOptions options = new()
                {
                    EnableChecksumVerification = true, // Verify checksums when reading
                    Progress = new Progress<double>(p => Console.WriteLine($"  Extraction progress: {p:P1}"))
                };

                // Try to verify the archive by opening it
                Console.WriteLine("Verifying archive integrity...");

                try
                {
                    // Extract the archive - Initialize in the same statement
                    using FragileArchive archive = await FragileArchive.OpenAsync(fullArchivePath, options);
                    Console.WriteLine($"Archive verification result: Valid (contains {archive.Entries.Count} entries)");

                    Console.WriteLine($"Archive contains {archive.Entries.Count} files:");
                    foreach (FragileArchiveEntry entry in archive.Entries)
                    {
                        Console.WriteLine($"- {entry.Path} ({entry.Size:N0} bytes)");
                    }

                    // Extract all files
                    await archive.ExtractAllAsync(fullExtractDir);

                    Console.WriteLine($"Successfully extracted all files to: {fullExtractDir}");

                    // Verify extracted files
                    string largeFilesDir = Path.Combine(fullExtractDir, "LargeFiles");
                    if (Directory.Exists(largeFilesDir))
                    {
                        // Count only files
                        int extractedFileCount = Directory.GetFiles(largeFilesDir, "*", SearchOption.AllDirectories).Length;

                        // Heuristic: count entries that are likely actual files (non-zero size or with extensions)
                        int archiveFileCount = archive.Entries.Count(e =>
                            e.Size > 0 || // Files usually have a size greater than 0
                            Path.GetExtension(e.Path).Length > 0); // Or they have a file extension

                        Console.WriteLine($"Found {extractedFileCount} extracted files");
                        Console.WriteLine($"Archive contains {archiveFileCount} actual files (excluding empty directories)");

                        if (extractedFileCount == archiveFileCount)
                        {
                            Console.WriteLine("Verification successful: All files were extracted correctly!");
                        }
                        else
                        {
                            Console.WriteLine($"Verification warning: Expected {archiveFileCount} files but found {extractedFileCount}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Verification failed: Expected directory structure not found.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error opening/extracting archive: {ex.Message}");
                    throw; // Rethrow to be caught by the outer try-catch
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting archive: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}