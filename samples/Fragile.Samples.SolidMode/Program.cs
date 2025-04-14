using Fragile.Compression;
using Fragile.Core;
using Fragile.Models;
using System.Diagnostics;
using System.Text;

namespace Fragile.Samples.SolidMode
{
    public class Program
    {
        // Proje klasörü yolu
        private static readonly string ProjectFolder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        // Örnek için kullanılacak test verisi klasörü
        private static readonly string TestDataFolder = Path.Combine(ProjectFolder, "TestData", "SolidMode");
        // Örnek için çıktı klasörü
        private static readonly string OutputFolder = Path.Combine(ProjectFolder, "Output", "SolidMode");
        // Çeşitli boyutlarda örnek dosyaların depolanacağı liste
        private static readonly List<string> TestFiles = new();

        public static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Kütüphanesi - Solid Mod Örneği");
            Console.WriteLine("=====================================");
            Console.WriteLine();

            // Test dizinlerini hazırla
            PrepareDirectories();

            // Test dosyalarını oluştur
            await CreateTestFiles();

            // Normal mod ve Solid mod karşılaştırması
            await CompareNormalAndSolidModes();

            // Çeşitli dosya türleri için Solid mod testi
            await TestSolidModeWithVariousFileTypes();

            // Büyük arşivlerde Solid mod performans testi
            await TestSolidModePerformance();

            Console.WriteLine();
            Console.WriteLine("Solid Mod testi tamamlandı. Çıktıları incelemek için:");
            Console.WriteLine($"  {OutputFolder}");
            Console.WriteLine();
            Console.WriteLine("Çıkmak için bir tuşa basın...");
            Console.ReadKey();
        }

        /// <summary>
        /// Test için gerekli dizinleri hazırlar
        /// </summary>
        private static void PrepareDirectories()
        {
            Console.WriteLine("Dizinler hazırlanıyor...");

            if (Directory.Exists(TestDataFolder))
            {
                Directory.Delete(TestDataFolder, true);
            }

            if (Directory.Exists(OutputFolder))
            {
                Directory.Delete(OutputFolder, true);
            }

            Directory.CreateDirectory(TestDataFolder);
            Directory.CreateDirectory(OutputFolder);

            // Alt dizinler oluştur
            Directory.CreateDirectory(Path.Combine(TestDataFolder, "Text"));
            Directory.CreateDirectory(Path.Combine(TestDataFolder, "Binary"));
            Directory.CreateDirectory(Path.Combine(TestDataFolder, "Mixed"));

            Console.WriteLine("Dizinler hazırlandı.");
            Console.WriteLine();
        }

        /// <summary>
        /// Test için örnek dosyalar oluşturur
        /// </summary>
        private static async Task CreateTestFiles()
        {
            Console.WriteLine("Test dosyaları oluşturuluyor...");

            // Metin dosyaları oluştur
            for (int i = 1; i <= 10; i++)
            {
                string filePath = Path.Combine(TestDataFolder, "Text", $"text_{i}.txt");
                await File.WriteAllTextAsync(filePath, GenerateRandomText(5000 * i));
                TestFiles.Add(filePath);
            }

            // İkili dosyalar oluştur
            Random random = new();
            for (int i = 1; i <= 5; i++)
            {
                string filePath = Path.Combine(TestDataFolder, "Binary", $"binary_{i}.bin");
                byte[] data = new byte[10000 * i];
                random.NextBytes(data);
                await File.WriteAllBytesAsync(filePath, data);
                TestFiles.Add(filePath);
            }

            // Karışık içerikli dosyalar
            for (int i = 1; i <= 5; i++)
            {
                string filePath = Path.Combine(TestDataFolder, "Mixed", $"mixed_{i}.dat");
                using (FileStream fs = File.Create(filePath))
                {
                    byte[] header = Encoding.UTF8.GetBytes("HEADER\n");
                    await fs.WriteAsync(header);

                    byte[] randomData = new byte[8000 * i];
                    random.NextBytes(randomData);
                    await fs.WriteAsync(randomData);

                    byte[] footer = Encoding.UTF8.GetBytes("\nFOOTER");
                    await fs.WriteAsync(footer);
                }
                TestFiles.Add(filePath);
            }

            Console.WriteLine($"Toplam {TestFiles.Count} test dosyası oluşturuldu.");
            Console.WriteLine();
        }

