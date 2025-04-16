using Fragile.Compression;
using Fragile.Core;
using Fragile.Models;
using System.Text;

namespace Fragile.Sample.Basic.Decompression
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Decompression Sample");
            Console.WriteLine("======================");

            // Create sample directory
            string sampleDir = "Sample";
            Directory.CreateDirectory(sampleDir);

            // Set up options for the archive
            FragileOptions options = new()
            {
                CompressionLevel = CompressionLevel.Ultra,
                CompressionAlgorithm = CompressionAlgorithm.Deflate
            };

            // Create a sample archive first
            string archivePath = await CreateSampleArchiveAsync(sampleDir, options);

            // Decompress the archive - this is the main example part
            Console.WriteLine("\nDecompression Example");
            Console.WriteLine("--------------------");

            // Create extraction directory
            string extractDir = Path.Combine(sampleDir, "Extracted");
            Directory.CreateDirectory(extractDir);

            // Open the archive
            using FragileArchive archive = await FragileArchive.OpenAsync(archivePath, options);

            // Display archive information
            Console.WriteLine($"Archive contains {archive.Entries.Count} files");

            // Extract all files
            archive.ExtractAll(extractDir);
            Console.WriteLine($"Files extracted to: {extractDir}");

            // List extracted files
            string[] extractedFiles = Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories);
            Console.WriteLine($"Extracted {extractedFiles.Length} files:");
            foreach (string file in extractedFiles)
            {
                Console.WriteLine($"- {Path.GetFileName(file)}");
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        // Helper method to create a sample archive to decompress
        static async Task<string> CreateSampleArchiveAsync(string sampleDir, FragileOptions options)
        {
            Console.WriteLine("Creating a sample archive...");

            // Create a sample text file
            string sampleFilePath = Path.Combine(sampleDir, "sample.txt");
            await File.WriteAllTextAsync(sampleFilePath, "This is a sample text file: X" + new string('Y', 25000) + "Z.");

            // Create archive
            string archivePath = Path.Combine(sampleDir, "sample_archive.frgl");
            using FragileArchive archive = await FragileArchive.CreateAsync(archivePath, options);
            await archive.AddFileAsync(sampleFilePath);
            await archive.SaveAsync();

            Console.WriteLine($"Created archive: {archivePath}");
            return archivePath;
        }
    }
}