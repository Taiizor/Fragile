using Fragile.Compression;
using Fragile.Core;
using Fragile.Models;
using System.Diagnostics;
using System.Text;

namespace Fragile.Samples.StreamingAPI
{
    /// <summary>
    /// Fragile kütüphanesinin akış tabanlı API kullanımını gösteren örnek uygulama
    /// </summary>
    public class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Streaming API Örneği");
            Console.WriteLine("============================");

            try
            {
                // Geçici dizin oluştur
                string tempDir = Path.Combine(Path.GetTempPath(), "FragileStreamingSample");
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
                Directory.CreateDirectory(tempDir);

                // Test dosyaları dizini
                string sourceDir = Path.Combine(tempDir, "Source");
                Directory.CreateDirectory(sourceDir);

                // Büyük test dosyası oluştur
                string largeFilePath = Path.Combine(sourceDir, "large_file.dat");
                await CreateLargeTestFile(largeFilePath, 100 * 1024 * 1024); // 100 MB

                // Akış tabanlı API ile büyük dosya işleme testleri
                await RunStreamingTests(sourceDir, tempDir);
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
        /// Akış tabanlı API testlerini çalıştırır
        /// </summary>
        private static async Task RunStreamingTests(string sourceDir, string outputDir)
        {
            Console.WriteLine("\n🔄 Akış Tabanlı API Testleri");
            Console.WriteLine("==============================");

            // Test edilen senaryolar
            await TestStreamCompression(Path.Combine(sourceDir, "large_file.dat"), outputDir);
            await TestChunkedProcessing(Path.Combine(sourceDir, "large_file.dat"), outputDir);
            await TestStreamingArchive(sourceDir, Path.Combine(outputDir, "streaming_archive.frgl"));
            await TestProgressReporting(sourceDir, Path.Combine(outputDir, "progress_archive.frgl"));
            await TestLimitedMemory(sourceDir, Path.Combine(outputDir, "limited_memory.frgl"));
        }

        /// <summary>
        /// Akış tabanlı sıkıştırma ve açma işlemlerini test eder
        /// </summary>
        private static async Task TestStreamCompression(string inputFilePath, string outputDir)
        {
            Console.WriteLine("\n📦 Test 1: Akış Tabanlı Sıkıştırma ve Açma");
            Console.WriteLine("----------------------------------------");

            try
            {
                FileInfo inputFile = new(inputFilePath);
                long inputSize = inputFile.Length;

                Console.WriteLine($"Kaynak dosya: {Path.GetFileName(inputFilePath)} ({inputSize:N0} bayt)");

                string compressedFilePath = Path.Combine(outputDir, "compressed_stream.bin");
                string decompressedFilePath = Path.Combine(outputDir, "decompressed_stream.dat");

                Stopwatch totalTimer = Stopwatch.StartNew();

                // Sıkıştırma sağlayıcısı - Store algoritması kullan (sıkıştırma yapma)
                CompressionProvider provider = CompressionProvider.Create(
                    CompressionAlgorithm.Store, CompressionLevel.Normal);

                // SIKIŞTIRMA
                Console.WriteLine("\nSıkıştırma işlemi başlıyor...");
                Stopwatch compressTimer = Stopwatch.StartNew();

                // Akış tabanlı sıkıştırma
                using (FileStream inputStream = new(inputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920))
                using (FileStream outputStream = new(compressedFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920))
                {
                    // İlerleme raporlama
                    Progress<double> progress = new(value =>
                    {
                        Console.Write($"\rSıkıştırma: %{value * 100:F1}");
                    });

                    // Akış tabanlı kopyalama (Store algoritması sıkıştırma yapmaz)
                    await provider.CompressAsync(inputStream, outputStream, progress);
                }

                compressTimer.Stop();
                Console.WriteLine($"\rSıkıştırma tamamlandı: {compressTimer.ElapsedMilliseconds:N0} ms");

                // Sıkıştırılmış dosya bilgisi
                FileInfo compressedFile = new(compressedFilePath);
                long compressedSize = compressedFile.Length;
                double ratio = (1.0 - ((double)compressedSize / inputSize)) * 100;

                Console.WriteLine($"Sıkıştırılmış boyut: {compressedSize:N0} bayt");
                Console.WriteLine($"Sıkıştırma oranı: %{ratio:F2}");

                // AÇMA
                Console.WriteLine("\nAçma işlemi başlıyor...");
                Stopwatch decompressTimer = Stopwatch.StartNew();

                // Akış tabanlı açma
                using (FileStream inputStream = new(compressedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920))
                using (FileStream outputStream = new(decompressedFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920))
                {
                    // İlerleme raporlama
                    Progress<double> progress = new(value =>
                    {
                        Console.Write($"\rAçma: %{value * 100:F1}");
                    });

                    // Akış tabanlı açma (aslında sadece kopyalama)
                    await provider.DecompressAsync(inputStream, outputStream, progress);
                }

                decompressTimer.Stop();
                Console.WriteLine($"\rAçma tamamlandı: {decompressTimer.ElapsedMilliseconds:N0} ms");

                // Açılmış dosya bilgisi
                FileInfo decompressedFile = new(decompressedFilePath);
                Console.WriteLine($"Açılmış boyut: {decompressedFile.Length:N0} bayt");

                // Doğrulama (dosya boyutları eşit olmalı)
                if (inputSize == decompressedFile.Length)
                {
                    Console.WriteLine("✅ Doğrulama başarılı: Dosya boyutları eşleşiyor");
                }
                else
                {
                    Console.WriteLine($"❌ Doğrulama başarısız: Dosya boyutları eşleşmiyor ({inputSize} vs {decompressedFile.Length})");
                }

                totalTimer.Stop();
                Console.WriteLine($"\nToplam işlem süresi: {totalTimer.ElapsedMilliseconds:N0} ms");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test başarısız: {ex.Message}");
            }
        }

        /// <summary>
        /// Büyük dosyaları parçalı işlemeyi test eder
        /// </summary>
        private static async Task TestChunkedProcessing(string inputFilePath, string outputDir)
        {
            Console.WriteLine("\n📊 Test 2: Parçalı İşleme");
            Console.WriteLine("-------------------------");

            try
            {
                FileInfo inputFile = new(inputFilePath);
                long inputSize = inputFile.Length;

                Console.WriteLine($"Kaynak dosya: {Path.GetFileName(inputFilePath)} ({inputSize:N0} bayt)");

                string processedFilePath = Path.Combine(outputDir, "chunked_process.dat");

                Stopwatch timer = Stopwatch.StartNew();

                // Parça boyutu: 5 MB
                const int chunkSize = 5 * 1024 * 1024;
                byte[] buffer = new byte[chunkSize];

                // Parça sayısı hesapla
                int totalChunks = (int)Math.Ceiling((double)inputSize / chunkSize);
                Console.WriteLine($"Parça boyutu: {chunkSize:N0} bayt");
                Console.WriteLine($"Toplam parça sayısı: {totalChunks}");

                // Parçalı işleme (burada örnek olarak sadece dosyayı kopyalıyoruz)
                using (FileStream inputStream = new(inputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, chunkSize))
                using (FileStream outputStream = new(processedFilePath, FileMode.Create, FileAccess.Write, FileShare.None, chunkSize))
                {
                    long totalBytesRead = 0;
                    int chunksProcessed = 0;
                    int bytesRead;

                    while ((bytesRead = await inputStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        // İşleme simülasyonu (burada örnek olarak buffer'ı doğrudan yazıyoruz)
                        await outputStream.WriteAsync(buffer, 0, bytesRead);

                        totalBytesRead += bytesRead;
                        chunksProcessed++;

                        // İlerleme göster
                        double progress = (double)totalBytesRead / inputSize;
                        Console.Write($"\rİşleniyor: Parça {chunksProcessed}/{totalChunks} - %{progress * 100:F1}");
                    }
                }

                timer.Stop();

                Console.WriteLine($"\rParçalı işleme tamamlandı: {timer.ElapsedMilliseconds:N0} ms");

                // Doğrulama
                FileInfo processedFile = new(processedFilePath);
                if (inputSize == processedFile.Length)
                {
                    Console.WriteLine("✅ Doğrulama başarılı: Dosya boyutları eşleşiyor");
                }
                else
                {
                    Console.WriteLine($"❌ Doğrulama başarısız: Dosya boyutları eşleşmiyor ({inputSize} vs {processedFile.Length})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test başarısız: {ex.Message}");
            }
        }

        /// <summary>
        /// Akış tabanlı arşiv oluşturma ve çıkarma işlemlerini test eder
        /// </summary>
        private static async Task TestStreamingArchive(string sourceDir, string archivePath)
        {
            Console.WriteLine("\n📚 Test 3: Akış Tabanlı Arşivleme");
            Console.WriteLine("--------------------------------");

            try
            {
                Console.WriteLine($"Kaynak dizin: {sourceDir}");
                Console.WriteLine($"Hedef arşiv: {Path.GetFileName(archivePath)}");

                Stopwatch totalTimer = Stopwatch.StartNew();

                // ARŞİVLEME
                Console.WriteLine("\nArşivleme işlemi başlıyor...");
                Stopwatch archiveTimer = Stopwatch.StartNew();

                // Arşiv seçenekleri
                FragileOptions options = new()
                {
                    CompressionAlgorithm = CompressionAlgorithm.Store, // Store algoritması kullanalım
                    CompressionLevel = CompressionLevel.Normal
                };

                // Akış tabanlı arşivleme
                using (FragileArchive archive = new(archivePath, FragileArchiveMode.Create, options))
                {
                    // Büyük dosya için özel işleme
                    string largeFilePath = Path.Combine(sourceDir, "large_file.dat");

                    Console.WriteLine($"Büyük dosya ekleniyor: {Path.GetFileName(largeFilePath)}");

                    // Doğrudan AddFile metodu kullanarak dosyayı arşive ekleyelim
                    archive.AddFile(largeFilePath);
                    Console.WriteLine("Dosya arşive eklendi");

                    // Arşivi kaydet
                    archive.Save();
                }

                archiveTimer.Stop();
                Console.WriteLine($"\rArşivleme tamamlandı: {archiveTimer.ElapsedMilliseconds:N0} ms");

                // Arşiv bilgisi
                FileInfo archiveFile = new(archivePath);
                Console.WriteLine($"Arşiv boyutu: {archiveFile.Length:N0} bayt");

                // ÇIKARMA
                Console.WriteLine("\nArşivden çıkarma işlemi başlıyor...");
                Stopwatch extractTimer = Stopwatch.StartNew();

                // Çıkarma dizini
                string extractDir = Path.Combine(Path.GetDirectoryName(archivePath), "extracted_streaming");
                if (Directory.Exists(extractDir))
                {
                    Directory.Delete(extractDir, true);
                }
                Directory.CreateDirectory(extractDir);

                // Akış tabanlı çıkarma
                using (FragileArchive archive = new(archivePath, FragileArchiveMode.Read))
                {
                    // Tüm dosyaları çıkar
                    archive.ExtractAll(extractDir);
                }

                extractTimer.Stop();
                Console.WriteLine($"\rÇıkarma tamamlandı: {extractTimer.ElapsedMilliseconds:N0} ms");

                totalTimer.Stop();
                Console.WriteLine($"\nToplam işlem süresi: {totalTimer.ElapsedMilliseconds:N0} ms");

                // Çıkarılan dosyaları kontrol et
                string extractedFilePath = Path.Combine(extractDir, "large_file.dat");
                if (File.Exists(extractedFilePath))
                {
                    FileInfo extractedFile = new(extractedFilePath);
                    FileInfo sourceFile = new(Path.Combine(sourceDir, "large_file.dat"));

                    if (extractedFile.Length == sourceFile.Length)
                    {
                        Console.WriteLine("✅ Doğrulama başarılı: Dosya boyutları eşleşiyor");
                    }
                    else
                    {
                        Console.WriteLine($"❌ Doğrulama başarısız: Dosya boyutları eşleşmiyor ({sourceFile.Length} vs {extractedFile.Length})");
                    }
                }
                else
                {
                    Console.WriteLine("❌ Doğrulama başarısız: Dosya bulunamadı");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test başarısız: {ex.Message}");
            }
        }

        /// <summary>
        /// İlerleme raporlamasını test eder
        /// </summary>
        private static async Task TestProgressReporting(string sourceDir, string archivePath)
        {
            Console.WriteLine("\n📈 Test 4: İlerleme Raporlama");
            Console.WriteLine("----------------------------");

            try
            {
                Console.WriteLine($"Kaynak dizin: {sourceDir}");
                Console.WriteLine($"Hedef arşiv: {Path.GetFileName(archivePath)}");

                // Akış tabanlı arşivleme ve ilerleme raporlama
                using (FragileArchive archive = new(archivePath, FragileArchiveMode.Create))
                {
                    string largeFilePath = Path.Combine(sourceDir, "large_file.dat");
                    FileInfo fileInfo = new(largeFilePath);

                    Console.WriteLine($"\nDosya ekleniyor: {Path.GetFileName(largeFilePath)} ({fileInfo.Length:N0} bayt)");

                    // İlerlemeyi izlemek için olay aboneliği simülasyonu
                    ProgressBar progressBar = new();

                    await SimulateStreamingOperation(
                        largeFilePath,
                        progressBar.Update
                    );

                    archive.Save();
                }

                Console.WriteLine("\nİşlem tamamlandı");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test başarısız: {ex.Message}");
            }
        }

        /// <summary>
        /// Sınırlı bellek kullanımını test eder
        /// </summary>
        private static async Task TestLimitedMemory(string sourceDir, string archivePath)
        {
            Console.WriteLine("\n💾 Test 5: Sınırlı Bellek Kullanımı");
            Console.WriteLine("---------------------------------");

            try
            {
                Console.WriteLine($"Kaynak dizin: {sourceDir}");
                Console.WriteLine($"Hedef arşiv: {Path.GetFileName(archivePath)}");

                // Sınırlı bellek kullanımı simülasyonu
                // Gerçek uygulamada, GC ve bellek yönetimi daha karmaşık olacaktır

                // Sınırlı bellek ayarlarıyla arşiv oluştur
                int memoryLimit = 10 * 1024 * 1024; // 10 MB
                Console.WriteLine($"Bellek limiti: {memoryLimit:N0} bayt");

                // Akış tabanlı sınırlı bellek kullanımıyla arşivleme
                using (FragileArchive archive = new(archivePath, FragileArchiveMode.Create))
                {
                    string largeFilePath = Path.Combine(sourceDir, "large_file.dat");
                    FileInfo fileInfo = new(largeFilePath);

                    Console.WriteLine($"\nDosya işleniyor: {Path.GetFileName(largeFilePath)} ({fileInfo.Length:N0} bayt)");

                    // Sınırlı bellek kullanımı simülasyonu
                    await SimulateLimitedMemoryProcessing(
                        largeFilePath,
                        memoryLimit
                    );

                    archive.Save();
                }

                Console.WriteLine("\nİşlem tamamlandı");

                // Gerçek bellek kullanımı bilgisi
                double memoryUsedMB = GC.GetTotalMemory(true) / (1024.0 * 1024.0);
                Console.WriteLine($"Güncel bellek kullanımı: {memoryUsedMB:F2} MB");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test başarısız: {ex.Message}");
            }
        }

        /// <summary>
        /// Akış işlemi ve ilerleme raporlamayı simüle eder
        /// </summary>
        private static async Task SimulateStreamingOperation(string filePath, Action<double> progressCallback)
        {
            FileInfo fileInfo = new(filePath);
            long fileSize = fileInfo.Length;

            // Sabit boyutlu buffer kullan
            const int bufferSize = 1024 * 1024; // 1 MB
            byte[] buffer = new byte[bufferSize];

            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize);
            long totalBytesRead = 0;
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                totalBytesRead += bytesRead;

                // İlerleme raporla
                double progress = (double)totalBytesRead / fileSize;
                progressCallback(progress);

                // İşlemi yavaşlat (gerçek uygulamada bu olmayacak)
                await Task.Delay(50);
            }
        }

        /// <summary>
        /// Sınırlı bellek kullanımı ile dosya işlemeyi simüle eder
        /// </summary>
        private static async Task SimulateLimitedMemoryProcessing(string filePath, int memoryLimit)
        {
            FileInfo fileInfo = new(filePath);
            long fileSize = fileInfo.Length;

            // Parça boyutu bellek limitinden daha küçük olmalı
            int chunkSize = memoryLimit / 2;
            byte[] buffer = new byte[chunkSize];

            Console.WriteLine($"Parça boyutu: {chunkSize:N0} bayt");

            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, chunkSize);
            long totalBytesRead = 0;
            int chunksProcessed = 0;
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                totalBytesRead += bytesRead;
                chunksProcessed++;

                // Her parçadan sonra bellek durumunu göster
                double progress = (double)totalBytesRead / fileSize;
                double memoryUsedMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0);

                Console.Write($"\rİşleniyor: Parça {chunksProcessed} - %{progress * 100:F1} - Bellek: {memoryUsedMB:F2} MB");

                // Bellek temizliği simülasyonu
                if (chunksProcessed % 10 == 0)
                {
                    GC.Collect();
                    double memoryAfterGC = GC.GetTotalMemory(true) / (1024.0 * 1024.0);
                    Console.WriteLine($"\nBellek temizlendi: {memoryAfterGC:F2} MB");
                }

                // İşlemi yavaşlat (gerçek uygulamada bu olmayacak)
                await Task.Delay(10);
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Test için büyük rastgele veri dosyası oluşturur
        /// </summary>
        private static async Task CreateLargeTestFile(string filePath, int sizeInBytes)
        {
            Console.WriteLine($"Büyük test dosyası oluşturuluyor: {Path.GetFileName(filePath)} ({sizeInBytes / (1024 * 1024)} MB)");

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            Stopwatch timer = Stopwatch.StartNew();

            // Dosya oluşturma işlemi
            using (FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920))
            {
                // 1 MB'lık parçalar halinde yaz
                Random random = new();
                byte[] buffer = new byte[1024 * 1024];

                int bytesRemaining = sizeInBytes;
                while (bytesRemaining > 0)
                {
                    // Parça boyutunu belirle
                    int chunkSize = Math.Min(buffer.Length, bytesRemaining);

                    // Rastgele veri oluştur
                    random.NextBytes(buffer);

                    // Dosyaya yaz
                    await fileStream.WriteAsync(buffer, 0, chunkSize);

                    // Kalan byte sayısını güncelle
                    bytesRemaining -= chunkSize;

                    // İlerleme göster
                    double progress = 1.0 - ((double)bytesRemaining / sizeInBytes);
                    Console.Write($"\rİlerleme: %{progress * 100:F1}");
                }
            }

            timer.Stop();

            Console.WriteLine($"\rBüyük test dosyası oluşturuldu: {timer.ElapsedMilliseconds:N0} ms");
        }
    }

    /// <summary>
    /// Basit konsol ilerleme çubuğu
    /// </summary>
    public class ProgressBar
    {
        private int _lastBarSize = 0;

        public void Update(double progress)
        {
            int barSize = (int)(progress * 50);
            if (barSize != _lastBarSize)
            {
                _lastBarSize = barSize;

                string bar = new string('#', barSize).PadRight(50, '-');
                Console.Write($"\rİlerleme: [{bar}] %{progress * 100:F1}");
            }
        }
    }
}