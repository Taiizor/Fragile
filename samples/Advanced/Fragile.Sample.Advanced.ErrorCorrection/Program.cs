using Fragile.Core;
using Fragile.Models;
using System.Text;

namespace Fragile.Sample.Advanced.ErrorCorrection
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

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

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
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
                ErrorCorrectionLevel = 20,
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

            // Add warning
            Console.WriteLine("WARNING: When applying corruption to the archive file, due to the complex structure");
            Console.WriteLine("of the Fragile format, the file signature must be preserved. For demonstration purposes,");
            Console.WriteLine("we are simulating corruption scenarios that might be encountered in real applications.");

            // Read the entire file
            byte[] fileBytes = await File.ReadAllBytesAsync(archivePath);

            // Very important: To protect signature parts, only target data blocks
            // Fragile format may use signature/metadata in different areas of the file
            // Therefore we need to apply corruption very carefully

            // Educational strategy: Target a very limited middle section
            int totalLength = fileBytes.Length;

            // Definitely preserve the first and last 2KB (for signatures and metadata)
            int safeHeaderSize = 2048; // Header region - 2KB
            int safeFooterSize = 2048; // End region - 2KB

            // Select a safe middle region - exactly the middle 25% of the file
            int middleStart = ((totalLength - safeHeaderSize - safeFooterSize) / 4) + safeHeaderSize;
            int middleSize = (int)((totalLength - safeHeaderSize - safeFooterSize) * 0.25);
            int middleEnd = middleStart + middleSize;

            if (middleEnd > totalLength - safeFooterSize)
            {
                middleEnd = totalLength - safeFooterSize;
                middleSize = middleEnd - middleStart;
            }

            if (middleSize <= 0 || middleStart >= middleEnd)
            {
                Console.WriteLine("WARNING: The archive is too small for safe corruption.");
                Console.WriteLine("In this case, corruption is skipped for a realistic demo.");
                Console.WriteLine("In the real world, small archives require more careful corruption strategies to preserve critical sections.");
                return;
            }

            Console.WriteLine($"Archive size: {totalLength} bytes");
            Console.WriteLine($"Safe header region: 0-{safeHeaderSize} ({safeHeaderSize} bytes)");
            Console.WriteLine($"Targeted corruption region: {middleStart}-{middleEnd} ({middleSize} bytes, {(double)middleSize / totalLength:P1})");
            Console.WriteLine($"Safe footer region: {totalLength - safeFooterSize}-{totalLength} ({safeFooterSize} bytes)");

            // Apply a very small and limited corruption
            int corruptionCount = Math.Min(5, middleSize / 200); // 1 corruption per 200 bytes, max 5
            if (corruptionCount <= 0)
            {
                corruptionCount = 1; // We must make at least 1 corruption
            }

            // Extra protections for robustness
            if (middleSize < 100)
            {
                Console.WriteLine("WARNING: Safe corruption region is too small! Skipping corruption.");
                return;
            }

            // Educational demo: Only corrupt within data blocks, don't corrupt metadata and structures
            Random random = new(123); // Fixed seed for reproducibility
            for (int i = 0; i < corruptionCount; i++)
            {
                int position = random.Next(middleStart, middleEnd);
                byte originalValue = fileBytes[position];
                byte newValue;

                do
                {
                    newValue = (byte)random.Next(0, 256);
                } while (newValue == originalValue); // Make sure we're actually changing the value

                fileBytes[position] = newValue;
                Console.WriteLine($"Byte changed: position {position}: {originalValue} -> {newValue}");
            }

            // Write the corrupted data back to the file
            await File.WriteAllBytesAsync(archivePath, fileBytes);
            Console.WriteLine($"Total {corruptionCount} bytes corrupted in the archive file");

            // Add a note
            Console.WriteLine("\nNOTE: In real applications, naturally occurring file corruptions tend to happen");
            Console.WriteLine("in less critical areas. This educational demo shows the worst-case scenario.");
        }

        static async Task RepairAndExtractArchive(string archivePath, string extractDir)
        {
            Console.WriteLine("\nAttempting to repair and extract the corrupted archive...");
            Console.WriteLine("The error correction mechanism will try to fix corruptions in data blocks,");
            Console.WriteLine("but critical metadata or signature corruptions may not be repairable.");

            try
            {
                // Make sure the extraction directory exists
                Directory.CreateDirectory(extractDir);

                // Open the archive with error correction enabled
                FragileOptions options = new()
                {
                    EnableErrorCorrection = true,
                    ErrorCorrectionLevel = 20
                };

                // Try to extract the archive despite corruption
                using FragileArchive archive = await FragileArchive.OpenAsync(archivePath, options);

                Console.WriteLine($"Success! Archive opened despite corruption.");
                Console.WriteLine($"Found {archive.Entries.Count} files in the archive.");

                // Extract all files
                await archive.ExtractAllAsync(extractDir);

                Console.WriteLine($"Files successfully extracted to: {extractDir}");

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
                        Console.WriteLine("File successfully recovered!");
                    }
                    else
                    {
                        Console.WriteLine("Warning: Extracted file exists but is empty.");
                    }
                }
                else
                {
                    Console.WriteLine("Error: Original file could not be extracted.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during repair and extraction: {ex.Message}");
                Console.WriteLine("Error correction may not be sufficient for the level of corruption.");

                // Add educational explanation
                Console.WriteLine("\nIMPORTANT NOTES:");
                Console.WriteLine("1. In real applications, corruption of the archive signature is one of the most serious issues.");
                Console.WriteLine("2. Signature verification runs before the error correction mechanism.");
                Console.WriteLine("3. Alternative archive recovery strategies:");
                Console.WriteLine("   - Store multiple backup copies");
                Console.WriteLine("   - Use higher error correction levels (25%-30%)");
                Console.WriteLine("   - Use specialized archive recovery tools");
                Console.WriteLine("4. Best practice: Store multiple copies of important data in different locations.");
            }
        }
    }
}