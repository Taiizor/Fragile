using Fragile.Verification;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Fragile.Samples.Verification
{
    /// <summary>
    /// Fragile bütünlük kontrolü ve doğrulama özelliklerini gösteren örnek uygulama
    /// </summary>
    public class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Bütünlük Kontrolü Örneği");
            Console.WriteLine("================================");

            try
            {
                // Geçici dizin oluştur
                string tempDir = Path.Combine(Path.GetTempPath(), "FragileVerificationSample");
                Directory.CreateDirectory(tempDir);

                // Test dosyaları için çıktı dizini
                string outputDir = Path.Combine(tempDir, "output");
                Directory.CreateDirectory(outputDir);

                // Farklı boyutlarda test dosyaları oluştur
                FileInfo[] testFiles = await CreateTestFiles(tempDir);
                Console.WriteLine("\nTest dosyaları oluşturuldu:");
                foreach (FileInfo file in testFiles)
                {
                    Console.WriteLine($"- {file.Name} ({file.Length:N0} bayt)");
                }

                // Tüm doğrulama algoritmaları için test et
                Console.WriteLine("\nAlgoritma karşılaştırması:");
                Console.WriteLine("===================================");

                // Tablo başlığı
                Console.WriteLine("| Algoritma | Dosya | Boyut | Hesaplama Süresi | Sağlama Boyutu |");
                Console.WriteLine("|-----------|-------|-------|-----------------|----------------|");

                foreach (FileInfo file in testFiles)
                {
                    foreach (ChecksumAlgorithm algorithm in Enum.GetValues<ChecksumAlgorithm>())
                    {
                        await TestVerification(file.FullName, algorithm);
                    }
                }

                // Dosya bozulma testi
                Console.WriteLine("\n\nBozulma Tespiti Testi");
                Console.WriteLine("===================================");
                await TestCorruption(testFiles[0].FullName, outputDir);
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
        /// Belirtilen algoritma ile dosya doğrulama testi yapar
        /// </summary>
        private static async Task TestVerification(string filePath, ChecksumAlgorithm algorithm)
        {
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                FileInfo fileInfo = new(filePath);

                // Doğrulama sağlayıcısını oluştur
                VerificationProvider provider = VerificationProvider.Create(algorithm);

                // Sağlama toplamını hesapla
                byte[] checksum;
                using (FileStream stream = new(filePath, FileMode.Open, FileAccess.Read))
                {
                    checksum = await provider.CalculateChecksumAsync(stream);
                }

                stopwatch.Stop();

                // Sonuçları yazdır (tablo formatında)
                Console.WriteLine($"| {algorithm,-9} | {Path.GetFileName(filePath),-5} | {fileInfo.Length,-5:N0} | {stopwatch.ElapsedMilliseconds,-15:N0} ms | {checksum.Length,-14:N0} bayt |");
            }
            catch (NotSupportedException nse)
            {
                Console.WriteLine($"| {algorithm,-9} | {Path.GetFileName(filePath),-5} | {"N/A",-5} | {"Desteklenmiyor",-15} | {"N/A",-14} |");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"| {algorithm,-9} | {Path.GetFileName(filePath),-5} | {"HATA",-5} | {ex.Message,-15} | {"N/A",-14} |");
            }
        }

        /// <summary>
        /// Dosya bozulma tespiti testi
        /// </summary>
        private static async Task TestCorruption(string filePath, string outputDir)
        {
            string fileName = Path.GetFileName(filePath);
            byte[] originalData = await File.ReadAllBytesAsync(filePath);

            // Bozulmamış dosyayı kontrol et (doğrulama geçerli olmalı)
            string intactFilePath = Path.Combine(outputDir, $"intact_{fileName}");
            await File.WriteAllBytesAsync(intactFilePath, originalData);

            // Bozulmuş dosyayı oluştur (bir byte değiştirilmiş)
            string corruptedFilePath = Path.Combine(outputDir, $"corrupted_{fileName}");
            byte[] corruptedData = (byte[])originalData.Clone();

            // Ortadaki bir byte'ı değiştirerek bozulma simüle et
            int middlePos = corruptedData.Length / 2;
            corruptedData[middlePos] = (byte)(corruptedData[middlePos] ^ 0xFF); // Bit düzeyinde XOR ile tersine çevir

            await File.WriteAllBytesAsync(corruptedFilePath, corruptedData);

            Console.WriteLine($"Orijinal dosya: {intactFilePath}");
            Console.WriteLine($"Bozulmuş dosya: {corruptedFilePath} (byte {middlePos} değiştirildi)");
            Console.WriteLine();

            // Her algoritmayla test et
            Console.WriteLine("| Algoritma | Bozulmamış Dosya | Bozulmuş Dosya |");
            Console.WriteLine("|-----------|-----------------|----------------|");

            foreach (ChecksumAlgorithm algorithm in Enum.GetValues<ChecksumAlgorithm>())
            {
                if (algorithm == ChecksumAlgorithm.None)
                {
                    continue; // None doğrulama yapmaz
                }

                try
                {
                    VerificationProvider provider = VerificationProvider.Create(algorithm);

                    // Önce sağlama toplamını hesapla
                    byte[] checksum;
                    using (FileStream stream = new(intactFilePath, FileMode.Open, FileAccess.Read))
                    {
                        checksum = await provider.CalculateChecksumAsync(stream);
                    }

                    // Bozulmamış dosyayı doğrula (başarılı olmalı)
                    bool intactResult;
                    using (FileStream stream = new(intactFilePath, FileMode.Open, FileAccess.Read))
                    {
                        intactResult = await provider.VerifyChecksumAsync(stream, checksum);
                    }

                    // Bozulmuş dosyayı doğrula (başarısız olmalı)
                    bool corruptedResult;
                    using (FileStream stream = new(corruptedFilePath, FileMode.Open, FileAccess.Read))
                    {
                        corruptedResult = await provider.VerifyChecksumAsync(stream, checksum);
                    }

                    Console.WriteLine($"| {algorithm,-9} | {(intactResult ? "✓ Geçerli" : "✗ Geçersiz"),-16} | {(corruptedResult ? "✓ Geçerli" : "✗ Geçersiz"),-14} |");
                }
                catch (NotSupportedException)
                {
                    Console.WriteLine($"| {algorithm,-9} | {"Desteklenmiyor",-16} | {"Desteklenmiyor",-14} |");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"| {algorithm,-9} | {"HATA",-16} | {ex.Message,-14} |");
                }
            }
        }

        /// <summary>
        /// Farklı boyutlarda test dosyaları oluşturur
        /// </summary>
        private static async Task<FileInfo[]> CreateTestFiles(string directory)
        {
            // Küçük dosya (10 KB)
            string smallFilePath = Path.Combine(directory, "small.dat");
            await CreateRandomDataFile(smallFilePath, 10 * 1024);

            // Orta dosya (1 MB)
            string mediumFilePath = Path.Combine(directory, "medium.dat");
            await CreateRandomDataFile(mediumFilePath, 1 * 1024 * 1024);

            // Büyük dosya (5 MB) - gerçekçi bir test için
            string largeFilePath = Path.Combine(directory, "large.dat");
            await CreateRandomDataFile(largeFilePath, 5 * 1024 * 1024);

            return new FileInfo[]
            {
                new(smallFilePath),
                new(mediumFilePath),
                new(largeFilePath)
            };
        }

        /// <summary>
        /// Belirtilen boyutta rastgele veri içeren bir dosya oluşturur
        /// </summary>
        private static async Task CreateRandomDataFile(string filePath, int sizeInBytes)
        {
            using RandomNumberGenerator rng = RandomNumberGenerator.Create();
            byte[] buffer = new byte[Math.Min(sizeInBytes, 1024 * 1024)]; // Max 1MB buffer

            using FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write);

            int remaining = sizeInBytes;
            while (remaining > 0)
            {
                int chunkSize = Math.Min(remaining, buffer.Length);
                rng.GetBytes(buffer, 0, chunkSize);
                await fileStream.WriteAsync(buffer, 0, chunkSize);
                remaining -= chunkSize;
            }
        }
    }
}
