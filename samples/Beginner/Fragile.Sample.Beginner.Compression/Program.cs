using Fragile.Compression;
using Fragile.Core;
using Fragile.Models;
using System.Text;

namespace Fragile.Sample.Beginner.Compression
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Compression Sample");
            Console.WriteLine("=========================");

            // Create sample directory
            string sampleDir = "Sample";
            Directory.CreateDirectory(sampleDir);

            // Create a large text file to demonstrate compression
            string largeFilePath = Path.Combine(sampleDir, "large_text.txt");
            CreateLargeTextFile(largeFilePath, 10000); // 10,000 lines of text

            // Get file size before compression
            long originalSize = new FileInfo(largeFilePath).Length;
            Console.WriteLine($"Original file size: {originalSize:N0} bytes");

            // Create archives with different compression levels
            await CreateCompressedArchive(sampleDir, largeFilePath, originalSize, CompressionLevel.Fastest, "fastest.frgl");
            await CreateCompressedArchive(sampleDir, largeFilePath, originalSize, CompressionLevel.Normal, "normal.frgl");
            await CreateCompressedArchive(sampleDir, largeFilePath, originalSize, CompressionLevel.Ultra, "ultra.frgl");

            // Compare file sizes
            await CompareArchivedFileSizes(sampleDir);

            Console.WriteLine("\nCompression sample completed successfully!");
            Console.WriteLine("Check the 'Sample' directory for the created files.");

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static void CreateLargeTextFile(string filePath, int lineCount)
        {
            Console.WriteLine($"Creating sample text file with {lineCount:N0} lines...");

            using StreamWriter writer = new(filePath);
            for (int i = 0; i < lineCount; i++)
            {
                // Generate a line with repeating patterns (highly compressible)
                writer.WriteLine($"Line {i}: This is a sample text with repeating content. " +
                    $"The quick brown fox jumps over the lazy dog. " +
                    $"Lorem ipsum dolor sit amet, consectetur adipiscing elit. " +
                    $"This text is repeated to create a large file that can be compressed efficiently.");
            }
        }

        static async Task CreateCompressedArchive(string outputDir, string filePath, long originalSize, CompressionLevel level, string archiveName)
        {
            Console.WriteLine($"\nCreating archive with {level} compression level...");
            string archivePath = Path.Combine(outputDir, archiveName);

            // Configure compression options
            FragileOptions options = new()
            {
                CompressionLevel = level,
                CompressionAlgorithm = CompressionAlgorithm.Deflate // Using Deflate
            };

            // Create the archive
            using FragileArchive archive = await FragileArchive.CreateAsync(archivePath, options);
            await archive.AddFileAsync(filePath);
            await archive.SaveAsync();

            // Report the compressed file size
            long compressedSize = new FileInfo(archivePath).Length;
            double compressionRatio = (double)originalSize / compressedSize;
            Console.WriteLine($"Archive '{archiveName}' size: {compressedSize:N0} bytes");
            Console.WriteLine($"Compression ratio: {compressionRatio:F2}x (saved {1 - ((double)compressedSize / originalSize):P2})");
        }

        static async Task CompareArchivedFileSizes(string sampleDir)
        {
            Console.WriteLine("\nComparing archive sizes:");
            Console.WriteLine("========================");

            string[] archiveFiles = {
                Path.Combine(sampleDir, "fastest.frgl"),
                Path.Combine(sampleDir, "normal.frgl"),
                Path.Combine(sampleDir, "ultra.frgl")
            };

            Console.WriteLine("|  Compression Level  |  File Size  |  Ratio  |");
            Console.WriteLine("|---------------------|-------------|---------|");

            long originalSize = new FileInfo(Path.Combine(sampleDir, "large_text.txt")).Length;

            foreach (string archivePath in archiveFiles)
            {
                if (File.Exists(archivePath))
                {
                    FileInfo fileInfo = new(archivePath);
                    string levelName = Path.GetFileNameWithoutExtension(archivePath);
                    double ratio = (double)originalSize / fileInfo.Length;

                    Console.WriteLine($"|  {levelName,-17}  |  {fileInfo.Length,9:N0}  |  {ratio,5:F2}x  |");
                }
            }
        }
    }
}