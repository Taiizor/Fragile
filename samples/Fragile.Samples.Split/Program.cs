using Fragile.Compression;
using Fragile.Core;
using Fragile.Models;
using System.Diagnostics;
using System.Text;

namespace Fragile.Samples.Split
{
    /// <summary>
    /// Fragile arşiv bölme ve birleştirme özelliklerini gösteren örnek uygulama
    /// </summary>
    public class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Arşiv Bölme Örneği");
            Console.WriteLine("===========================");

            try
            {
                // Geçici dizin oluştur
                string tempDir = Path.Combine(Path.GetTempPath(), "FragileSplitSample");
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
                Directory.CreateDirectory(tempDir);

                // Büyük arşiv oluşturmak için test dosyaları
                string testFilesDir = Path.Combine(tempDir, "TestFiles");
                Directory.CreateDirectory(testFilesDir);

                // Test için büyük dosyalar oluştur
                await CreateLargeTestFiles(testFilesDir);

                // Arşiv bölme testi
                string archivePath = Path.Combine(tempDir, "large_archive.frgl");
                string extractDir = Path.Combine(tempDir, "Extracted");
                await TestArchiveSplitting(testFilesDir, archivePath, extractDir);
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
        /// Arşiv bölme ve birleştirme sürecini test eder
        /// </summary>
        private static async Task TestArchiveSplitting(string sourceDir, string archivePath, string extractDir)
        {
            Console.WriteLine("\n📦 ARŞIV BÖLME VE BIRLEŞTIRME TESTİ");
            Console.WriteLine("====================================");

            try
            {
                // 1. Önce büyük bir arşiv oluştur
                Console.WriteLine($"\n📝 Adım 1: Büyük arşiv oluşturuluyor...");
                await CreateLargeArchive(sourceDir, archivePath);

                // 2. Arşivi parçalara böl
                Console.WriteLine($"\n✂️ Adım 2: Arşiv parçalara bölünüyor...");
                FragileArchivePartCollection splitParts = await SplitArchive(archivePath);

                // 3. Bölünmüş parçaları birleştir
                Console.WriteLine($"\n🔄 Adım 3: Bölünmüş parçalar birleştiriliyor...");
                string combinedArchivePath = Path.Combine(
                    Path.GetDirectoryName(archivePath),
                    "combined_" + Path.GetFileName(archivePath));
                await CombineSplitParts(splitParts, combinedArchivePath);

                // 4. Birleştirilmiş arşivi çıkar
                Console.WriteLine($"\n📤 Adım 4: Birleştirilmiş arşiv çıkarılıyor...");
                await ExtractArchive(combinedArchivePath, extractDir);

                // 5. Orijinal dosyalarla karşılaştır
                Console.WriteLine($"\n🔍 Adım 5: Çıkarılan dosyalar doğrulanıyor...");
                await VerifyExtractedFiles(sourceDir, extractDir);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test başarısız: {ex.Message}");
            }
        }

        /// <summary>
        /// Kaynak dizindeki dosyalardan büyük bir arşiv oluşturur
        /// </summary>
        private static async Task CreateLargeArchive(string sourceDir, string archivePath)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            // Arşivleme seçenekleri
            FragileOptions options = new()
            {
                CompressionAlgorithm = CompressionAlgorithm.Deflate,
                CompressionLevel = CompressionLevel.Fast, // Hızlı olması için
                EnableChecksumVerification = true,
                UseParallelProcessing = true
            };

            // İlerleme raporlama
            Progress<double> progress = new(value =>
            {
                Console.Write($"\rArşiv oluşturuluyor: %{value * 100:F1}");
            });

            try
            {
                // FragileUtility gibi bir yardımcı sınıf olmadığı için doğrudan FragileArchive kullanıyoruz
                using FragileArchive archive = new(archivePath, FragileArchiveMode.Create);
                // Dizini arşive ekle
                int count = archive.AddDirectory(sourceDir, "", true);

                // Arşivi kaydet
                archive.Save();

                stopwatch.Stop();

                FileInfo fileInfo = new(archivePath);
                Console.WriteLine($"\rArşiv oluşturuldu: {count} dosya, {fileInfo.Length:N0} bayt ({stopwatch.ElapsedMilliseconds:N0} ms)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\r❌ Arşiv oluşturulamadı: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Arşivi belirli boyutlarda parçalara böler
        /// </summary>
        private static async Task<FragileArchivePartCollection> SplitArchive(string archivePath)
        {
            Console.WriteLine($"Arşiv bölünüyor: {Path.GetFileName(archivePath)}");

            try
            {
                // Arşiv dosyasının boyutunu al
                FileInfo fileInfo = new(archivePath);
                long totalSize = fileInfo.Length;

                // Test için arşivi küçük parçalara bölelim
                // Gerçek uygulamada daha büyük parça boyutları kullanılabilir (örn. 10 MB, 100 MB)
                long partSize = totalSize / 5; // Yaklaşık 5 parça
                if (partSize < 64 * 1024)
                {
                    partSize = 64 * 1024; // Min 64 KB
                }

                Console.WriteLine($"Toplam boyut: {totalSize:N0} bayt");
                Console.WriteLine($"Parça boyutu: {partSize:N0} bayt");

                // Bu örnekte, FragileArchive sınıfının doğrudan bölme yapabildiğini varsayıyoruz
                // Gerçek uygulamada bu işlem daha karmaşık olabilir ve özel bir API gerektirebilir

                // Dosyayı parçalara bölmeyi simüle et (gerçek bir API olmadığından)
                // Bu kısım normalde kütüphane API'si tarafından yönetilir
                byte[] fullArchiveData = await File.ReadAllBytesAsync(archivePath);

                int totalParts = (int)Math.Ceiling((double)totalSize / partSize);
                Console.WriteLine($"Toplam parça sayısı: {totalParts}");

                FragileArchivePartCollection partCollection = new();

                for (int i = 0; i < totalParts; i++)
                {
                    int partIndex = i + 1;
                    long startOffset = i * partSize;
                    long length = Math.Min(partSize, totalSize - startOffset);

                    string partFileName = FragileArchivePart.GetPartFileName(archivePath, partIndex, totalParts);

                    // Parça verilerini oluştur
                    byte[] partData = new byte[length];
                    Array.Copy(fullArchiveData, startOffset, partData, 0, length);

                    // Parça dosyasını yaz
                    await File.WriteAllBytesAsync(partFileName, partData);

                    // Parça nesnesini oluştur
                    FragileArchivePart part = new()
                    {
                        PartIndex = partIndex,
                        TotalParts = totalParts,
                        Path = partFileName,
                        Size = length,
                        Offset = startOffset
                    };

                    // Koleksiyona ekle
                    partCollection.Add(part);

                    Console.WriteLine($"Parça {partIndex}/{totalParts} oluşturuldu: {Path.GetFileName(partFileName)} ({length:N0} bayt)");
                }

                return partCollection;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Arşiv bölünemedi: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Bölünmüş parçaları birleştirerek tam arşivi oluşturur
        /// </summary>
        private static async Task CombineSplitParts(FragileArchivePartCollection parts, string outputPath)
        {
            Console.WriteLine($"Parçalar birleştiriliyor: {parts.Count} parça -> {Path.GetFileName(outputPath)}");

            try
            {
                // İlerleme raporlama
                Progress<double> progress = new(value =>
                {
                    Console.Write($"\rBirleştirme: %{value * 100:F1}");
                });

                // Parçaları birleştir
                await parts.CombinePartsAsync(outputPath, progress);

                FileInfo fileInfo = new(outputPath);
                Console.WriteLine($"\rParçalar birleştirildi: {fileInfo.Length:N0} bayt");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\r❌ Parçalar birleştirilemedi: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Arşivi belirtilen dizine çıkarır
        /// </summary>
        private static async Task ExtractArchive(string archivePath, string extractDir)
        {
            Console.WriteLine($"Arşiv çıkarılıyor: {Path.GetFileName(archivePath)} -> {extractDir}");

            try
            {
                // Çıkarma dizinini temizle ve oluştur
                if (Directory.Exists(extractDir))
                {
                    Directory.Delete(extractDir, true);
                }
                Directory.CreateDirectory(extractDir);

                // Arşivi çıkar
                using FragileArchive archive = new(archivePath, FragileArchiveMode.Read);
                archive.ExtractAll(extractDir);

                Console.WriteLine($"Arşiv çıkarıldı: {archive.Entries.Count} dosya");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Arşiv çıkarılamadı: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Çıkarılan dosyaların orijinal dosyalarla aynı olduğunu doğrular
        /// </summary>
        private static async Task VerifyExtractedFiles(string originalDir, string extractedDir)
        {
            Console.WriteLine("Dosya doğrulaması başlıyor...");

            try
            {
                bool allValid = true;
                int verifiedCount = 0;

                // Orijinal dizindeki tüm dosyaları kontrol et
                foreach (string origFilePath in Directory.GetFiles(originalDir, "*", SearchOption.AllDirectories))
                {
                    // Çıkarılan dizindeki karşılık gelen dosya yolunu hesapla
                    string relativePath = Path.GetRelativePath(originalDir, origFilePath);
                    string extractedFilePath = Path.Combine(extractedDir, relativePath);

                    // Dosya var mı?
                    if (!File.Exists(extractedFilePath))
                    {
                        Console.WriteLine($"❌ Eksik dosya: {relativePath}");
                        allValid = false;
                        continue;
                    }

                    // Dosya içerikleri eşit mi?
                    byte[] origContent = await File.ReadAllBytesAsync(origFilePath);
                    byte[] extractedContent = await File.ReadAllBytesAsync(extractedFilePath);

                    if (origContent.Length != extractedContent.Length)
                    {
                        Console.WriteLine($"❌ Boyut uyuşmazlığı: {relativePath} ({origContent.Length} vs {extractedContent.Length})");
                        allValid = false;
                        continue;
                    }

                    bool contentEqual = true;
                    for (int i = 0; i < origContent.Length; i++)
                    {
                        if (origContent[i] != extractedContent[i])
                        {
                            contentEqual = false;
                            break;
                        }
                    }

                    if (!contentEqual)
                    {
                        Console.WriteLine($"❌ İçerik uyuşmazlığı: {relativePath}");
                        allValid = false;
                        continue;
                    }

                    verifiedCount++;
                }

                if (allValid)
                {
                    Console.WriteLine($"✅ Tüm dosyalar doğrulandı: {verifiedCount} dosya kontrol edildi.");
                }
                else
                {
                    Console.WriteLine($"⚠️ Bazı dosyalar doğrulanamadı.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Doğrulama hatası: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Test için büyük dosyalar oluşturur
        /// </summary>
        private static async Task CreateLargeTestFiles(string directory)
        {
            Console.WriteLine("Test dosyaları oluşturuluyor...");

            try
            {
                // Toplam 5-10 MB civarında veri olacak şekilde ayarla
                // Not: Gerçekte çok daha büyük olabilir, bu sadece test amaçlı

                // 1 MB metin dosyası
                string textFilePath = Path.Combine(directory, "large_text.txt");
                await CreateLargeTextFile(textFilePath, 1 * 1024 * 1024);

                // Alt dizinler
                string imagesDir = Path.Combine(directory, "images");
                Directory.CreateDirectory(imagesDir);

                string docsDir = Path.Combine(directory, "documents");
                Directory.CreateDirectory(docsDir);

                // Çeşitli boyutlarda binary dosyalar
                await CreateRandomFile(Path.Combine(imagesDir, "image1.dat"), 500 * 1024);    // 500 KB
                await CreateRandomFile(Path.Combine(imagesDir, "image2.dat"), 1500 * 1024);   // 1.5 MB
                await CreateRandomFile(Path.Combine(docsDir, "document.dat"), 1024 * 1024);   // 1 MB
                await CreateRandomFile(Path.Combine(docsDir, "spreadsheet.dat"), 2 * 1024 * 1024); // 2 MB

                // Dosya bilgilerini göster
                long totalSize = 0;
                int fileCount = 0;

                foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                {
                    FileInfo fileInfo = new(file);
                    totalSize += fileInfo.Length;
                    fileCount++;

                    Console.WriteLine($"- {fileInfo.Name}: {fileInfo.Length:N0} bayt");
                }

                Console.WriteLine($"Toplam {fileCount} dosya oluşturuldu ({totalSize:N0} bayt)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test dosyaları oluşturulamadı: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Belirtilen boyutta rastgele veri içeren bir dosya oluşturur
        /// </summary>
        private static async Task CreateRandomFile(string filePath, int sizeInBytes)
        {
            Random random = new();
            byte[] buffer = new byte[Math.Min(sizeInBytes, 64 * 1024)]; // Max 64 KB buffer

            using FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write);
            int remaining = sizeInBytes;

            while (remaining > 0)
            {
                int chunkSize = Math.Min(remaining, buffer.Length);
                random.NextBytes(buffer);
                await fileStream.WriteAsync(buffer, 0, chunkSize);
                remaining -= chunkSize;
            }
        }

        /// <summary>
        /// Belirtilen boyutta rastgele metin içeren bir dosya oluşturur
        /// </summary>
        private static async Task CreateLargeTextFile(string filePath, int sizeInBytes)
        {
            using FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write);
            using StreamWriter writer = new(fileStream);
            int bytesWritten = 0;
            Random random = new();

            // Rastgele paragraflar oluştur
            while (bytesWritten < sizeInBytes)
            {
                // Rastgele bir paragraf oluştur
                string paragraph = GenerateRandomParagraph(random, 100, 200); // 100-200 kelimelik paragraf
                await writer.WriteLineAsync(paragraph);
                await writer.WriteLineAsync(); // Boş satır ekle

                // Yaklaşık yazılan byte sayısını hesapla (UTF-8'de her karakter 1-4 byte)
                bytesWritten += paragraph.Length + 2; // +2 satır sonu karakterleri için
            }
        }

        /// <summary>
        /// Belirtilen uzunlukta rastgele bir paragraf oluşturur
        /// </summary>
        private static string GenerateRandomParagraph(Random random, int minWords, int maxWords)
        {
            string[] words = {
                "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing", "elit",
                "sed", "do", "eiusmod", "tempor", "incididunt", "ut", "labore", "et", "dolore",
                "magna", "aliqua", "enim", "ad", "minim", "veniam", "quis", "nostrud", "exercitation",
                "ullamco", "laboris", "nisi", "aliquip", "ex", "commodo", "consequat", "duis",
                "aute", "irure", "reprehenderit", "voluptate", "velit", "esse", "cillum",
                "fugiat", "nulla", "pariatur", "excepteur", "sint", "occaecat", "cupidatat",
                "non", "proident", "sunt", "culpa", "qui", "officia", "deserunt", "mollit",
                "anim", "id", "est", "laborum", "fragile", "archive", "split", "test"
            };

            int wordCount = random.Next(minWords, maxWords + 1);
            StringBuilder result = new();

            for (int i = 0; i < wordCount; i++)
            {
                if (i > 0)
                {
                    result.Append(" ");
                }

                string word = words[random.Next(words.Length)];

                // İlk kelime ve yaklaşık her 10. kelimeden sonra nokta ekleyerek cümle oluştur
                if (i == 0 || (i > 0 && result[^1] == '.'))
                {
                    // Büyük harfle başla
                    word = char.ToUpper(word[0]) + word[1..];
                }

                result.Append(word);

                // Yaklaşık her 10. kelimeden sonra nokta ekle
                if (random.Next(10) == 0 && i < wordCount - 1)
                {
                    result.Append(".");
                }
            }

            // Paragraf sonuna nokta ekle
            if (result[^1] != '.')
            {
                result.Append(".");
            }

            return result.ToString();
        }
    }
}