        /// <summary>
        /// Normal mod ve Solid mod arasındaki farkları karşılaştırır
        /// </summary>
        private static async Task CompareNormalAndSolidModes()
        {
            Console.WriteLine("Normal Mod ve Solid Mod karşılaştırması yapılıyor...");

            string normalArchivePath = Path.Combine(OutputFolder, "normal_mode.fragile");
            string solidArchivePath = Path.Combine(OutputFolder, "solid_mode.fragile");

            // Normal mod arşivi oluştur
            FragileOptions normalSettings = new()
            {
                CompressionAlgorithm = CompressionAlgorithm.Deflate,
                CompressionLevel = CompressionLevel.Ultra,
                UseSolidCompression = false // Normal mod (her dosya ayrı sıkıştırılır)
            };

            Console.WriteLine("Normal mod arşivi oluşturuluyor...");
            Stopwatch normalStopwatch = Stopwatch.StartNew();
            await CreateArchive(normalArchivePath, TestFiles, normalSettings);
            normalStopwatch.Stop();

            FileInfo normalFileInfo = new(normalArchivePath);
            Console.WriteLine($"Normal mod arşiv boyutu: {FormatFileSize(normalFileInfo.Length)}");
            Console.WriteLine($"Normal mod arşivleme süresi: {normalStopwatch.ElapsedMilliseconds} ms");

            // Solid mod arşivi oluştur
            FragileOptions solidOptions = new()
            {
                CompressionAlgorithm = CompressionAlgorithm.Deflate,
                CompressionLevel = CompressionLevel.Ultra,
                UseSolidCompression = true // Solid mod (tüm dosyalar birlikte sıkıştırılır)
            };

            Console.WriteLine("Solid mod arşivi oluşturuluyor...");
            Stopwatch solidStopwatch = Stopwatch.StartNew();
            await CreateArchive(solidArchivePath, TestFiles, solidOptions);
            solidStopwatch.Stop();

            FileInfo solidFileInfo = new(solidArchivePath);
            Console.WriteLine($"Solid mod arşiv boyutu: {FormatFileSize(solidFileInfo.Length)}");
            Console.WriteLine($"Solid mod arşivleme süresi: {solidStopwatch.ElapsedMilliseconds} ms");

            // Sonuçları karşılaştır
            double sizeRatio = (double)solidFileInfo.Length / normalFileInfo.Length;
            double timeRatio = (double)solidStopwatch.ElapsedMilliseconds / normalStopwatch.ElapsedMilliseconds;

            Console.WriteLine();
            Console.WriteLine("Karşılaştırma Sonuçları:");
            Console.WriteLine($"Boyut oranı (Solid/Normal): {sizeRatio:F2} " +
                             $"({(sizeRatio < 1 ? "%" + (100 - (sizeRatio * 100)).ToString("F1") + " daha küçük" : "%" + ((sizeRatio * 100) - 100).ToString("F1") + " daha büyük")})");
            Console.WriteLine($"Süre oranı (Solid/Normal): {timeRatio:F2} " +
                             $"({(timeRatio < 1 ? "%" + (100 - (timeRatio * 100)).ToString("F1") + " daha hızlı" : "%" + ((timeRatio * 100) - 100).ToString("F1") + " daha yavaş")})");
            Console.WriteLine();
        }

        /// <summary>
        /// Çeşitli dosya türleriyle Solid modun davranışını test eder
        /// </summary>
        private static async Task TestSolidModeWithVariousFileTypes()
        {
            Console.WriteLine("Çeşitli dosya türleriyle Solid mod testi yapılıyor...");

            // Metin dosyaları için Solid mod testi
            List<string> textFiles = Directory.GetFiles(Path.Combine(TestDataFolder, "Text")).ToList();
            string textSolidArchivePath = Path.Combine(OutputFolder, "text_solid.fragile");
            string textNormalArchivePath = Path.Combine(OutputFolder, "text_normal.fragile");

            await TestFileTypeCompression("Metin Dosyaları", textFiles, textNormalArchivePath, textSolidArchivePath);

            // İkili dosyalar için Solid mod testi
            List<string> binaryFiles = Directory.GetFiles(Path.Combine(TestDataFolder, "Binary")).ToList();
            string binarySolidArchivePath = Path.Combine(OutputFolder, "binary_solid.fragile");
            string binaryNormalArchivePath = Path.Combine(OutputFolder, "binary_normal.fragile");

            await TestFileTypeCompression("İkili Dosyalar", binaryFiles, binaryNormalArchivePath, binarySolidArchivePath);

            // Karışık dosyalar için Solid mod testi
            List<string> mixedFiles = Directory.GetFiles(Path.Combine(TestDataFolder, "Mixed")).ToList();
            string mixedSolidArchivePath = Path.Combine(OutputFolder, "mixed_solid.fragile");
            string mixedNormalArchivePath = Path.Combine(OutputFolder, "mixed_normal.fragile");

            await TestFileTypeCompression("Karışık İçerikli Dosyalar", mixedFiles, mixedNormalArchivePath, mixedSolidArchivePath);
        }

