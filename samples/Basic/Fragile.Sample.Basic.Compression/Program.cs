using Fragile.Compression;
using Fragile.Core;
using Fragile.Models;
using System.Text;

namespace Fragile.Sample.Basic.Compression
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Compression Sample");
            Console.WriteLine("======================");

            // Create sample directory
            string sampleDir = "Sample";
            Directory.CreateDirectory(sampleDir);

            // Create a sample file to compress
            string sampleFilePath = Path.Combine(sampleDir, "sample.txt");
            await CreateSampleFileAsync(sampleFilePath);

            // Get original file size
            long originalSize = new FileInfo(sampleFilePath).Length;
            Console.WriteLine($"Sample file size: {originalSize:N0} bytes");

            // Compare different compression algorithms
            Console.WriteLine("\nComparison of Different Compression Algorithms");
            Console.WriteLine("=========================================");

            // Test different compression algorithms
            await TestCompressionAlgorithm(sampleDir, sampleFilePath, originalSize, CompressionAlgorithm.Store, "store");
            await TestCompressionAlgorithm(sampleDir, sampleFilePath, originalSize, CompressionAlgorithm.Deflate, "deflate");
            await TestCompressionAlgorithm(sampleDir, sampleFilePath, originalSize, CompressionAlgorithm.Brotli, "brotli");

            // Display comparison table
            ShowComparisonTable(sampleDir, originalSize);

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static async Task CreateSampleFileAsync(string filePath)
        {
            Console.WriteLine($"Creating sample file: {filePath}");

            // Generate sample text data
            StringBuilder sb = new();
            for (int i = 0; i < 1000; i++)
            {
                sb.AppendLine($"This is line {i} of the sample text file for testing compression algorithms in Fragile library.");
            }

            // Write to file
            await File.WriteAllTextAsync(filePath, sb.ToString());
            Console.WriteLine("Sample file created.");
        }

        static async Task TestCompressionAlgorithm(string outputDir, string filePath, long originalSize,
            CompressionAlgorithm algorithm, string filePrefix)
        {
            Console.WriteLine($"\nTesting {algorithm} algorithm...");
            string archivePath = Path.Combine(outputDir, $"{filePrefix}.frgl");

            // Configure compression options
            FragileOptions options = new()
            {
                CompressionAlgorithm = algorithm,
                CompressionLevel = CompressionLevel.Ultra
            };

            // Create the archive
            using FragileArchive archive = await FragileArchive.CreateAsync(archivePath, options);
            await archive.AddFileAsync(filePath);
            await archive.SaveAsync();

            // Report the compressed file size
            long compressedSize = new FileInfo(archivePath).Length;
            double compressionRatio = (double)originalSize / compressedSize;
            double savingsPercentage = 1 - ((double)compressedSize / originalSize);

            Console.WriteLine($"Archive size '{filePrefix}.frgl': {compressedSize:N0} bytes");
            Console.WriteLine($"Compression ratio: {compressionRatio:F2}x (savings: {savingsPercentage:P2})");
        }

        static void ShowComparisonTable(string sampleDir, long originalSize)
        {
            Console.WriteLine("\nAlgorithm comparison summary:");
            Console.WriteLine("=========================");

            string[] algorithms = { "store", "deflate", "brotli" };

            Console.WriteLine("| Algorithm | File Size  | Ratio | Savings |");
            Console.WriteLine("|-----------|------------|-------|---------|");

            foreach (string alg in algorithms)
            {
                string archivePath = Path.Combine(sampleDir, $"{alg}.frgl");
                if (File.Exists(archivePath))
                {
                    FileInfo fileInfo = new(archivePath);
                    double ratio = (double)originalSize / fileInfo.Length;
                    double savings = 1 - ((double)fileInfo.Length / originalSize);

                    Console.WriteLine($"| {alg,-9} | {fileInfo.Length,9:N0} | {ratio,5:F2}x | {savings,7:P2} |");
                }
            }
        }
    }
}