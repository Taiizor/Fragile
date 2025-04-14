using Fragile.Core;
using Fragile.Models;
using Fragile.Utils;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Fragile.Sample.Advanced.ArchiveSplitting
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Fragile Advanced Archive Splitting Sample");
            Console.WriteLine("=========================================");

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
            await SplitArchiveIntoParts(archivePath, splitDir);

            // Recombine the parts into a single archive
            string recombinedPath = Path.Combine(sampleDir, "recombined_archive.frgl");
            await RecombineArchiveParts(splitDir, recombinedPath);

            // Extract and verify the recombined archive
            string extractDir = Path.Combine(sampleDir, "Extracted");
            await ExtractAndVerifyArchive(recombinedPath, extractDir);

            Console.WriteLine("\nArchive splitting sample completed!");
            Console.WriteLine("Check the 'Sample' directory for the created files and parts.");
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
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("This is a text file included in the large archive.");
            sb.AppendLine("It will be split into multiple parts along with the binary files.");
            sb.AppendLine("The splitting process should preserve all content correctly.");
            
            File.WriteAllText(Path.Combine(largeFilesDir, "information.txt"), sb.ToString());
            
            Console.WriteLine("Created sample files:");
            foreach (string file in Directory.GetFiles(largeFilesDir))
            {
                FileInfo fileInfo = new FileInfo(file);
                Console.WriteLine($"- {fileInfo.Name}: {fileInfo.Length:N0} bytes");
            }
        }
        
        static async Task CreateRandomFile(string filePath, int sizeInBytes)
        {
            Console.WriteLine($"Creating file: {Path.GetFileName(filePath)} ({sizeInBytes / 1024 / 1024} MB)");
            
            using FileStream stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            Random random = new Random();
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
                    Console.WriteLine($"  Progress: {((sizeInBytes - remainingBytes) * 100.0 / sizeInBytes):F1}%");
                }
            }
        }

        static async Task CreateLargeArchive(string sourceDir, string archivePath)
        {
            Console.WriteLine($"\nCreating large archive: {archivePath}");
            
            // Configure options
            FragileOptions options = new FragileOptions
            {
                CompressionAlgorithm = Fragile.Compression.CompressionAlgorithm.Store, // No compression for speed
                EnableChecksumVerification = true,
                Progress = new Progress<double>(p => Console.WriteLine($"  Archive creation progress: {p:P1}"))
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
            }
        }

        static async Task SplitArchiveIntoParts(string archivePath, string outputDir)
        {
            Console.WriteLine($"\nSplitting archive into parts...");
            
            // Make sure the output directory exists
            Directory.CreateDirectory(outputDir);
            
            try
            {
                // Configure options for splitting
                FragileOptions options = new FragileOptions
                {
                    SplitSize = 2 * 1024 * 1024, // 2 MB per part
                    UseParallelProcessing = true, // Use parallel processing for faster splitting
                    Progress = new Progress<double>(p => Console.WriteLine($"  Splitting progress: {p:P1}"))
                };
                
                // Split the archive
                FragileArchivePartCollection parts = await FragileUtility.SplitArchiveAsync(
                    archivePath, 
                    outputDir, 
                    options);
                
                Console.WriteLine($"Successfully split archive into {parts.Count} parts:");
                
                // List the parts
                foreach (var part in parts)
                {
                    FileInfo fileInfo = new FileInfo(part.Path);
                    Console.WriteLine($"- Part {part.PartIndex}/{part.TotalParts}: {fileInfo.Name} ({fileInfo.Length:N0} bytes)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error splitting archive: {ex.Message}");
            }
        }

        static async Task RecombineArchiveParts(string partsDir, string outputPath)
        {
            Console.WriteLine($"\nRecombining archive parts...");
            
            try
            {
                // Find all part files
                string[] partFiles = Directory.GetFiles(partsDir, "*.part*");
                if (partFiles.Length == 0)
                {
                    throw new FileNotFoundException("No archive parts found in the specified directory.");
                }
                
                // Get the first part file
                string firstPart = partFiles.OrderBy(f => f).First();
                Console.WriteLine($"Starting with first part: {Path.GetFileName(firstPart)}");
                
                // Configure options
                FragileOptions options = new FragileOptions
                {
                    UseParallelProcessing = true,
                    Progress = new Progress<double>(p => Console.WriteLine($"  Combining progress: {p:P1}"))
                };
                
                // Combine the parts
                await FragileUtility.CombinePartsAsync(firstPart, outputPath, options);
                
                // Report success
                long combinedSize = new FileInfo(outputPath).Length;
                Console.WriteLine($"Successfully recombined parts into: {outputPath}");
                Console.WriteLine($"Recombined archive size: {combinedSize:N0} bytes ({combinedSize / 1024 / 1024} MB)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error recombining archive parts: {ex.Message}");
            }
        }

        static async Task ExtractAndVerifyArchive(string archivePath, string extractDir)
        {
            Console.WriteLine($"\nExtracting and verifying recombined archive...");
            
            try
            {
                // Make sure the extraction directory exists
                Directory.CreateDirectory(extractDir);
                
                // Configure options
                FragileOptions options = new FragileOptions
                {
                    Progress = new Progress<double>(p => Console.WriteLine($"  Extraction progress: {p:P1}"))
                };
                
                // Extract the archive
                using FragileArchive archive = await FragileArchive.OpenAsync(archivePath, options);
                
                Console.WriteLine($"Archive contains {archive.Entries.Count} files:");
                foreach (var entry in archive.Entries)
                {
                    Console.WriteLine($"- {entry.Path} ({entry.Size:N0} bytes)");
                }
                
                // Extract all files
                await archive.ExtractAllAsync(extractDir);
                
                Console.WriteLine($"Successfully extracted all files to: {extractDir}");
                
                // Verify extracted files
                string largeFilesDir = Path.Combine(extractDir, "LargeFiles");
                if (Directory.Exists(largeFilesDir))
                {
                    int extractedFileCount = Directory.GetFiles(largeFilesDir, "*", SearchOption.AllDirectories).Length;
                    Console.WriteLine($"Found {extractedFileCount} extracted files");
                    
                    if (extractedFileCount == archive.Entries.Count)
                    {
                        Console.WriteLine("Verification successful: All files were extracted correctly!");
                    }
                    else
                    {
                        Console.WriteLine($"Verification warning: Expected {archive.Entries.Count} files but found {extractedFileCount}");
                    }
                }
                else
                {
                    Console.WriteLine("Verification failed: Expected directory structure not found.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting archive: {ex.Message}");
            }
        }
    }
} 