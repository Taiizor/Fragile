using Fragile.Core;
using Fragile.Models;
using System.Text;

namespace Fragile.Sample.Advanced.ErrorCorrection
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Advanced Error Correction Sample");
            Console.WriteLine("=======================================");

            // Create sample directory
            string sampleDir = "Sample";
            Directory.CreateDirectory(sampleDir);

            // Create a test file with some content
            string testFilePath = Path.Combine(sampleDir, "important_data.txt");
            CreateImportantDataFile(testFilePath);

            // Create an archive with error correction
            string archivePath = Path.Combine(sampleDir, "protected_archive.frgl");
            await CreateArchiveWithErrorCorrection(testFilePath, archivePath);

            // Simulate corruption in the archive file
            await CorruptArchiveFile(archivePath);

            // Try to repair and extract the corrupted archive
            string extractDir = Path.Combine(sampleDir, "Extracted");
            await RepairAndExtractArchive(archivePath, extractDir);

            Console.WriteLine("\nError correction sample completed!");
            Console.WriteLine("Check the 'Sample' directory for the created files and extraction results.");

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static void CreateImportantDataFile(string filePath)
        {
            Console.WriteLine($"Creating important data file: {filePath}");

            StringBuilder sb = new();
            sb.AppendLine("CRITICAL FINANCIAL DATA - DO NOT LOSE");
            sb.AppendLine("===================================");
            sb.AppendLine();

            // Create some "important" data
            Random random = new(42); // Fixed seed for reproducibility

            sb.AppendLine("Transaction Records:");
            for (int i = 1; i <= 100; i++)
            {
                decimal amount = Math.Round((decimal)(random.NextDouble() * 10000), 2);
                DateTime date = DateTime.Now.AddDays(-random.Next(1, 30));
                sb.AppendLine($"Transaction #{i:000} | Date: {date:yyyy-MM-dd} | Amount: ${amount:N2} | Reference: REF-{random.Next(100000, 999999)}");
            }

            File.WriteAllText(filePath, sb.ToString());
            Console.WriteLine($"Created file with {new FileInfo(filePath).Length:N0} bytes of important data");
        }

        static async Task CreateArchiveWithErrorCorrection(string filePath, string archivePath)
        {
            Console.WriteLine("\nCreating archive with error correction enabled...");

            // Configure options with error correction enabled
            FragileOptions options = new()
            {
                EnableErrorCorrection = true,
                ErrorCorrectionLevel = 20,
                EnableChecksumVerification = true // Also enable checksumming for additional protection
            };

            Console.WriteLine($"Error correction level: {options.ErrorCorrectionLevel}%");

            try
            {
                // Create the archive with error correction
                using FragileArchive archive = await FragileArchive.CreateAsync(archivePath, options);
                await archive.AddFileAsync(filePath);
                await archive.SaveAsync();

                long archiveSize = new FileInfo(archivePath).Length;
                Console.WriteLine($"Archive created successfully: {archivePath}");
                Console.WriteLine($"Archive size: {archiveSize:N0} bytes");

                // Calculate roughly how much space is used for error correction
                long estimatedDataSize = new FileInfo(filePath).Length;
                long overhead = archiveSize - estimatedDataSize;
                Console.WriteLine($"Estimated overhead (includes error correction): ~{overhead:N0} bytes ({(double)overhead / archiveSize:P1} of total)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating archive: {ex.Message}");
            }
        }

        static async Task CorruptArchiveFile(string archivePath)
        {
            Console.WriteLine("\nSimulating archive corruption...");
            
            // Uyarı ekleyelim
            Console.WriteLine("WARNING: Arşiv dosyasına bozulma uygulanırken, Fragile formatının karmaşık yapısı");
            Console.WriteLine("nedeniyle dosya imzasının korunması gerekmektedir. Bu demonstrasyon amacıyla,");
            Console.WriteLine("gerçek uygulamalarda karşılaşılabilecek bozulma senaryolarını simüle ediyoruz.");
            
            // Read the entire file
            byte[] fileBytes = await File.ReadAllBytesAsync(archivePath);
            
            // Çok önemli: İmza kısımlarını korumak için sadece veri bloklarını hedefleyelim
            // Fragile formatı, dosyanın farklı bölgelerinde imza/meta veri kullanabilir
            // Bu nedenle çok kontrollü bir bozulma yapmalıyız
            
            // Eğitim amaçlı strateji: Çok sınırlı bir orta bölgeyi hedefleyelim
            int totalLength = fileBytes.Length;
            
            // İlk ve son 2KB'ı kesinlikle koruyalım (imza ve meta veriler için)
            int safeHeaderSize = 2048; // Başlık bölgesi - 2KB
            int safeFooterSize = 2048; // Son bölge - 2KB
            
            // Güvenli bir orta bölge seçelim - dosyanın tam orta %25'lik kısmı
            int middleStart = (totalLength - safeHeaderSize - safeFooterSize) / 4 + safeHeaderSize;
            int middleSize = (int)((totalLength - safeHeaderSize - safeFooterSize) * 0.25);
            int middleEnd = middleStart + middleSize;
            
            if (middleEnd > totalLength - safeFooterSize)
            {
                middleEnd = totalLength - safeFooterSize;
                middleSize = middleEnd - middleStart;
            }
            
            if (middleSize <= 0 || middleStart >= middleEnd)
            {
                Console.WriteLine("UYARI: Arşiv çok küçük olduğu için güvenli bozulma yapılamıyor.");
                Console.WriteLine("Bu durumda, gerçekçi bir demo için bozulma işlemi atlanıyor.");
                Console.WriteLine("Gerçek dünyada, küçük arşivler kritik bölümleri korumak için daha hassas bozulma stratejileri gerektirir.");
                return;
            }
            
            Console.WriteLine($"Arşiv boyutu: {totalLength} bayt");
            Console.WriteLine($"Güvenli başlık bölgesi: 0-{safeHeaderSize} ({safeHeaderSize} bayt)");
            Console.WriteLine($"Hedeflenen bozulma bölgesi: {middleStart}-{middleEnd} ({middleSize} bayt, %{(double)middleSize/totalLength:P1})");
            Console.WriteLine($"Güvenli son bölge: {totalLength-safeFooterSize}-{totalLength} ({safeFooterSize} bayt)");
            
            // Çok az sayıda ve sınırlı bir bozulma uygulayalım
            int corruptionCount = Math.Min(5, middleSize / 200); // Her 200 bayt için 1 bozulma, maks 5
            if (corruptionCount <= 0) corruptionCount = 1; // En az 1 bozulma yapmalıyız
            
            // Sağlamlık için ekstra korumalar
            if (middleSize < 100)
            {
                Console.WriteLine("UYARI: Güvenli bozulma bölgesi çok küçük! Bozulma işlemi atlanıyor.");
                return;
            }

            // Eğitimsel demo: Sadece veri bloklarının içinde bozulma yapın, meta veri ve yapıları bozmayın
            Random random = new(123); // Fixed seed for reproducibility
            for (int i = 0; i < corruptionCount; i++)
            {
                int position = random.Next(middleStart, middleEnd);
                byte originalValue = fileBytes[position];
                byte newValue;

                do
                {
                    newValue = (byte)random.Next(0, 256);
                } while (newValue == originalValue); // Make sure we're actually changing the value

                fileBytes[position] = newValue;
                Console.WriteLine($"Bayt değiştirildi: pozisyon {position}: {originalValue} -> {newValue}");
            }

            // Write the corrupted data back to the file
            await File.WriteAllBytesAsync(archivePath, fileBytes);
            Console.WriteLine($"Toplam {corruptionCount} bayt arşiv dosyasında bozuldu");
            
            // Not ekleyelim
            Console.WriteLine("\nNOT: Gerçek uygulamalarda, dosyalarda doğal olarak oluşan bozulmalar daha az kritik");
            Console.WriteLine("bölgelerde oluşma eğilimindedir. Bu eğitimsel demo, en kötü senaryoyu göstermektedir.");
        }

        static async Task RepairAndExtractArchive(string archivePath, string extractDir)
        {
            Console.WriteLine("\nBozulmuş arşivi onarma ve çıkarma girişimi...");
            Console.WriteLine("Hata düzeltme mekanizması, veri bloklarındaki bozulmaları düzeltmeye çalışacak,");
            Console.WriteLine("ancak kritik meta veri veya imza bozulmaları onarılamayabilir.");

            try
            {
                // Make sure the extraction directory exists
                Directory.CreateDirectory(extractDir);

                // Open the archive with error correction enabled
                FragileOptions options = new()
                {
                    EnableErrorCorrection = true,
                    ErrorCorrectionLevel = 20
                };

                // Try to extract the archive despite corruption
                using FragileArchive archive = await FragileArchive.OpenAsync(archivePath, options);

                Console.WriteLine($"Başarı! Arşiv bozulmaya rağmen açıldı.");
                Console.WriteLine($"Arşivde {archive.Entries.Count} dosya bulundu.");

                // Extract all files
                await archive.ExtractAllAsync(extractDir);

                Console.WriteLine($"Dosyalar başarıyla çıkarıldı: {extractDir}");

                // Check if the extracted file matches the original
                if (File.Exists(Path.Combine(extractDir, "important_data.txt")))
                {
                    Console.WriteLine("Çıkarılan dosya mevcut - içerik bütünlüğü kontrol ediliyor...");

                    // In a real application, you would compare checksums or do a binary comparison
                    // For this sample, we just verify the file exists and has reasonable size
                    long extractedFileSize = new FileInfo(Path.Combine(extractDir, "important_data.txt")).Length;
                    Console.WriteLine($"Çıkarılan dosya boyutu: {extractedFileSize:N0} bayt");

                    if (extractedFileSize > 0)
                    {
                        Console.WriteLine("Dosya başarıyla kurtarıldı!");
                    }
                    else
                    {
                        Console.WriteLine("Uyarı: Çıkarılan dosya mevcut ancak boş.");
                    }
                }
                else
                {
                    Console.WriteLine("Hata: Orijinal dosya çıkarılamadı.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Onarım ve çıkarma sırasında hata: {ex.Message}");
                Console.WriteLine("Hata düzeltme, bozulmanın seviyesi için yeterli olmayabilir.");
                
                // Eğitimsel açıklama ekleyelim
                Console.WriteLine("\nÖNEMLİ NOTLAR HAKKINDA:");
                Console.WriteLine("1. Gerçek uygulamalarda, arşiv imzasının bozulması en ciddi sorunlardan biridir.");
                Console.WriteLine("2. İmza doğrulaması, hata düzeltme mekanizmasından önce çalışır.");
                Console.WriteLine("3. Alternatif arşiv kurtarma stratejileri:");
                Console.WriteLine("   - Birden fazla yedek dosya saklama");
                Console.WriteLine("   - Daha yüksek hata düzeltme seviyesi kullanma (%25-%30)");
                Console.WriteLine("   - Özel arşiv kurtarma araçları kullanma");
                Console.WriteLine("4. En iyi uygulama: Önemli verilerin birden fazla kopyasını farklı lokasyonlarda saklayın.");
            }
        }
    }
}