        /// <summary>
        /// Büyük arşivlerde Solid modun performansını test eder
        /// </summary>
        private static async Task TestSolidModePerformance()
        {
            Console.WriteLine("Büyük arşivlerde Solid mod performans testi yapılıyor...");

            // Büyük dosyalar oluştur
            List<string> largeFiles = new();
            for (int i = 1; i <= 3; i++)
            {
                string filePath = Path.Combine(TestDataFolder, $"large_{i}.dat");
                await CreateLargeFile(filePath, 10 * 1024 * 1024); // 10 MB
                largeFiles.Add(filePath);
            }

            // Normal mod ile arşivle
            string largeNormalArchivePath = Path.Combine(OutputFolder, "large_normal.fragile");
            FragileOptions normalSettings = new()
            {
                CompressionAlgorithm = CompressionAlgorithm.Deflate,
                CompressionLevel = CompressionLevel.Ultra,
                UseSolidCompression = false
            };

            Console.WriteLine("Büyük dosyaları normal modda arşivleme...");
            Stopwatch normalStopwatch = Stopwatch.StartNew();
            await CreateArchive(largeNormalArchivePath, largeFiles, normalSettings);
            normalStopwatch.Stop();

            // Solid mod ile arşivle
            string largeSolidArchivePath = Path.Combine(OutputFolder, "large_solid.fragile");
            FragileOptions solidOptions = new()
            {
                CompressionAlgorithm = CompressionAlgorithm.Deflate,
                CompressionLevel = CompressionLevel.Ultra,
                UseSolidCompression = true
            };

            Console.WriteLine("Büyük dosyaları solid modda arşivleme...");
            Stopwatch solidStopwatch = Stopwatch.StartNew();
            await CreateArchive(largeSolidArchivePath, largeFiles, solidOptions);
            solidStopwatch.Stop();

            // Sonuçları karşılaştır
            FileInfo normalFileInfo = new(largeNormalArchivePath);
            FileInfo solidFileInfo = new(largeSolidArchivePath);

            Console.WriteLine();
            Console.WriteLine("Büyük Dosyalar İçin Performans Sonuçları:");
            Console.WriteLine($"Normal mod arşiv boyutu: {FormatFileSize(normalFileInfo.Length)}");
            Console.WriteLine($"Normal mod arşivleme süresi: {normalStopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Solid mod arşiv boyutu: {FormatFileSize(solidFileInfo.Length)}");
            Console.WriteLine($"Solid mod arşivleme süresi: {solidStopwatch.ElapsedMilliseconds} ms");

            double sizeRatio = (double)solidFileInfo.Length / normalFileInfo.Length;
            double timeRatio = (double)solidStopwatch.ElapsedMilliseconds / normalStopwatch.ElapsedMilliseconds;

            Console.WriteLine($"Boyut oranı (Solid/Normal): {sizeRatio:F2}");
            Console.WriteLine($"Süre oranı (Solid/Normal): {timeRatio:F2}");
            Console.WriteLine();

            // Çıkarma performansını test et
            await TestExtractionPerformance(largeNormalArchivePath, largeSolidArchivePath);
        }

