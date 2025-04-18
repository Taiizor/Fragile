using Fragile.Compression;
using Fragile.Core;
using Fragile.Encryption;
using Fragile.Models;
using Fragile.Verification;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Fragile.Sample.Mastery.SecureFileVault
{
    class Program
    {
        private const string SampleDataFolder = "SampleData";
        private const string VaultsFolder = "SecureVaults";
        private const string ExtractFolder = "ExtractedFiles";

        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("=== Fragile Güvenli Dosya Kasası Örneği ===");

            try
            {
                // Çalışma ortamını hazırla
                await PrepareEnvironmentAsync();

                // Örnek bir kasa oluştur
                await CreateSampleVaultAsync();

                // Kasayı aç ve içeriğini göster
                await OpenVaultAsync();

                // Sıkıştırma algoritmalarını karşılaştır
                await CompareCompressionsAsync();

                // Şifreleme algoritmalarını karşılaştır
                await CompareEncryptionsAsync();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Hata: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine("\nÇıkmak için bir tuşa basın...");
            Console.ReadKey();
        }

        static async Task PrepareEnvironmentAsync()
        {
            Console.WriteLine("\n=> Örnek ortam oluşturuluyor...");

            // Çalışma klasörlerini oluştur
            Directory.CreateDirectory(SampleDataFolder);
            Directory.CreateDirectory(VaultsFolder);
            Directory.CreateDirectory(ExtractFolder);

            // Örnek metin dosyası oluştur
            string textFile = Path.Combine(SampleDataFolder, "ornek.txt");
            await File.WriteAllTextAsync(textFile,
                "Bu Fragile kütüphanesi ile şifrelenecek bir örnek dosyadır.\n" +
                "Bu dosya güvenli bir şekilde kasada saklanacak.\n" +
                "Fragile, gelişmiş şifreleme ve sıkıştırma özellikleri sunar.");

            // Örnek JSON dosyası oluştur
            string jsonFile = Path.Combine(SampleDataFolder, "config.json");
            await File.WriteAllTextAsync(jsonFile,
                "{\n" +
                "  \"uygulama\": \"Fragile Kasa Örneği\",\n" +
                "  \"versiyon\": \"1.0.0\",\n" +
                "  \"ayarlar\": {\n" +
                "    \"sifreleme\": \"AES-256\",\n" +
                "    \"sikistirma\": \"LZMA\",\n" +
                "    \"dogrulama\": true\n" +
                "  }\n" +
                "}");

            // Örnek binary dosya oluştur
            string binaryFile = Path.Combine(SampleDataFolder, "ornekVeri.bin");
            byte[] randomData = new byte[4096];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomData);
            }
            await File.WriteAllBytesAsync(binaryFile, randomData);

            // Alt klasör oluştur
            string subFolder = Path.Combine(SampleDataFolder, "Dokumantasyon");
            Directory.CreateDirectory(subFolder);

            string readmeFile = Path.Combine(subFolder, "benioku.md");
            await File.WriteAllTextAsync(readmeFile,
                "# Fragile Güvenli Kasa\n\n" +
                "Bu örnek, Fragile kütüphanesinin aşağıdaki özelliklerini gösterir:\n\n" +
                "- AES-256 şifreleme\n" +
                "- LZMA sıkıştırma\n" +
                "- SHA-256 bütünlük doğrulaması\n" +
                "- Hata düzeltme\n\n" +
                "Dosyalarınız güvende!");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Örnek dosyalar oluşturuldu.");
            Console.ResetColor();
        }

        static async Task CreateSampleVaultAsync()
        {
            Console.WriteLine("\n=> Örnek kasa oluşturuluyor...");

            string kasaAdi = "OrnekKasa";
            string kasaYolu = Path.Combine(VaultsFolder, $"{kasaAdi}.vault");
            string sifre = "GuvenliParola123";

            Console.WriteLine($"Kasa adı: {kasaAdi}");
            Console.WriteLine($"Kasa şifresi: {sifre}");

            // Kasa yapılandırması
            FragileOptions options = new()
            {
                Password = sifre,
                EnableEncryption = true,
                EncryptionMethod = EncryptionMethod.AES256,
                CompressionAlgorithm = CompressionAlgorithm.Brotli,
                CompressionLevel = CompressionLevel.Ultra,
                EnableChecksumVerification = true,
                ChecksumAlgorithm = ChecksumAlgorithm.SHA256,
                EnableErrorCorrection = false,
                ErrorCorrectionLevel = 10,
                Extension = ".vault",
                Progress = new Progress<double>(p => ReportProgress("Kasa oluşturuluyor", p))
            };

            Console.WriteLine("\nKasa ayarları:");
            Console.WriteLine($"- Şifreleme: {options.EncryptionMethod}");
            Console.WriteLine($"- Sıkıştırma: {options.CompressionAlgorithm} ({options.CompressionLevel})");
            Console.WriteLine($"- Doğrulama: {options.ChecksumAlgorithm}");
            Console.WriteLine($"- Hata Düzeltme: {(options.EnableErrorCorrection ? $"Aktif ({options.ErrorCorrectionLevel}%)" : "Pasif")}");

            Console.WriteLine("\nDosyalar kasaya ekleniyor...");
            using FragileArchive archive = await FragileArchive.CreateAsync(kasaYolu, options);

            // Örnek klasördeki tüm dosyaları ekle
            int fileCount = await archive.AddDirectoryAsync(SampleDataFolder);

            // Arşivi kaydet
            await archive.SaveAsync();
            Console.WriteLine(); // Çıktıyı iyileştirmek için boş satır

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Kasa başarıyla oluşturuldu: {kasaYolu}");
            Console.WriteLine($"Toplam {fileCount} dosya eklendi.");
            Console.WriteLine($"Kasa boyutu: {FormatFileSize(new FileInfo(kasaYolu).Length)}");
            Console.ResetColor();
        }

        static async Task OpenVaultAsync()
        {
            Console.WriteLine("\n=> Kasa açılıyor ve içeriği inceleniyor...");

            string kasaAdi = "OrnekKasa";
            string kasaYolu = Path.Combine(VaultsFolder, $"{kasaAdi}.vault");
            string sifre = "GuvenliParola123";

            if (!File.Exists(kasaYolu))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Hata: Kasa bulunamadı: {kasaYolu}");
                Console.ResetColor();
                return;
            }

            // Çıkarma klasörünü temizle
            if (Directory.Exists(ExtractFolder))
            {
                Directory.Delete(ExtractFolder, true);
                Directory.CreateDirectory(ExtractFolder);
            }

            FragileOptions options = new()
            {
                Password = sifre,
                EnableEncryption = true,
                EncryptionMethod = EncryptionMethod.AES256,
                CompressionAlgorithm = CompressionAlgorithm.Brotli,
                CompressionLevel = CompressionLevel.Ultra,
                EnableChecksumVerification = true,
                ChecksumAlgorithm = ChecksumAlgorithm.SHA256,
                EnableErrorCorrection = true,
                ErrorCorrectionLevel = 10,
                Extension = ".vault",
                Progress = new Progress<double>(p => ReportProgress("Kasa açılıyor", p))
            };

            try
            {
                using FragileArchive archive = await FragileArchive.OpenAsync(kasaYolu, options);
                Console.WriteLine(); // Boş satır

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Kasa başarıyla açıldı: {kasaYolu}");
                Console.WriteLine($"Dosya sayısı: {archive.Entries.Count}");
                Console.ResetColor();

                // Dosya listesini göster
                Console.WriteLine("\nKasa İçeriği:");
                Console.WriteLine("-------------");

                foreach (FragileArchiveEntry entry in archive.Entries)
                {
                    string typeIcon = entry.IsDirectory ? "📁" : "📄";
                    string sizeInfo = entry.IsDirectory ? "" : $" ({FormatFileSize(entry.Size)})";
                    Console.WriteLine($"{typeIcon} {entry.Path}{sizeInfo}");
                }

                // Dosyaları çıkar
                Console.WriteLine("\nDosyalar çıkarılıyor...");
                options.Progress = new Progress<double>(p => ReportProgress("Dosyalar çıkarılıyor", p));
                await archive.ExtractAllAsync(ExtractFolder);
                Console.WriteLine(); // Boş satır

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Tüm dosyalar şu konuma çıkarıldı: {ExtractFolder}");
                Console.ResetColor();

                // Dosya bütünlüğünü kontrol et
                Console.WriteLine("\nDosya bütünlüğü kontrol ediliyor...");
                bool allFilesOK = true;

                foreach (FragileArchiveEntry entry in archive.Entries)
                {
                    if (!entry.IsDirectory)
                    {
                        string extractedPath = Path.Combine(ExtractFolder, entry.Path);
                        if (!File.Exists(extractedPath))
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"❌ {entry.Path} - Çıkarma başarısız!");
                            Console.ResetColor();
                            allFilesOK = false;
                        }
                        else
                        {
                            long actualSize = new FileInfo(extractedPath).Length;
                            if (actualSize != entry.Size)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"❌ {entry.Path} - Boyut uyuşmazlığı: {FormatFileSize(actualSize)} != {FormatFileSize(entry.Size)}");
                                Console.ResetColor();
                                allFilesOK = false;
                            }
                        }
                    }
                }

                if (allFilesOK)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("✅ Tüm dosyalar başarıyla doğrulandı!");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Kasa açılırken hata oluştu: {ex.Message}");
                Console.ResetColor();
            }
        }

        static async Task CompareCompressionsAsync()
        {
            Console.WriteLine("\n=> Sıkıştırma algoritmaları karşılaştırılıyor...");

            string testFilePath = Path.Combine(SampleDataFolder, "ornek.txt");
            if (!File.Exists(testFilePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Test dosyası bulunamadı!");
                Console.ResetColor();
                return;
            }

            long originalSize = new FileInfo(testFilePath).Length;
            Console.WriteLine($"Orijinal dosya boyutu: {FormatFileSize(originalSize)}");

            // Karşılaştırılacak algoritmalar
            CompressionAlgorithm[] algorithms =
            {
                CompressionAlgorithm.Store,   // Sıkıştırma yok
                CompressionAlgorithm.Deflate, // Standart
                CompressionAlgorithm.Brotli,  // Yüksek oran
            };

            Console.WriteLine("\nAlgoritma      Boyut          Oran    Süre");
            Console.WriteLine("------------------------------------------");

            foreach (CompressionAlgorithm algorithm in algorithms)
            {
                string vaultPath = Path.Combine(VaultsFolder, $"comp_{algorithm}.vault");

                try
                {
                    Stopwatch sw = Stopwatch.StartNew();

                    FragileOptions options = new()
                    {
                        Password = "test123",
                        Extension = ".vault",
                        EnableEncryption = false, // Şifreleme yok (adil karşılaştırma için)
                        CompressionAlgorithm = algorithm,
                        CompressionLevel = CompressionLevel.Ultra
                    };

                    using FragileArchive archive = await FragileArchive.CreateAsync(vaultPath, options);
                    await archive.AddFileAsync(testFilePath);
                    await archive.SaveAsync();

                    sw.Stop();

                    long compressedSize = new FileInfo(vaultPath).Length;
                    double ratio = 0;
                    if (compressedSize > 0)
                    {
                        ratio = (double)originalSize / compressedSize;
                    }

                    string ratioText = algorithm == CompressionAlgorithm.Store ? "1.0x" : $"{ratio:F2}x";
                    string timeText = $"{sw.ElapsedMilliseconds} ms";

                    Console.WriteLine($"{algorithm,-13} {FormatFileSize(compressedSize),-14} {ratioText,-7} {timeText}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{algorithm,-13} Hata: {ex.Message}");
                }
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\nNot: LZMA en iyi sıkıştırma sağlar, LZ4 ise en hızlı seçenektir.");
            Console.ResetColor();
        }

        static async Task CompareEncryptionsAsync()
        {
            Console.WriteLine("\n=> Şifreleme algoritmaları karşılaştırılıyor...");

            string testFilePath = Path.Combine(SampleDataFolder, "ornek.txt");
            if (!File.Exists(testFilePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Test dosyası bulunamadı!");
                Console.ResetColor();
                return;
            }

            // Karşılaştırılacak şifreleme metodları
            EncryptionMethod[] methods =
            {
                EncryptionMethod.AES128,
                EncryptionMethod.AES256,
                EncryptionMethod.ChaCha20,
                EncryptionMethod.Twofish
            };

            Console.WriteLine("\nŞifreleme       Boyut          Süre");
            Console.WriteLine("-----------------------------------");

            foreach (EncryptionMethod method in methods)
            {
                string vaultPath = Path.Combine(VaultsFolder, $"enc_{method}.vault");

                try
                {
                    Stopwatch sw = Stopwatch.StartNew();

                    FragileOptions options = new()
                    {
                        Password = "test123",
                        Extension = ".vault",
                        EnableEncryption = true,
                        EncryptionMethod = method,
                        CompressionAlgorithm = CompressionAlgorithm.Store // Sıkıştırma yok (adil karşılaştırma için)
                    };

                    using FragileArchive archive = await FragileArchive.CreateAsync(vaultPath, options);
                    await archive.AddFileAsync(testFilePath);
                    await archive.SaveAsync();

                    sw.Stop();

                    long encryptedSize = new FileInfo(vaultPath).Length;
                    string timeText = $"{sw.ElapsedMilliseconds} ms";

                    Console.WriteLine($"{method,-14} {FormatFileSize(encryptedSize),-14} {timeText}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{method,-14} Hata: {ex.Message}");
                }
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\nNot: AES-256 en güvenli şifreleme sağlar ve modern donanımda oldukça hızlıdır.");
            Console.ResetColor();
        }

        static void ReportProgress(string operation, double progress)
        {
            int width = 30;
            int completedWidth = (int)(width * progress);

            Console.Write($"\r{operation}: [");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(new string('█', completedWidth));
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(new string('░', width - completedWidth));
            Console.ResetColor();
            Console.Write($"] {progress:P0}");
        }

        static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            double size = bytes;

            while (size >= 1024 && counter < suffixes.Length - 1)
            {
                size /= 1024;
                counter++;
            }

            return $"{size:0.##} {suffixes[counter]}";
        }
    }
}