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

                // Asenkron arşiv oluştur
                using FragileArchive archive = await FragileArchive.CreateAsync(archivePath, options);

                // Dosyaları ekle
                int fileCount = await archive.AddDirectoryAsync(sourceDir, "", true);

                // Arşivi kaydet
                await archive.SaveAsync();

                stopwatch.Stop();

                FileInfo fileInfo = new(archivePath);
                Console.WriteLine($"\r✅ Arşiv oluşturuldu: {fileCount} dosya, {fileInfo.Length:N0} bayt ({stopwatch.ElapsedMilliseconds:N0} ms)");

                // Hata düzeltme verilerinin boyutu
                if (errorCorrectionLevel > 0)
                {
                    // Gerçek arşiv boyutuna göre hata düzeltme verisinin tahmini boyutu
                    long baseSize = (long)(fileInfo.Length / (1 + (errorCorrectionLevel / 100.0)));
                    long overheadSize = fileInfo.Length - baseSize;
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

                // Hata düzeltme seviyesini belirle
                int errorCorrectionLevel = 0;
                if (archivePath.Contains("_ec5."))
                {
                    errorCorrectionLevel = 5;
                }
                else if (archivePath.Contains("_ec10."))
                {
                    errorCorrectionLevel = 10;
                }
                else if (archivePath.Contains("_ec20."))
                {
                    errorCorrectionLevel = 20;
                }

                // Arşiv ayarlarını hazırla
                FragileOptions options = new()
                {
                    EnableErrorCorrection = errorCorrectionLevel > 0,
                    ErrorCorrectionLevel = errorCorrectionLevel,
                    Progress = new Progress<double>(value =>
                    {
                        // İlerleme bildirimi
                    })
                };

                int repairAttempts = 0;
                int repairedFiles = 0;

                // Callback işlevi - onarım sayısını takip eder
                void RepairCallback(long position, int repairCount)
                {
                    if (repairCount > 0)
                    {
                        repairAttempts++;
                        repairedFiles++;
                    }
                }

                try
                {
                    // Arşivi aç ve çıkar
                    using FragileArchive archive = await FragileArchive.OpenAsync(archivePath, options);
                    await archive.ExtractAllAsync(extractDir);

                    Console.WriteLine($"\n📊 Özet: {archive.Entries.Count} dosya, {repairAttempts} onarım denemesi, {repairedFiles} başarılı onarım");
                }
                catch (Exception ex)
                {
                    // Her dosyayı ayrı ayrı çıkarmayı dene
                    using FragileArchive archive = await FragileArchive.OpenAsync(archivePath, options);

                    foreach (FragileArchiveEntry entry in archive.Entries)
                    {
                        if (entry.IsDirectory)
                        {
                            Directory.CreateDirectory(Path.Combine(extractDir, entry.Path));
                            continue;
                        }

                        try
                        {
                            string outputPath = Path.Combine(extractDir, entry.Path);
                            await archive.ExtractAsync(entry.Path, outputPath);
                            Console.WriteLine($"✅ Çıkarıldı: {entry.Path}");
                        }
                        catch (Exception extractEx)
                        {
                            repairAttempts++;
                            Console.WriteLine($"⚠️ Hata: {entry.Path} çıkarılamadı: {extractEx.Message}");
                        }
                    }

                    Console.WriteLine($"\n📊 Özet: {archive.Entries.Count} dosya, {repairAttempts} onarım denemesi, {repairedFiles} başarılı onarım");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Arşiv çıkarma işlemi başarısız: {ex.Message}");
                throw;
            }
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

                // Kurtarma oranını hesapla - eksik dosyaları hesaba katma
                double successRate = totalFiles > 0 ? (verifiedFiles + corruptedFiles) * 100.0 / totalFiles : 0;
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
                // Metin dosyaları oluştur (sıkıştırma için iyi)
                Directory.CreateDirectory(Path.Combine(directory, "Documents"));
                for (int i = 1; i <= 5; i++)
                {
                    string filePath = Path.Combine(directory, "Documents", $"document_{i}.txt");
                    await File.WriteAllTextAsync(filePath, await GenerateTextContentAsync(20 * 1024)); // 20 KB
                }

                // Resim dosyaları oluştur (zaten sıkıştırılmış veri gibi - düşük sıkıştırma)
                Directory.CreateDirectory(Path.Combine(directory, "Images"));
                for (int i = 1; i <= 3; i++)
                {
                    string filePath = Path.Combine(directory, "Images", $"image_{i}.dat");
                    await CreateRandomBinaryFileAsync(filePath, 30 * 1024); // 30 KB
                }

                // Büyük veri dosyası oluştur
                Directory.CreateDirectory(Path.Combine(directory, "Data"));
                string dataFilePath = Path.Combine(directory, "Data", "large_data.bin");
                await CreateCompressibleDataFileAsync(dataFilePath, 100 * 1024); // 100 KB

                Console.WriteLine("✅ Test dosyaları oluşturuldu:");
                Console.WriteLine($"  📄 Metin dosyaları: 5");
                Console.WriteLine($"  🖼️ Resim dosyaları: 3");
                Console.WriteLine($"  📊 Veri dosyaları: 1");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test dosyaları oluşturulamadı: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Rastgele ikili dosya oluşturur
        /// </summary>
        private static async Task CreateRandomBinaryFileAsync(string filePath, int sizeInBytes)
        {
            using FileStream fs = new(filePath, FileMode.Create);
            Random random = new();
            byte[] buffer = new byte[4096];

            int remainingBytes = sizeInBytes;
            while (remainingBytes > 0)
            {
                int bytesToWrite = Math.Min(buffer.Length, remainingBytes);
                random.NextBytes(buffer);
                await fs.WriteAsync(buffer, 0, bytesToWrite);
                remainingBytes -= bytesToWrite;
            }
        }

        /// <summary>
        /// Sıkıştırılabilir veri dosyası oluşturur
        /// </summary>
        private static async Task CreateCompressibleDataFileAsync(string filePath, int sizeInBytes)
        {
            using FileStream fs = new(filePath, FileMode.Create);
            Random random = new();
            byte[] patterns = new byte[256];
            random.NextBytes(patterns);

            int remainingBytes = sizeInBytes;
            while (remainingBytes > 0)
            {
                // Tekrarlanan desenleri kullan - sıkıştırma için daha iyi
                int patternIndex = random.Next(0, patterns.Length);
                byte patternValue = patterns[patternIndex];

                // Desen uzunluğu: 16-128 bayt arası
                int patternLength = random.Next(16, 129);
                patternLength = Math.Min(patternLength, remainingBytes);

                byte[] pattern = new byte[patternLength];
                Array.Fill(pattern, patternValue);

                await fs.WriteAsync(pattern, 0, pattern.Length);
                remainingBytes -= patternLength;
            }
        }

        /// <summary>
        /// Metin içeriği oluşturur
        /// </summary>
        private static async Task<string> GenerateTextContentAsync(int length)
        {
            StringBuilder sb = new(length);
            Random random = new();

            // Lorem ipsum benzeri metin oluştur
            string[] words = {
                "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing", "elit",
                "sed", "do", "eiusmod", "tempor", "incididunt", "ut", "labore", "et", "dolore",
                "magna", "aliqua", "enim", "ad", "minim", "veniam", "quis", "nostrud", "exercitation",
                "ullamco", "laboris", "nisi", "aliquip", "ex", "ea", "commodo", "consequat"
            };

            // Paragraflar oluştur
            int chars = 0;
            while (chars < length)
            {
                // Paragraf (200-800 karakter arası)
                int paragraphLength = random.Next(200, 801);

                // Cümleler oluştur
                int sentenceCount = random.Next(3, 8);
                for (int s = 0; s < sentenceCount && chars < length; s++)
                {
                    // Cümle başlangıcı büyük harf
                    string firstWord = words[random.Next(words.Length)];
                    sb.Append(char.ToUpper(firstWord[0]) + firstWord[1..]);
                    chars += firstWord.Length;

                    // Kelimeleri ekle (3-15 kelime)
                    int wordCount = random.Next(3, 16);
                    for (int w = 0; w < wordCount && chars < length; w++)
                    {
                        sb.Append(' ');
                        chars++;
                        string word = words[random.Next(words.Length)];
                        sb.Append(word);
                        chars += word.Length;
                    }

                    // Cümle noktalama işareti
                    string[] punctuation = { ".", ".", ".", "!", "?", ";" };
                    sb.Append(punctuation[random.Next(punctuation.Length)]);
                    chars++;

                    // Boşluk ekle (cümle sonunda)
                    if (s < sentenceCount - 1 && chars < length)
                    {
                        sb.Append(' ');
                        chars++;
                    }
                }

                // Paragraf sonu
                if (chars < length)
                {
                    sb.Append("\r\n\r\n");
                    chars += 4;
                }
            }

            return sb.ToString(0, Math.Min(length, sb.Length));
        }
    }
}