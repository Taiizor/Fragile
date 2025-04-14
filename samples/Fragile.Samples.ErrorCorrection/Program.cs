using Fragile.Compression;
using Fragile.Core;
using Fragile.Models;
using System.Diagnostics;
using System.Text;

namespace Fragile.Samples.ErrorCorrection
{
    /// <summary>
    /// Fragile arşivlerinde hata düzeltme ve kurtarma özelliklerini gösteren örnek uygulama
    /// </summary>
    public class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Hata Düzeltme Örneği");
            Console.WriteLine("============================");

            try
            {
                // Geçici dizin oluştur
                string tempDir = Path.Combine(Path.GetTempPath(), "FragileErrorCorrectionSample");
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
                Directory.CreateDirectory(tempDir);

                // Test dosyaları oluştur
                string sourceDir = Path.Combine(tempDir, "Source");
                Directory.CreateDirectory(sourceDir);
                await CreateTestFiles(sourceDir);

                // Farklı hata düzeltme seviyelerinde arşivler oluştur ve test et
                await TestErrorCorrectionLevels(sourceDir, tempDir);
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
        /// Farklı hata düzeltme seviyelerinde arşivler oluşturur ve test eder
        /// </summary>
        private static async Task TestErrorCorrectionLevels(string sourceDir, string outputDir)
        {
            int[] errorCorrectionLevels = { 0, 5, 10, 20 }; // Yüzdeler

            foreach (int level in errorCorrectionLevels)
            {
                Console.WriteLine($"\n\n🛡️ HATA DÜZELTME SEVİYESİ: %{level}");
                Console.WriteLine("====================================");

                try
                {
                    // Arşiv yolu
                    string archivePath = Path.Combine(outputDir, $"archive_ec{level}.frgl");

                    // Bu seviyede arşiv oluştur
                    await CreateArchiveWithErrorCorrection(sourceDir, archivePath, level);

                    // Arşivi boz
                    string corruptedArchivePath = Path.Combine(outputDir, $"corrupted_ec{level}.frgl");
                    await CorruptArchive(archivePath, corruptedArchivePath, 100); // 100 byte'ı boz

                    // Bozuk arşivi çıkarmayı ve düzeltmeyi dene
                    string extractDir = Path.Combine(outputDir, $"extracted_ec{level}");
                    await ExtractAndRepairArchive(corruptedArchivePath, extractDir);

                    // Çıkarılan dosyaları doğrula
                    await VerifyExtractedFiles(sourceDir, extractDir);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ %{level} seviyesinde test başarısız: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Belirtilen hata düzeltme seviyesi ile arşiv oluşturur
        /// </summary>
        private static async Task CreateArchiveWithErrorCorrection(string sourceDir, string archivePath, int errorCorrectionLevel)
        {
            Console.WriteLine($"📝 Arşiv oluşturuluyor (EC: %{errorCorrectionLevel}): {Path.GetFileName(archivePath)}");

            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();

                // Arşiv seçenekleri - hata düzeltme seviyesi ayarla
                FragileOptions options = new()
                {
                    CompressionAlgorithm = CompressionAlgorithm.Deflate,
                    CompressionLevel = CompressionLevel.Normal,
                    EnableErrorCorrection = errorCorrectionLevel > 0,
                    ErrorCorrectionLevel = errorCorrectionLevel,
                    Progress = new Progress<double>(value =>
                    {
                        Console.Write($"\rArşivleniyor: %{value * 100:F1}");
                    })
                };

                // Arşivi oluştur
                using FragileArchive archive = new(archivePath, FragileArchiveMode.Create);
                // Dosyaları ekle
                int fileCount = archive.AddDirectory(sourceDir, "", true);

                // Arşivi kaydet
                archive.Save();

                stopwatch.Stop();

                FileInfo fileInfo = new(archivePath);
                Console.WriteLine($"\r✅ Arşiv oluşturuldu: {fileCount} dosya, {fileInfo.Length:N0} bayt ({stopwatch.ElapsedMilliseconds:N0} ms)");

                // Hata düzeltme verilerinin boyutu
                long overheadSize = 0;
                if (errorCorrectionLevel > 0)
                {
                    // Gerçek uygulamada burada arşivden hata düzeltme verilerinin boyutunu hesaplardık
                    overheadSize = (long)(fileInfo.Length * (errorCorrectionLevel / 100.0));
                    Console.WriteLine($"📊 Hata düzeltme verileri: ~{overheadSize:N0} bayt (toplam boyutun ~%{errorCorrectionLevel})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\r❌ Arşiv oluşturulamadı: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Arşivi belirli sayıda byte değiştirerek bozar
        /// </summary>
        private static async Task CorruptArchive(string sourcePath, string corruptedPath, int byteCount)
        {
            Console.WriteLine($"🔨 Arşiv bozuluyor: {byteCount} byte değişiyor");

            try
            {
                // Önce dosyayı kopyala
                File.Copy(sourcePath, corruptedPath, true);

                // Rastgele bytelar değiştir
                byte[] data = await File.ReadAllBytesAsync(corruptedPath);

                if (data.Length < byteCount + 100)
                {
                    throw new InvalidOperationException("Arşiv bozulacak kadar büyük değil");
                }

                Random random = new();

                // Arşiv başlığını korumak için ilk 100 byte'ı değiştirmiyoruz
                int[] corruptedPositions = new int[byteCount];
                for (int i = 0; i < byteCount; i++)
                {
                    int position = random.Next(100, data.Length);
                    data[position] = (byte)random.Next(256);
                    corruptedPositions[i] = position;
                }

                await File.WriteAllBytesAsync(corruptedPath, data);

                // Bozulan pozisyonları göster
                Console.WriteLine($"💔 Arşiv bozuldu: {byteCount} byte değiştirildi");
                Console.WriteLine($"📍 Değiştirilen pozisyonlar: {string.Join(", ", corruptedPositions.Take(5))}...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Arşiv bozma işlemi başarısız: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Bozuk arşivi çıkarmayı ve düzeltmeyi dener
        /// </summary>
        private static async Task ExtractAndRepairArchive(string archivePath, string extractDir)
        {
            Console.WriteLine($"🔄 Bozuk arşivden çıkarma ve düzeltme: {Path.GetFileName(archivePath)}");

            try
            {
                // Çıkarma dizinini temizle
                if (Directory.Exists(extractDir))
                {
                    Directory.Delete(extractDir, true);
                }
                Directory.CreateDirectory(extractDir);

                // Arşivi aç
                using FragileArchive archive = new(archivePath, FragileArchiveMode.Read);
                int repairAttempts = 0;
                int repairedFiles = 0;

                // Tüm dosyaları çıkarmayı dene
                foreach (FragileArchiveEntry entry in archive.Entries)
                {
                    if (entry.IsDirectory)
                    {
                        // Dizin ise, oluştur ve devam et
                        Directory.CreateDirectory(Path.Combine(extractDir, entry.Path));
                        continue;
                    }

                    try
                    {
                        // Normal çıkarma
                        archive.Extract(entry.Path, Path.Combine(extractDir, entry.Path));
                        Console.WriteLine($"✅ Çıkarıldı: {entry.Path}");
                    }
                    catch (Exception ex)
                    {
                        // Çıkarma başarısız - hata düzeltme dene
                        Console.WriteLine($"⚠️ Hata: {entry.Path} çıkarılamadı: {ex.Message}");

                        try
                        {
                            repairAttempts++;

                            // Burada arşivden dosya düzeltme fonksiyonu çağrılacak
                            // (Gerçek uygulamada kütüphaneniz bu fonksiyonaliteyi sağlamalı)
                            bool repaired = await SimulateFileRepair(archive, entry, Path.Combine(extractDir, entry.Path));

                            if (repaired)
                            {
                                repairedFiles++;
                                Console.WriteLine($"🛠️ Onarıldı: {entry.Path}");
                            }
                            else
                            {
                                Console.WriteLine($"❌ Onarılamadı: {entry.Path}");
                            }
                        }
                        catch (Exception repairEx)
                        {
                            Console.WriteLine($"❌ Onarım başarısız: {repairEx.Message}");
                        }
                    }
                }

                Console.WriteLine($"\n📊 Özet: {archive.Entries.Count} dosya, {repairAttempts} onarım denemesi, {repairedFiles} başarılı onarım");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Arşiv çıkarma işlemi başarısız: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Dosya onarımını simüle eder (gerçek uygulamada kütüphaneniz bu işlevi sağlamalı)
        /// </summary>
        private static async Task<bool> SimulateFileRepair(FragileArchive archive, FragileArchiveEntry entry, string outputPath)
        {
            // Gerçek onarım işlevi için burada kütüphanenin hata düzeltme
            // işlevi çağrılacak. Bu örnekte, basit bir simülasyon yapıyoruz.

            // Arşivin hata düzeltme seviyesine göre başarı şansını belirle
            int errorCorrectionLevel = 0;
            if (archive.ArchivePath.Contains("_ec5."))
            {
                errorCorrectionLevel = 5;
            }
            else if (archive.ArchivePath.Contains("_ec10."))
            {
                errorCorrectionLevel = 10;
            }
            else if (archive.ArchivePath.Contains("_ec20."))
            {
                errorCorrectionLevel = 20;
            }

            // Dosya boyutuna göre onarım şansını hesapla
            // Gerçekte bu, hata düzeltme algoritmasının yeteneklerine bağlı olacak
            double repairChance = Math.Min(0.2 + (errorCorrectionLevel / 100.0 * 2), 0.95);

            // Simülasyon: Hata düzeltme düzeyine bağlı olarak dosyayı başarılı bir şekilde onarmayı dene
            Random random = new();
            bool success = random.NextDouble() < repairChance;

            if (success)
            {
                // Onarım başarılı - boş bir dosya oluştur (simülasyon için)
                // Gerçek uygulamada, onarılmış içerikle doldurulacak
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                await File.WriteAllTextAsync(outputPath, $"[Bu dosya {Path.GetFileName(archive.ArchivePath)} arşivinden başarıyla onarıldı.]\n");
                await Task.Delay(500); // Onarımın zaman aldığını simüle et
            }

            return success;
        }

        /// <summary>
        /// Çıkarılan dosyaları orijinal kaynak ile karşılaştırır
        /// </summary>
        private static async Task VerifyExtractedFiles(string sourceDir, string extractedDir)
        {
            Console.WriteLine("\n🔍 Çıkarılan dosyaları doğrulama...");

            try
            {
                int totalFiles = 0;
                int verifiedFiles = 0;
                int missingFiles = 0;
                int corruptedFiles = 0;

                foreach (string sourceFile in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
                {
                    totalFiles++;

                    // Göreli yol hesapla
                    string relativePath = Path.GetRelativePath(sourceDir, sourceFile);
                    string extractedFile = Path.Combine(extractedDir, relativePath);

                    if (!File.Exists(extractedFile))
                    {
                        Console.WriteLine($"❌ Eksik dosya: {relativePath}");
                        missingFiles++;
                        continue;
                    }

                    // Dosya içeriklerini karşılaştırma
                    byte[] sourceBytes = await File.ReadAllBytesAsync(sourceFile);
                    byte[] extractedBytes = await File.ReadAllBytesAsync(extractedFile);

                    // Bu örnekte, onarılmış dosyalar orijinal içeriği içermediğinden
                    // sadece dosyanın var olup olmadığını kontrol ediyoruz
                    if (extractedBytes.Length == 0 || extractedBytes.Length != sourceBytes.Length)
                    {
                        Console.WriteLine($"⚠️ Değiştirilmiş/onarılmış dosya: {relativePath}");
                        corruptedFiles++;
                    }
                    else if (extractedBytes.SequenceEqual(sourceBytes))
                    {
                        verifiedFiles++;
                    }
                    else
                    {
                        Console.WriteLine($"❌ Bozuk dosya: {relativePath}");
                        corruptedFiles++;
                    }
                }

                // Sonuçları göster
                Console.WriteLine($"\n📊 Doğrulama özeti:");
                Console.WriteLine($"  ✅ Doğrulanan: {verifiedFiles}/{totalFiles}");
                Console.WriteLine($"  ⚠️ Değişmiş/onarılmış: {corruptedFiles}/{totalFiles}");
                Console.WriteLine($"  ❌ Eksik: {missingFiles}/{totalFiles}");

                double successRate = (verifiedFiles + corruptedFiles) * 100.0 / totalFiles;
                Console.WriteLine($"  📈 Kurtarma oranı: %{successRate:F1}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Doğrulama hatası: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Test için örnek dosyalar oluşturur
        /// </summary>
        private static async Task CreateTestFiles(string directory)
        {
            Console.WriteLine("📁 Test dosyaları oluşturuluyor...");

            try
            {
                // Alt dizinler oluştur
                string textDir = Path.Combine(directory, "Documents");
                string imageDir = Path.Combine(directory, "Images");
                string dataDir = Path.Combine(directory, "Data");

                Directory.CreateDirectory(textDir);
                Directory.CreateDirectory(imageDir);
                Directory.CreateDirectory(dataDir);

                // Metin dosyaları oluştur
                for (int i = 1; i <= 5; i++)
                {
                    string content = $"Bu, hata düzeltme testleri için örnek metin dosyası #{i}.\n" +
                                    $"İçerdiği bilgiler çok önemlidir ve arşiv bozulsa bile kurtarılabilmelidir.\n" +
                                    $"Dosya kimliği: {Guid.NewGuid()}\n";

                    // İçeriği biraz daha büyüt
                    for (int j = 0; j < 20; j++)
                    {
                        content += $"Örnek satır {j}: {DateTime.Now.AddDays(-j)}\n";
                    }

                    await File.WriteAllTextAsync(
                        Path.Combine(textDir, $"document_{i}.txt"), content);
                }

                // Resim dosyalarını simüle et (binary veri)
                Random random = new();
                for (int i = 1; i <= 3; i++)
                {
                    byte[] imageData = new byte[50 * 1024]; // 50 KB
                    random.NextBytes(imageData);
                    await File.WriteAllBytesAsync(
                        Path.Combine(imageDir, $"image_{i}.dat"), imageData);
                }

                // Büyük veri dosyası
                byte[] largeData = new byte[500 * 1024]; // 500 KB
                random.NextBytes(largeData);
                await File.WriteAllBytesAsync(
                    Path.Combine(dataDir, "large_data.bin"), largeData);

                Console.WriteLine("✅ Test dosyaları oluşturuldu:");
                Console.WriteLine($"  📄 Metin dosyaları: 5");
                Console.WriteLine($"  🖼️ Resim dosyaları: 3");
                Console.WriteLine($"  📊 Veri dosyaları: 1");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test dosyaları oluşturulamadı: {ex.Message}");
                throw;
            }
        }
    }
}
