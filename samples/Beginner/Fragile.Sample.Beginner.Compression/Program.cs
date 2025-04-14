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
            Console.WriteLine($"Orijinal dosya boyutu: {originalSize:N0} bayt");

            // Test farklı algoritmaları Normal sıkıştırma seviyesi ile
            Console.WriteLine("\nFarklı algoritmaları Normal sıkıştırma seviyesi ile test ediliyor...");
            await TestCompressionAlgorithm(sampleDir, largeFilePath, originalSize, CompressionAlgorithm.Deflate, "deflate");
            await TestCompressionAlgorithm(sampleDir, largeFilePath, originalSize, CompressionAlgorithm.LZMA, "lzma");
            await TestCompressionAlgorithm(sampleDir, largeFilePath, originalSize, CompressionAlgorithm.BZip2, "bzip2");
            await TestCompressionAlgorithm(sampleDir, largeFilePath, originalSize, CompressionAlgorithm.ZStd, "zstd");
            await TestCompressionAlgorithm(sampleDir, largeFilePath, originalSize, CompressionAlgorithm.LZ4, "lz4");

            // Ayrıca ZStd ile farklı sıkıştırma seviyelerini test edelim
            Console.WriteLine("\nZStd algoritmasında farklı sıkıştırma seviyelerini test ediliyor...");
            await TestCompressionLevel(sampleDir, largeFilePath, originalSize, CompressionAlgorithm.ZStd, CompressionLevel.Fastest, "zstd_fastest");
            await TestCompressionLevel(sampleDir, largeFilePath, originalSize, CompressionAlgorithm.ZStd, CompressionLevel.Normal, "zstd_normal");
            await TestCompressionLevel(sampleDir, largeFilePath, originalSize, CompressionAlgorithm.ZStd, CompressionLevel.Ultra, "zstd_ultra");

            // Karşılaştırma tablosu
            await CompareAlgorithms(sampleDir, originalSize);
            await CompareLevels(sampleDir, originalSize);

            Console.WriteLine("\nSıkıştırma testi başarıyla tamamlandı!");
            Console.WriteLine("Oluşturulan dosyaları 'Sample' dizininde kontrol edebilirsiniz.");

            Console.WriteLine("Çıkmak için bir tuşa basın...");
            Console.ReadKey();
        }

        static void CreateLargeTextFile(string filePath, int lineCount)
        {
            Console.WriteLine($"{lineCount:N0} satırlık örnek metin dosyası oluşturuluyor...");

            using StreamWriter writer = new(filePath);
            for (int i = 0; i < lineCount; i++)
            {
                // Generate a line with repeating patterns (highly compressible)
                writer.WriteLine($"Satır {i}: Bu tekrarlayan içeriğe sahip örnek bir metindir. " +
                    $"Hızlı kahverengi tilki tembel köpeğin üzerinden atlar. " +
                    $"Lorem ipsum dolor sit amet, consectetur adipiscing elit. " +
                    $"Bu metin, verimli bir şekilde sıkıştırılabilecek büyük bir dosya oluşturmak için tekrarlanmaktadır.");
            }
        }

        static async Task TestCompressionAlgorithm(string outputDir, string filePath, long originalSize, 
            CompressionAlgorithm algorithm, string filePrefix)
        {
            Console.WriteLine($"\n{algorithm} algoritması test ediliyor...");
            string archivePath = Path.Combine(outputDir, $"{filePrefix}.frgl");

            // Configure compression options
            FragileOptions options = new()
            {
                CompressionLevel = CompressionLevel.Normal,
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
            
            Console.WriteLine($"Arşiv '{filePrefix}.frgl' boyutu: {compressedSize:N0} bayt");
            Console.WriteLine($"Sıkıştırma oranı: {compressionRatio:F2}x (kazanç: {savingsPercentage:P2})");
        }

        static async Task TestCompressionLevel(string outputDir, string filePath, long originalSize,
            CompressionAlgorithm algorithm, CompressionLevel level, string archiveName)
        {
            Console.WriteLine($"\n{algorithm} algoritması {level} seviyesi ile test ediliyor...");
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
            
            Console.WriteLine($"Arşiv '{archiveName}.frgl' boyutu: {compressedSize:N0} bayt");
            Console.WriteLine($"Sıkıştırma oranı: {compressionRatio:F2}x (kazanç: {savingsPercentage:P2})");
        }

        static async Task CompareAlgorithms(string sampleDir, long originalSize)
        {
            Console.WriteLine("\nAlgoritma karşılaştırması:");
            Console.WriteLine("=========================");

            string[] algorithms = { "deflate", "lzma", "bzip2", "zstd", "lz4" };
            
            Console.WriteLine("|  Algoritma   |   Dosya Boyutu   |   Oran   |   Kazanç   |");
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
            Console.WriteLine("\nZStd sıkıştırma seviyeleri karşılaştırması:");
            Console.WriteLine("=======================================");

            string[] levels = { "zstd_fastest", "zstd_normal", "zstd_ultra" };
            
            Console.WriteLine("|  Seviye      |   Dosya Boyutu   |   Oran   |   Kazanç   |");
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
                    string levelName = level.Replace("zstd_", "");

                    Console.WriteLine($"|  {levelName,-10}  |  {fileInfo.Length,14:N0}  |  {ratio,6:F2}x  |  {savings,8:P2}  |");
                }
            }
        }
    }
}