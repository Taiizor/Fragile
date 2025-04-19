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

            // Test different algorithms with Normal compression level
            Console.WriteLine("\nTesting different algorithms with Normal compression level...");
            await TestCompressionAlgorithm(sampleDir, largeFilePath, originalSize, CompressionAlgorithm.GZip, "gzip");
            await TestCompressionAlgorithm(sampleDir, largeFilePath, originalSize, CompressionAlgorithm.ZLib, "zlib");
            await TestCompressionAlgorithm(sampleDir, largeFilePath, originalSize, CompressionAlgorithm.Store, "store");
            await TestCompressionAlgorithm(sampleDir, largeFilePath, originalSize, CompressionAlgorithm.Brotli, "brotli");
            await TestCompressionAlgorithm(sampleDir, largeFilePath, originalSize, CompressionAlgorithm.Deflate, "deflate");

            // Also test different compression levels with Brotli
            Console.WriteLine("\nTesting different compression levels with Brotli algorithm...");
            await TestCompressionLevel(sampleDir, largeFilePath, originalSize, CompressionAlgorithm.Brotli, CompressionLevel.Fastest, "brotli_fastest");
            await TestCompressionLevel(sampleDir, largeFilePath, originalSize, CompressionAlgorithm.Brotli, CompressionLevel.Normal, "brotli_normal");
            await TestCompressionLevel(sampleDir, largeFilePath, originalSize, CompressionAlgorithm.Brotli, CompressionLevel.Ultra, "brotli_ultra");

            // Comparison tables
            await CompareAlgorithms(sampleDir, originalSize);
            await CompareLevels(sampleDir, originalSize);

            Console.WriteLine("\nCompression test completed successfully!");
            Console.WriteLine("You can check the generated files in the 'Sample' directory.");

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static void CreateLargeTextFile(string filePath, int lineCount)
        {
            Console.WriteLine($"Creating a sample text file with {lineCount:N0} lines...");

            using StreamWriter writer = new(filePath);
            for (int i = 0; i < lineCount; i++)
            {
                // Generate a line with repeating patterns (highly compressible)
                writer.WriteLine($"Line {i}: This is a sample text with repeating content. " +
                    $"The quick brown fox jumps over the lazy dog. " +
                    $"Lorem ipsum dolor sit amet, consectetur adipiscing elit. " +
                    $"This text is repeated to create a large file that can be efficiently compressed.");
            }
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

            Console.WriteLine($"Archive '{filePrefix}.frgl' size: {compressedSize:N0} bytes");
            Console.WriteLine($"Compression ratio: {compressionRatio:F2}x (savings: {savingsPercentage:P2})");
        }

        static async Task TestCompressionLevel(string outputDir, string filePath, long originalSize,
            CompressionAlgorithm algorithm, CompressionLevel level, string archiveName)
        {
            Console.WriteLine($"\nTesting {algorithm} algorithm with {level} level...");
            string archivePath = Path.Combine(outputDir, $"{archiveName}.frgl");

            // Configure compression options
            FragileOptions options = new()
            {
                CompressionLevel = level,
                CompressionAlgorithm = algorithm
            };

            // Create the archive
            using FragileArchive archive = await FragileArchive.CreateAsync(archivePath, options);
            await archive.AddFileAsync(filePath);
            await archive.SaveAsync();

            // Report the compressed file size
            long compressedSize = new FileInfo(archivePath).Length;
            double compressionRatio = (double)originalSize / compressedSize;
            double savingsPercentage = 1 - ((double)compressedSize / originalSize);

            Console.WriteLine($"Archive '{archiveName}.frgl' size: {compressedSize:N0} bytes");
            Console.WriteLine($"Compression ratio: {compressionRatio:F2}x (savings: {savingsPercentage:P2})");
        }

        static async Task CompareAlgorithms(string sampleDir, long originalSize)
        {
            Console.WriteLine("\nAlgorithm comparison:");
            Console.WriteLine("=========================");

            string[] algorithms = { "gzip", "zlib", "store", "brotli", "deflate" };

            Console.WriteLine("|  Algorithm   |   File Size      |   Ratio   |   Savings   |");
            Console.WriteLine("|--------------|------------------|----------|------------|");

            foreach (string alg in algorithms)
            {
                string archivePath = Path.Combine(sampleDir, $"{alg}.frgl");
                if (File.Exists(archivePath))
                {
                    FileInfo fileInfo = new(archivePath);
                    double ratio = (double)originalSize / fileInfo.Length;
                    double savings = 1 - ((double)fileInfo.Length / originalSize);

                    Console.WriteLine($"|  {alg,-10}  |  {fileInfo.Length,14:N0}  |  {ratio,6:F2}x  |  {savings,8:P2}  |");
                }
            }
        }

        static async Task CompareLevels(string sampleDir, long originalSize)
        {
            Console.WriteLine("\nBrotli compression levels comparison:");
            Console.WriteLine("=======================================");

            string[] levels = { "brotli_fastest", "brotli_normal", "brotli_ultra" };

            Console.WriteLine("|  Level       |   File Size      |   Ratio   |   Savings   |");
            Console.WriteLine("|--------------|------------------|----------|------------|");

            foreach (string level in levels)
            {
                string archivePath = Path.Combine(sampleDir, $"{level}.frgl");
                if (File.Exists(archivePath))
                {
                    FileInfo fileInfo = new(archivePath);
                    double ratio = (double)originalSize / fileInfo.Length;
                    double savings = 1 - ((double)fileInfo.Length / originalSize);

                    // Extract level name for display
                    string levelName = level.Replace("brotli_", "");

                    Console.WriteLine($"|  {levelName,-10}  |  {fileInfo.Length,14:N0}  |  {ratio,6:F2}x  |  {savings,8:P2}  |");
                }
            }
        }
    }
}