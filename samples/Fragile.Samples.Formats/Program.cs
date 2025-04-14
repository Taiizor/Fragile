using Fragile.Compression;
using Fragile.Core;
using Fragile.Formats;
using Fragile.Models;
using System.Text;

namespace Fragile.Samples.Formats
{
    /// <summary>
    /// Fragile format uyumluluğu özelliklerini gösteren örnek uygulama
    /// </summary>
    public class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Format Uyumluluğu Örneği");
            Console.WriteLine("=================================");

            try
            {
                // Geçici dizin oluştur
                string tempDir = Path.Combine(Path.GetTempPath(), "FragileFormatsSample");
                Directory.CreateDirectory(tempDir);

                // Örnek dizin ve dosyalar için test dizini
                string testDir = Path.Combine(tempDir, "TestFiles");
                Directory.CreateDirectory(testDir);

                // Arşivlenecek örnek dosyaları oluştur
                await CreateSampleFiles(testDir);

                // Farklı format uyumluluğu ile arşivler oluştur ve test et
                await TestFormatCompatibility(testDir, tempDir);
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
        /// Farklı format uyumluluğu türleri için arşiv oluşturma ve dönüştürme
        /// </summary>
        private static async Task TestFormatCompatibility(string sourceDir, string outputDir)
        {
            // Tüm format uyumluluğu türleri için test
            foreach (FormatCompatibility format in Enum.GetValues<FormatCompatibility>())
            {
                // Bazı formatlar henüz uygulanmamış olabilir
                try
                {
                    // Format adını görüntüle
                    Console.WriteLine($"\n\n📦 FORMAT: {format}");
                    Console.WriteLine("==============================");

                    // Bu format türü için arşiv adı
                    string archiveFileName = $"archive_{format}.frgl";
                    string archivePath = Path.Combine(outputDir, archiveFileName);

                    // Bu format için arşiv oluştur
                    await CreateArchiveWithFormat(sourceDir, archivePath, format);

                    // Format algılama testi
                    await DetectArchiveFormat(archivePath);

                    // Çıkarma testleri
                    await ExtractArchive(archivePath, Path.Combine(outputDir, $"extracted_{format}"));

                    // Format dönüşüm testleri (sadece desteklenen formatlar için)
                    if (format != FormatCompatibility.Native)
                    {
                        string nativeArchivePath = Path.Combine(outputDir, $"converted_to_native_{format}.frgl");
                        await ConvertFormat(archivePath, nativeArchivePath, FormatCompatibility.Native);
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
        }

        /// <summary>
        /// Belirtilen format uyumluluğu ile arşiv oluşturur
        /// </summary>
        private static async Task CreateArchiveWithFormat(string sourceDir, string archivePath, FormatCompatibility format)
        {
            Console.WriteLine($"\n📥 Arşiv oluşturuluyor: {Path.GetFileName(archivePath)}");
            Console.WriteLine($"Format: {format}");
            Console.WriteLine($"Kaynak: {sourceDir}");

            try
            {
                // Arşiv seçeneklerini hazırla
                FragileOptions options = new()
                {
                    FormatCompatibility = format,
                    // Format için uygun sıkıştırma ve şifreleme seçenekleri ayarla
                    // (her format her özelliği desteklemeyebilir)
                    CompressionAlgorithm = GetSupportedCompressionForFormat(format),
                    EnableEncryption = false // Basitlik için şifreleme kapalı
                };

                // Format sağlayıcısını oluştur
                FormatProvider formatProvider = FormatProvider.Create(format);

                // Arşiv oluşturma işlemi
                using (FragileArchive archive = new(archivePath, FragileArchiveMode.Create))
                {
                    // Dosyaları arşive ekle
                    int count = archive.AddDirectory(sourceDir, "", true);

                    // Arşivi kaydet
                    archive.Save();

                    Console.WriteLine($"✅ Arşiv oluşturuldu: {count} dosya eklendi.");
                    Console.WriteLine($"Uzantı: {formatProvider.GetDefaultExtension()}");
                }

                // Arşiv bilgilerini göster
                FileInfo fileInfo = new(archivePath);
                Console.WriteLine($"Arşiv boyutu: {fileInfo.Length:N0} bayt");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Arşiv oluşturulamadı: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Arşiv formatını algılar
        /// </summary>
        private static async Task DetectArchiveFormat(string archivePath)
        {
            Console.WriteLine($"\n🔍 Format tespiti: {Path.GetFileName(archivePath)}");

            try
            {
                // Tüm formatlar için test et
                foreach (FormatCompatibility format in Enum.GetValues<FormatCompatibility>())
                {
                    try
                    {
                        FormatProvider formatProvider = FormatProvider.Create(format);
                        bool canRead = formatProvider.CanRead(archivePath);

                        Console.WriteLine($"- {format}: {(canRead ? "✅ Okunabilir" : "❌ Okunamaz")}");
                    }
                    catch (NotSupportedException)
                    {
                        Console.WriteLine($"- {format}: ⚠️ Desteklenmiyor");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Format tespiti başarısız: {ex.Message}");
            }
        }

        /// <summary>
        /// Arşivi çıkarır
        /// </summary>
        private static async Task ExtractArchive(string archivePath, string extractDir)
        {
            Console.WriteLine($"\n📤 Arşiv çıkarılıyor: {Path.GetFileName(archivePath)}");
            Console.WriteLine($"Hedef: {extractDir}");

            try
            {
                // Dizin oluştur
                Directory.CreateDirectory(extractDir);

                // Arşivi çıkar
                using FragileArchive archive = new(archivePath, FragileArchiveMode.Read);
                archive.ExtractAll(extractDir);

                Console.WriteLine($"✅ Arşiv çıkarıldı: {archive.Entries.Count} dosya");

                // Çıkarılan dosyaları listele
                Console.WriteLine("Çıkarılan dosyalar:");
                foreach (FragileArchiveEntry entry in archive.Entries)
                {
                    string entryType = entry.IsDirectory ? "📁" : "📄";
                    Console.WriteLine($"{entryType} {entry.Path} ({entry.Size:N0} bayt)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Arşiv çıkarılamadı: {ex.Message}");
            }
        }

        /// <summary>
        /// Bir arşivi başka bir formata dönüştürür
        /// </summary>
        private static async Task ConvertFormat(string inputPath, string outputPath, FormatCompatibility targetFormat)
        {
            Console.WriteLine($"\n🔄 Format dönüştürme: {Path.GetFileName(inputPath)} -> {Path.GetFileName(outputPath)}");
            Console.WriteLine($"Hedef format: {targetFormat}");

            try
            {
                // Format sağlayıcısı oluştur
                FormatProvider formatProvider = FormatProvider.Create(targetFormat);

                // Dönüştürme işlemi
                Progress<double> progress = new(value =>
                {
                    Console.Write($"\rDönüştürme: %{value * 100:F1}");
                });

                await formatProvider.ConvertAsync(inputPath, outputPath, null, progress);

                Console.WriteLine($"\r✅ Dönüştürme tamamlandı");

                // Dönüştürülmüş arşiv bilgilerini göster
                FileInfo fileInfo = new(outputPath);
                Console.WriteLine($"Dönüştürülmüş arşiv boyutu: {fileInfo.Length:N0} bayt");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Dönüştürme başarısız: {ex.Message}");
            }
        }

        /// <summary>
        /// Test için örnek dosyalar oluşturur
        /// </summary>
        private static async Task CreateSampleFiles(string directory)
        {
            Console.WriteLine($"Test dosyaları oluşturuluyor: {directory}");

            // Metin dosyası
            string textFilePath = Path.Combine(directory, "document.txt");
            await File.WriteAllTextAsync(textFilePath,
                "Bu bir test dosyasıdır.\n" +
                "Fragile format uyumluluğu testleri için kullanılmaktadır.\n" +
                "Her format, farklı dosya türlerini ve özellikleri destekleyebilir.");

            // Alt dizinler
            string subDir1 = Path.Combine(directory, "images");
            Directory.CreateDirectory(subDir1);

            string subDir2 = Path.Combine(directory, "documents");
            Directory.CreateDirectory(subDir2);

            // Alt dizinlere dosyalar
            // Not: Gerçek uygulamada burada resim dosyaları olacak
            string imagePlaceholderPath = Path.Combine(subDir1, "image1.dat");
            await CreateBinaryFile(imagePlaceholderPath, 50 * 1024); // 50 KB

            string docPlaceholderPath = Path.Combine(subDir2, "document1.dat");
            await CreateBinaryFile(docPlaceholderPath, 100 * 1024); // 100 KB

            Console.WriteLine("Test dosyaları oluşturuldu.");
        }

        /// <summary>
        /// Belirtilen boyutta rastgele ikili dosya oluşturur
        /// </summary>
        private static async Task CreateBinaryFile(string filePath, int sizeInBytes)
        {
            Random random = new();
            byte[] data = new byte[sizeInBytes];
            random.NextBytes(data);

            await File.WriteAllBytesAsync(filePath, data);
        }

        /// <summary>
        /// Format için desteklenen sıkıştırma algoritmasını döndürür
        /// </summary>
        private static CompressionAlgorithm GetSupportedCompressionForFormat(FormatCompatibility format)
        {
            return format switch
            {
                FormatCompatibility.Native => CompressionAlgorithm.Deflate,
                FormatCompatibility.Zip => CompressionAlgorithm.Deflate, // ZIP, Deflate kullanır
                FormatCompatibility.Tar => CompressionAlgorithm.Store,   // Temel TAR sıkıştırma kullanmaz
                FormatCompatibility.SevenZip => CompressionAlgorithm.LZMA, // 7z genellikle LZMA kullanır
                _ => CompressionAlgorithm.Store
            };
        }
    }
}
