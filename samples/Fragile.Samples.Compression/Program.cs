using Fragile.Compression;
using System.Diagnostics;
using System.Text;

namespace Fragile.Samples.Compression
{
    /// <summary>
    /// Farklı sıkıştırma algoritmaları ve seviyelerini karşılaştıran örnek uygulama
    /// </summary>
    public class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Sıkıştırma Örneği");
            Console.WriteLine("=========================");

            try
            {
                // Örnek dosya oluştur
                string tempDir = Path.Combine(Path.GetTempPath(), "FragileCompressionSample");
                Directory.CreateDirectory(tempDir);

                string testFilePath = Path.Combine(tempDir, "test.txt");

                // 1 MB büyüklüğünde test dosyası oluştur
                await CreateSampleFile(testFilePath, 1024 * 1024);
                Console.WriteLine($"Örnek dosya oluşturuldu: {testFilePath} (1 MB)");

                string outputDir = Path.Combine(tempDir, "output");
                Directory.CreateDirectory(outputDir);

                // Tüm sıkıştırma algoritmaları ve seviyeleri için test et
                foreach (CompressionAlgorithm algorithm in Enum.GetValues<CompressionAlgorithm>())
                {
                    foreach (CompressionLevel level in Enum.GetValues<CompressionLevel>())
                    {
                        await TestCompression(testFilePath, outputDir, algorithm, level);
                    }
                }

                Console.WriteLine("\nSıkıştırma testi tamamlandı.");
                Console.WriteLine($"Dosyalar burada bulunabilir: {tempDir}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hata: {ex.Message}");
                Console.WriteLine($"Yığın izleme: {ex.StackTrace}");
            }

            Console.WriteLine("\nÇıkmak için bir tuşa basın...");
            Console.ReadKey();
        }

        /// <summary>
        /// Belirtilen algoritma ve seviye ile sıkıştırma testi yapar
        /// </summary>
        private static async Task TestCompression(string inputFilePath, string outputDir, CompressionAlgorithm algorithm, CompressionLevel level)
        {
            // Orijinal dosya bilgilerini al
            FileInfo inputFileInfo = new(inputFilePath);
            long originalSize = inputFileInfo.Length;

            // Sıkıştırılmış dosya adı
            string compressedFileName = $"{Path.GetFileNameWithoutExtension(inputFilePath)}_{algorithm}_{level}.bin";
            string compressedFilePath = Path.Combine(outputDir, compressedFileName);

            // Açılan dosya adı
            string decompressedFileName = $"{Path.GetFileNameWithoutExtension(inputFilePath)}_{algorithm}_{level}_decompressed.txt";
            string decompressedFilePath = Path.Combine(outputDir, decompressedFileName);

            Console.WriteLine($"\nTest: {algorithm} / {level}");

            try
            {
                // Sıkıştırma sağlayıcısını oluştur
                CompressionProvider provider = CompressionProvider.Create(algorithm, level);

                // Sıkıştır
                Stopwatch stopwatch = Stopwatch.StartNew();

                using (FileStream inputStream = new(inputFilePath, FileMode.Open, FileAccess.Read))
                using (FileStream outputStream = new(compressedFilePath, FileMode.Create, FileAccess.Write))
                {
                    Progress<double> progress = new(value =>
                    {
                        Console.Write($"\rSıkıştırma: %{value * 100:F1}");
                    });

                    await provider.CompressAsync(inputStream, outputStream, progress);
                }

                stopwatch.Stop();
                long compressTime = stopwatch.ElapsedMilliseconds;

                // Sıkıştırılmış dosya bilgilerini al
                FileInfo compressedFileInfo = new(compressedFilePath);
                long compressedSize = compressedFileInfo.Length;
                double ratio = 100.0 - ((double)compressedSize / originalSize * 100.0);

                Console.WriteLine($"\rSıkıştırma: %100 - Tamamlandı ({compressTime} ms)");
                Console.WriteLine($"Orijinal boyut: {originalSize:N0} bayt");
                Console.WriteLine($"Sıkıştırılmış boyut: {compressedSize:N0} bayt");
                Console.WriteLine($"Sıkıştırma oranı: %{ratio:F2}");

                try
                {
                    // Sıkıştırmayı aç
                    stopwatch.Restart();

                    using (FileStream inputStream = new(compressedFilePath, FileMode.Open, FileAccess.Read))
                    using (FileStream outputStream = new(decompressedFilePath, FileMode.Create, FileAccess.Write))
                    {
                        Progress<double> progress = new(value =>
                        {
                            Console.Write($"\rAçma: %{value * 100:F1}");
                        });

                        await provider.DecompressAsync(inputStream, outputStream, progress);
                    }

                    stopwatch.Stop();
                    long decompressTime = stopwatch.ElapsedMilliseconds;

                    Console.WriteLine($"\rAçma: %100 - Tamamlandı ({decompressTime} ms)");

                    // Doğrulama
                    bool isValid = await VerifyDecompression(inputFilePath, decompressedFilePath);
                    Console.WriteLine($"Doğrulama: {(isValid ? "Başarılı" : "Başarısız")}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\rAçma hatası: {ex.Message}");
                    // Burada algoritmanın sıkıştırma işlemi başarılı fakat açma işlemi başarısız olduğu kaydediliyor
                    Console.WriteLine($"Geliştirme notu: Bu algoritma için açma (DecompressAsync) metodunun düzeltilmesi gerekiyor");
                }
            }
            catch (NotSupportedException nse)
            {
                Console.WriteLine($"Desteklenmiyor: {nse.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hata: {ex.Message}");
            }
        }

        /// <summary>
        /// Açılan dosyanın orijinali ile aynı olup olmadığını doğrular
        /// </summary>
        private static async Task<bool> VerifyDecompression(string originalFilePath, string decompressedFilePath)
        {
            using FileStream originalStream = new(originalFilePath, FileMode.Open, FileAccess.Read);
            using FileStream decompressedStream = new(decompressedFilePath, FileMode.Open, FileAccess.Read);
            if (originalStream.Length != decompressedStream.Length)
            {
                return false;
            }

            const int bufferSize = 81920; // 80 KB
            byte[] originalBuffer = new byte[bufferSize];
            byte[] decompressedBuffer = new byte[bufferSize];

            int bytesRead;
            while ((bytesRead = await originalStream.ReadAsync(originalBuffer, 0, bufferSize)) > 0)
            {
                await decompressedStream.ReadAsync(decompressedBuffer, 0, bytesRead);

                for (int i = 0; i < bytesRead; i++)
                {
                    if (originalBuffer[i] != decompressedBuffer[i])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Belirtilen boyutta örnek bir metin dosyası oluşturur
        /// </summary>
        private static async Task CreateSampleFile(string filePath, int sizeInBytes)
        {
            using FileStream stream = new(filePath, FileMode.Create, FileAccess.Write);
            using StreamWriter writer = new(stream);
            // Lorem ipsum metni ile rastgele içerik oluştur
            string loremIpsum = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";

            int totalWritten = 0;
            Random random = new();

            while (totalWritten < sizeInBytes)
            {
                // Rastgele sayı ekleyerek benzersiz metin satırları oluştur
                string line = $"{loremIpsum} [{random.Next(10000)}]\n";
                await writer.WriteLineAsync(line);
                totalWritten += line.Length;
            }
        }
    }
}