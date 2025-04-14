using Fragile.Core;
using Fragile.Models;
using System.Text;

namespace Fragile.Sample.Advanced.ErrorCorrection
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Fragile Advanced Error Correction Sample");
            Console.WriteLine("=======================================");

            // Create sample directory
            string sampleDir = "Sample";
            Directory.CreateDirectory(sampleDir);

            // Create a test file with some content
            string testFilePath = Path.Combine(sampleDir, "important_data.txt");
            CreateImportantDataFile(testFilePath);

            // Create an archive with error correction
            string archivePath = Path.Combine(sampleDir, "protected_archive.frgl");
            await CreateArchiveWithErrorCorrection(testFilePath, archivePath);

            // Simulate corruption in the archive file
            await CorruptArchiveFile(archivePath);

            // Try to repair and extract the corrupted archive
            string extractDir = Path.Combine(sampleDir, "Extracted");
            await RepairAndExtractArchive(archivePath, extractDir);

            Console.WriteLine("\nError correction sample completed!");
            Console.WriteLine("Check the 'Sample' directory for the created files and extraction results.");
        }

        static void CreateImportantDataFile(string filePath)
        {
            Console.WriteLine($"Creating important data file: {filePath}");

            StringBuilder sb = new();
            sb.AppendLine("CRITICAL FINANCIAL DATA - DO NOT LOSE");
            sb.AppendLine("===================================");
            sb.AppendLine();

            // Create some "important" data
            Random random = new(42); // Fixed seed for reproducibility

            sb.AppendLine("Transaction Records:");
            for (int i = 1; i <= 100; i++)
            {
                decimal amount = Math.Round((decimal)(random.NextDouble() * 10000), 2);
                DateTime date = DateTime.Now.AddDays(-random.Next(1, 30));
                sb.AppendLine($"Transaction #{i:000} | Date: {date:yyyy-MM-dd} | Amount: ${amount:N2} | Reference: REF-{random.Next(100000, 999999)}");
            }

            File.WriteAllText(filePath, sb.ToString());
            Console.WriteLine($"Created file with {new FileInfo(filePath).Length:N0} bytes of important data");
        }

        static async Task CreateArchiveWithErrorCorrection(string filePath, string archivePath)
        {
            Console.WriteLine("\nCreating archive with error correction enabled...");

            // Configure options with error correction enabled
            FragileOptions options = new()
            {
                EnableErrorCorrection = true,
                ErrorCorrectionLevel = 10, // 10% of the archive size for error correction
                EnableChecksumVerification = true // Also enable checksumming for additional protection
            };

            Console.WriteLine($"Error correction level: {options.ErrorCorrectionLevel}%");

            try
            {
                // Create the archive with error correction
                using FragileArchive archive = await FragileArchive.CreateAsync(archivePath, options);
                await archive.AddFileAsync(filePath);
                await archive.SaveAsync();

                long archiveSize = new FileInfo(archivePath).Length;
                Console.WriteLine($"Archive created successfully: {archivePath}");
                Console.WriteLine($"Archive size: {archiveSize:N0} bytes");

                // Calculate roughly how much space is used for error correction
                long estimatedDataSize = new FileInfo(filePath).Length;
                long overhead = archiveSize - estimatedDataSize;
                Console.WriteLine($"Estimated overhead (includes error correction): ~{overhead:N0} bytes ({(double)overhead / archiveSize:P1} of total)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating archive: {ex.Message}");
            }
        }

        static async Task CorruptArchiveFile(string archivePath)
        {
            Console.WriteLine("\nSimulating archive corruption...");

            // Read the entire file
            byte[] fileBytes = await File.ReadAllBytesAsync(archivePath);

            // Don't corrupt the header (first 32 bytes), as that would make the file unrecognizable
            // Corrupt some bytes in the middle of the file
            Random random = new(123); // Fixed seed for reproducibility

            // Define corruption parameters
            int startPos = 512; // Skip the header area
            int corruptionCount = fileBytes.Length > 2048 ? 40 : 10; // Corrupt more bytes in larger files

            if (startPos >= fileBytes.Length)
            {
                startPos = fileBytes.Length / 4; // Adjust for very small files
            }

            // Perform corruption (change random bytes)
            for (int i = 0; i < corruptionCount; i++)
            {
                int position = random.Next(startPos, fileBytes.Length - 1);
                byte originalValue = fileBytes[position];
                byte newValue;

                do
                {
                    newValue = (byte)random.Next(0, 256);
                } while (newValue == originalValue); // Make sure we're actually changing the value

                fileBytes[position] = newValue;
                Console.WriteLine($"Corrupted byte at position {position}: {originalValue} -> {newValue}");
            }

            // Write the corrupted data back to the file
            await File.WriteAllBytesAsync(archivePath, fileBytes);
            Console.WriteLine($"Corrupted {corruptionCount} bytes in the archive file");
        }

        static async Task RepairAndExtractArchive(string archivePath, string extractDir)
        {
            Console.WriteLine("\nAttempting to repair and extract the corrupted archive...");

            try
            {
                // Make sure the extraction directory exists
                Directory.CreateDirectory(extractDir);

                // Open the archive with error correction enabled
                FragileOptions options = new()
                {
                    EnableErrorCorrection = true, // Ensure error correction is enabled for repair
                    ErrorCorrectionLevel = 10     // Same level used when creating the archive
                };

                // Try to extract the archive despite corruption
                using FragileArchive archive = await FragileArchive.OpenAsync(archivePath, options);

                Console.WriteLine($"Successfully opened the archive despite corruption.");
                Console.WriteLine($"Found {archive.Entries.Count} files in the archive.");

                // Extract all files
                await archive.ExtractAllAsync(extractDir);

                Console.WriteLine($"Successfully extracted files to: {extractDir}");

                // Check if the extracted file matches the original
                if (File.Exists(Path.Combine(extractDir, "important_data.txt")))
                {
                    Console.WriteLine("Extracted file exists - checking content integrity...");

                    // In a real application, you would compare checksums or do a binary comparison
                    // For this sample, we just verify the file exists and has reasonable size
                    long extractedFileSize = new FileInfo(Path.Combine(extractDir, "important_data.txt")).Length;
                    Console.WriteLine($"Extracted file size: {extractedFileSize:N0} bytes");

                    if (extractedFileSize > 0)
                    {
                        Console.WriteLine("File appears to be recovered successfully!");
                    }
                    else
                    {
                        Console.WriteLine("Warning: Extracted file exists but is empty.");
                    }
                }
                else
                {
                    Console.WriteLine("Error: Failed to extract the original file.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during repair and extraction: {ex.Message}");
                Console.WriteLine($"Error correction may not have been sufficient for the level of corruption.");
            }
        }
    }
}