        /// <summary>
        /// Normal ve Solid mod arşivlerinin çıkarma performansını test eder
        /// </summary>
        private static async Task TestExtractionPerformance(string normalArchivePath, string solidArchivePath)
        {
            Console.WriteLine("Arşiv çıkarma performansı test ediliyor...");

            string normalExtractFolder = Path.Combine(OutputFolder, "extract_normal");
            string solidExtractFolder = Path.Combine(OutputFolder, "extract_solid");

            Directory.CreateDirectory(normalExtractFolder);
            Directory.CreateDirectory(solidExtractFolder);

            // Normal mod arşivini çıkar
            Console.WriteLine("Normal mod arşivini çıkarma...");
            Stopwatch normalStopwatch = Stopwatch.StartNew();
            await ExtractArchive(normalArchivePath, normalExtractFolder);
            normalStopwatch.Stop();

            // Solid mod arşivini çıkar
            Console.WriteLine("Solid mod arşivini çıkarma...");
            Stopwatch solidStopwatch = Stopwatch.StartNew();
            await ExtractArchive(solidArchivePath, solidExtractFolder);
            solidStopwatch.Stop();

            Console.WriteLine();
            Console.WriteLine("Çıkarma Performansı Sonuçları:");
            Console.WriteLine($"Normal mod çıkarma süresi: {normalStopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Solid mod çıkarma süresi: {solidStopwatch.ElapsedMilliseconds} ms");

            double timeRatio = (double)solidStopwatch.ElapsedMilliseconds / normalStopwatch.ElapsedMilliseconds;
            Console.WriteLine($"Çıkarma süre oranı (Solid/Normal): {timeRatio:F2} " +
                            $"({(timeRatio < 1 ? "%" + (100 - (timeRatio * 100)).ToString("F1") + " daha hızlı" : "%" + ((timeRatio * 100) - 100).ToString("F1") + " daha yavaş")})");
            Console.WriteLine();

            // Tek dosya çıkarma performansı
            await TestSingleFileExtractionPerformance(normalArchivePath, solidArchivePath);
        }

        /// <summary>
        /// Normal ve Solid mod arşivlerinden tek dosya çıkarma performansını test eder
        /// </summary>
        private static async Task TestSingleFileExtractionPerformance(string normalArchivePath, string solidArchivePath)
        {
            Console.WriteLine("Tek dosya çıkarma performansı test ediliyor...");

            string normalExtractFolder = Path.Combine(OutputFolder, "extract_normal_single");
            string solidExtractFolder = Path.Combine(OutputFolder, "extract_solid_single");

            Directory.CreateDirectory(normalExtractFolder);
            Directory.CreateDirectory(solidExtractFolder);

            // Son dosyayı seç
            string targetFile = Path.GetFileName(TestFiles.Last());

            // Normal mod arşivinden tek dosya çıkar
            Console.WriteLine($"Normal mod arşivinden tek dosya çıkarma: {targetFile}");
            Stopwatch normalStopwatch = Stopwatch.StartNew();
            await ExtractSingleFile(normalArchivePath, normalExtractFolder, targetFile);
            normalStopwatch.Stop();

            // Solid mod arşivinden tek dosya çıkar
            Console.WriteLine($"Solid mod arşivinden tek dosya çıkarma: {targetFile}");
            Stopwatch solidStopwatch = Stopwatch.StartNew();
            await ExtractSingleFile(solidArchivePath, solidExtractFolder, targetFile);
            solidStopwatch.Stop();

            Console.WriteLine();
            Console.WriteLine("Tek Dosya Çıkarma Performansı Sonuçları:");
            Console.WriteLine($"Normal moddan tek dosya çıkarma süresi: {normalStopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Solid moddan tek dosya çıkarma süresi: {solidStopwatch.ElapsedMilliseconds} ms");

            double timeRatio = (double)solidStopwatch.ElapsedMilliseconds / normalStopwatch.ElapsedMilliseconds;
            Console.WriteLine($"Tek dosya çıkarma süre oranı (Solid/Normal): {timeRatio:F2} " +
                            $"({(timeRatio < 1 ? "%" + (100 - (timeRatio * 100)).ToString("F1") + " daha hızlı" : "%" + ((timeRatio * 100) - 100).ToString("F1") + " daha yavaş")})");
            Console.WriteLine();
        }

        /// <summary>
        /// Belirli bir dosya türü grubunun Normal mod ve Solid moddaki sıkıştırma performansını test eder
        /// </summary>
        private static async Task TestFileTypeCompression(string fileTypeLabel, List<string> files,
                                                      string normalArchivePath, string solidArchivePath)
        {
            Console.WriteLine($"{fileTypeLabel} için test yapılıyor...");

            // Normal mod arşivi oluştur
            FragileOptions normalSettings = new()
            {
                CompressionAlgorithm = CompressionAlgorithm.Deflate,
                CompressionLevel = CompressionLevel.Ultra,
                UseSolidCompression = false
            };

            await CreateArchive(normalArchivePath, files, normalSettings);
            FileInfo normalFileInfo = new(normalArchivePath);

            // Solid mod arşivi oluştur
            FragileOptions solidSettings = new()
            {
                CompressionAlgorithm = CompressionAlgorithm.Deflate,
                CompressionLevel = CompressionLevel.Ultra,
                UseSolidCompression = true
            };

            await CreateArchive(solidArchivePath, files, solidSettings);
            FileInfo solidFileInfo = new(solidArchivePath);

            // Sonuçları yazdır
            double sizeRatio = (double)solidFileInfo.Length / normalFileInfo.Length;
            Console.WriteLine($"{fileTypeLabel} sonuçları:");
            Console.WriteLine($"  Normal mod boyutu: {FormatFileSize(normalFileInfo.Length)}");
            Console.WriteLine($"  Solid mod boyutu: {FormatFileSize(solidFileInfo.Length)}");
            Console.WriteLine($"  Boyut Oranı (Solid/Normal): {sizeRatio:F2} " +
                             $"({(sizeRatio < 1 ? "%" + (100 - (sizeRatio * 100)).ToString("F1") + " daha küçük" : "%" + ((sizeRatio * 100) - 100).ToString("F1") + " daha büyük")})");
            Console.WriteLine();
        }

        /// <summary>
        /// Rastgele metin oluşturur
        /// </summary>
        private static string GenerateRandomText(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,;:!?-_\n\r";
            Random random = new();
            StringBuilder sb = new(length);

            for (int i = 0; i < length; i++)
            {
                // Satır sonları ekle
                if (i % 80 == 0 && i > 0)
                {
                    sb.Append("\n");
                }
                else
                {
                    sb.Append(chars[random.Next(chars.Length)]);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Belirtilen boyutta büyük test dosyası oluşturur
        /// </summary>
        private static async Task CreateLargeFile(string filePath, int sizeInBytes)
        {
            using FileStream fs = File.Create(filePath);
            Random random = new();
            byte[] buffer = new byte[8192]; // 8 KB buffer

            int bytesRemaining = sizeInBytes;
            while (bytesRemaining > 0)
            {
                int bytesToWrite = Math.Min(buffer.Length, bytesRemaining);
                random.NextBytes(buffer);
                await fs.WriteAsync(buffer, 0, bytesToWrite);
                bytesRemaining -= bytesToWrite;
            }
        }

        /// <summary>
        /// Fragile arşivi oluşturur
        /// </summary>
        private static async Task CreateArchive(string archivePath, List<string> filesToAdd, FragileOptions settings)
        {
            try
            {
                using FragileArchive archive = await FragileArchive.CreateAsync(archivePath, settings);

                foreach (string filePath in filesToAdd)
                {
                    string relativePath = Path.GetFileName(filePath);
                    await archive.AddFileAsync(filePath, relativePath);
                }

                await archive.SaveAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Arşiv oluşturma hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Fragile arşivini çıkarır
        /// </summary>
        private static async Task ExtractArchive(string archivePath, string extractFolder)
        {
            try
            {
                using FragileArchive archive = await FragileArchive.OpenAsync(archivePath);
                await archive.ExtractAllAsync(extractFolder);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Arşiv çıkarma hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Fragile arşivinden tek dosya çıkarır
        /// </summary>
        private static async Task ExtractSingleFile(string archivePath, string extractFolder, string fileName)
        {
            try
            {
                using FragileArchive archive = await FragileArchive.OpenAsync(archivePath);
                var entry = archive.Entries.FirstOrDefault(e =>
                    Path.GetFileName(e.Name).Equals(fileName, StringComparison.OrdinalIgnoreCase));

                if (entry != null)
                {
                    await entry.ExtractToAsync(Path.Combine(extractFolder, fileName));
                }
                else
                {
                    Console.WriteLine($"Dosya bulunamadı: {fileName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Tek dosya çıkarma hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Dosya boyutunu okunabilir formatta döndürür
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }

            return $"{number:n2} {suffixes[counter]}";
        }
    }
}
