using Fragile.Encryption;
using System.Diagnostics;
using System.Text;

namespace Fragile.Samples.Encryption
{
    /// <summary>
    /// Fragile şifreleme özelliklerini gösteren örnek uygulama
    /// </summary>
    public class Program
    {
        // Test sonuçlarını saklamak için koleksiyon
        private static List<EncryptionTestResult> _testResults = new();

        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Şifreleme Örneği");
            Console.WriteLine("========================");

            try
            {
                // Geçici dizin oluştur
                string tempDir = Path.Combine(Path.GetTempPath(), "FragileEncryptionSample");
                Directory.CreateDirectory(tempDir);

                // Test dosya boyutları
                var fileSizes = new Dictionary<string, int>
                {
                    { "Küçük", 200 },        // 200 byte civarı
                    { "Orta", 100 * 1024 },  // 100 KB
                    { "Büyük", 1024 * 1024 } // 1 MB
                };

                // Şifreleme şifresi tanımla
                string password = "GuvenliParola123!";
                Console.WriteLine($"Şifreleme parolası: {password}");

                // Test tüm şifreleme metotları için yapılacak
                string outputDir = Path.Combine(tempDir, "output");
                Directory.CreateDirectory(outputDir);

                // Küçük boyutlu test için örnek içerik oluştur
                string testFilePath = Path.Combine(tempDir, "gizli_veri.txt");
                string originalContent = "Bu metin, Fragile kütüphanesi tarafından şifrelenecek ve korunacak gizli içeriktir.\n" +
                                        "Kredi kartı numarası: 1234-5678-9012-3456\n" +
                                        "Güvenlik kodu: 123\n" +
                                        "Parola: GizliParola123!";

                File.WriteAllText(testFilePath, originalContent);
                Console.WriteLine($"Örnek dosya oluşturuldu: {testFilePath}");
                Console.WriteLine("\nOrijinal içerik:");
                Console.WriteLine("------------------------------");
                Console.WriteLine(originalContent);
                Console.WriteLine("------------------------------");

                // Şifreleme metodlarını test et
                await TestEncryption(testFilePath, outputDir, EncryptionMethod.None, password);
                await TestEncryption(testFilePath, outputDir, EncryptionMethod.AES128, password);
                await TestEncryption(testFilePath, outputDir, EncryptionMethod.AES256, password);

                // Yanlış şifre ile deşifrelemeyi göster
                await TestWrongPassword(testFilePath, outputDir, EncryptionMethod.AES256, password);

                // Farklı boyutlardaki dosyalar için performans testi
                Console.WriteLine("\n\n=== PERFORMANS TESTİ ===");
                foreach (var size in fileSizes)
                {
                    Console.WriteLine($"\n>> {size.Key} Boyutlu Dosya Testi ({size.Value:N0} bayt)");
                    
                    if (size.Key != "Küçük") // Küçük dosyayı zaten oluşturduk
                    {
                        // Test dosyası oluştur
                        string sizeTestFilePath = Path.Combine(tempDir, $"test_{size.Key.ToLowerInvariant()}.txt");
                        await CreateSampleFile(sizeTestFilePath, size.Value);
                        Console.WriteLine($"Test dosyası oluşturuldu: {sizeTestFilePath}");
                    }
                    else
                    {
                        // Küçük test için mevcut dosyayı kullan
                        Console.WriteLine($"Mevcut küçük dosya kullanılıyor: {testFilePath}");
                    }

                    string sizeTestPath = size.Key == "Küçük" ? testFilePath : Path.Combine(tempDir, $"test_{size.Key.ToLowerInvariant()}.txt");
                    
                    // Tüm şifreleme yöntemleri için performans testi
                    foreach (EncryptionMethod method in Enum.GetValues<EncryptionMethod>())
                    {
                        await TestEncryptionPerformance(sizeTestPath, outputDir, method, password, size.Key);
                    }
                }

                // Özet bilgilerini göster
                DisplaySummary();

                Console.WriteLine("\nŞifreleme testi tamamlandı.");
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
        /// Test sonuçlarını özetler ve gösterir
        /// </summary>
        private static void DisplaySummary()
        {
            if (_testResults.Count == 0)
                return;

            Console.WriteLine("\n\n=== SONUÇLARIN ÖZETİ ===");

            // Başarılı testler
            var successfulTests = _testResults.Where(r => r.IsSuccessful).ToList();
            Console.WriteLine($"\nBaşarılı Testler: {successfulTests.Count}");

            if (successfulTests.Any())
            {
                // Her boyut için en hızlı şifreleme işlemi
                var bySize = successfulTests.GroupBy(t => t.FileSize);
                
                foreach (var sizeGroup in bySize)
                {
                    Console.WriteLine($"\n>> {sizeGroup.Key} boyutlu dosya için sonuçlar:");
                    
                    // En hızlı şifreleme
                    var fastestEncryption = sizeGroup.OrderBy(r => r.EncryptionTime).First();
                    Console.WriteLine($"En Hızlı Şifreleme: {fastestEncryption.Method} - {fastestEncryption.EncryptionTime} ms");
                    
                    // En hızlı şifre çözme
                    var fastestDecryption = sizeGroup.OrderBy(r => r.DecryptionTime).First();
                    Console.WriteLine($"En Hızlı Şifre Çözme: {fastestDecryption.Method} - {fastestDecryption.DecryptionTime} ms");
                    
                    // En az ek yük
                    var leastOverhead = sizeGroup.OrderBy(r => r.Overhead).First();
                    Console.WriteLine($"En Az Ek Yük: {leastOverhead.Method} - {leastOverhead.Overhead:N0} bayt");
                }
            }

            // Başarısız testler
            var failedTests = _testResults.Where(r => !r.IsSuccessful && !r.IsExpectedFailure).ToList();
            Console.WriteLine($"\nBaşarısız Testler: {failedTests.Count}");

            // Desteklenmeyen algoritmalar
            var unsupportedAlgorithms = failedTests
                .Where(r => r.ErrorMessage?.Contains("not supported") == true)
                .Select(r => r.Method)
                .Distinct()
                .ToList();

            if (unsupportedAlgorithms.Any())
            {
                Console.WriteLine("Desteklenmeyen Şifreleme Yöntemleri: " + string.Join(", ", unsupportedAlgorithms));
            }
        }

        /// <summary>
        /// Belirtilen şifreleme metodu ile şifreleme testi yapar
        /// </summary>
        private static async Task TestEncryption(string inputFilePath, string outputDir,
            EncryptionMethod method, string password)
        {
            Console.WriteLine($"\n\nTest: {method}");
            Console.WriteLine("===============================");

            try
            {
                // Şifrelenmiş dosya yolu
                string encryptedFilePath = Path.Combine(outputDir, $"encrypted_{method}.bin");

                // Şifresi çözülmüş dosya yolu
                string decryptedFilePath = Path.Combine(outputDir, $"decrypted_{method}.txt");

                // Test sonucu nesnesi
                EncryptionTestResult result = new()
                {
                    Method = method,
                    FileSize = "Küçük",
                    IsSuccessful = false
                };

                // Şifreleme sağlayıcısını oluştur
                EncryptionProvider provider = EncryptionProvider.Create(method, password);

                // Dosyayı şifrele
                Stopwatch stopwatch = Stopwatch.StartNew();

                using (FileStream inputStream = new(inputFilePath, FileMode.Open, FileAccess.Read))
                using (FileStream outputStream = new(encryptedFilePath, FileMode.Create, FileAccess.Write))
                {
                    Progress<double> progress = new(value =>
                    {
                        Console.Write($"\rŞifreleme: %{value * 100:F1}");
                    });

                    await provider.EncryptAsync(inputStream, outputStream, progress);
                }

                stopwatch.Stop();
                long encryptionTime = stopwatch.ElapsedMilliseconds;
                result.EncryptionTime = encryptionTime;
                Console.WriteLine($"\rŞifreleme: %100 - Tamamlandı ({encryptionTime} ms)");

                // Şifreli dosya bilgilerini göster
                FileInfo inputInfo = new(inputFilePath);
                FileInfo encryptedInfo = new(encryptedFilePath);
                
                long originalSize = inputInfo.Length;
                long encryptedSize = encryptedInfo.Length;
                long overhead = encryptedSize - originalSize;
                
                result.OriginalSize = originalSize;
                result.EncryptedSize = encryptedSize;
                result.Overhead = overhead;

                Console.WriteLine($"Orijinal boyut: {originalSize:N0} bayt");
                Console.WriteLine($"Şifrelenmiş boyut: {encryptedSize:N0} bayt");
                Console.WriteLine($"Ek yük: {overhead:N0} bayt");

                // Şifreli içeriği byte olarak göster (ilk 100 byte)
                byte[] encryptedBytes = File.ReadAllBytes(encryptedFilePath);

                Console.WriteLine("\nŞifrelenmiş içerik (ilk 100 byte, hex):");
                Console.WriteLine("------------------------------");
                Console.WriteLine(BytesToHex(encryptedBytes.AsSpan(0, Math.Min(100, encryptedBytes.Length))));
                Console.WriteLine("------------------------------");

                // Şifreyi çöz
                stopwatch.Restart();

                using (FileStream inputStream = new(encryptedFilePath, FileMode.Open, FileAccess.Read))
                using (FileStream outputStream = new(decryptedFilePath, FileMode.Create, FileAccess.Write))
                {
                    Progress<double> progress = new(value =>
                    {
                        Console.Write($"\rŞifre çözme: %{value * 100:F1}");
                    });

                    await provider.DecryptAsync(inputStream, outputStream, progress);
                }

                stopwatch.Stop();
                long decryptionTime = stopwatch.ElapsedMilliseconds;
                result.DecryptionTime = decryptionTime;
                Console.WriteLine($"\rŞifre çözme: %100 - Tamamlandı ({decryptionTime} ms)");

                // Şifresi çözülmüş içeriği göster
                string decryptedContent = File.ReadAllText(decryptedFilePath);
                Console.WriteLine("\nŞifresi çözülmüş içerik:");
                Console.WriteLine("------------------------------");
                Console.WriteLine(decryptedContent);
                Console.WriteLine("------------------------------");

                // Doğrulama
                bool isValid = File.ReadAllText(inputFilePath) == decryptedContent;
                result.IsSuccessful = isValid;
                Console.WriteLine($"Doğrulama: {(isValid ? "Başarılı ✓" : "Başarısız ✗")}");
                
                // Test sonucunu listeye ekle
                _testResults.Add(result);
            }
            catch (NotSupportedException nse)
            {
                _testResults.Add(new EncryptionTestResult 
                { 
                    Method = method, 
                    FileSize = "Küçük", 
                    IsSuccessful = false, 
                    ErrorMessage = nse.Message 
                });
                Console.WriteLine($"Desteklenmiyor: {nse.Message}");
            }
            catch (Exception ex)
            {
                _testResults.Add(new EncryptionTestResult 
                { 
                    Method = method, 
                    FileSize = "Küçük", 
                    IsSuccessful = false, 
                    ErrorMessage = ex.Message 
                });
                Console.WriteLine($"Hata: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Belirtilen şifreleme metodu ile performans testi yapar
        /// </summary>
        private static async Task TestEncryptionPerformance(string inputFilePath, string outputDir,
            EncryptionMethod method, string password, string sizeCategory)
        {
            Console.WriteLine($"\nPerformans Testi: {method}");
            Console.WriteLine("--------------------------------");

            try
            {
                // Şifrelenmiş ve şifresi çözülmüş dosya yolları
                string encryptedFilePath = Path.Combine(outputDir, $"perf_{sizeCategory.ToLowerInvariant()}_{method}.bin");
                string decryptedFilePath = Path.Combine(outputDir, $"perf_{sizeCategory.ToLowerInvariant()}_{method}_decrypted.txt");

                // Test sonucu nesnesi
                EncryptionTestResult result = new()
                {
                    Method = method,
                    FileSize = sizeCategory,
                    IsSuccessful = false
                };

                // Şifreleme sağlayıcısını oluştur
                EncryptionProvider provider = EncryptionProvider.Create(method, password);

                // Dosya bilgileri
                FileInfo inputInfo = new(inputFilePath);
                long originalSize = inputInfo.Length;
                result.OriginalSize = originalSize;

                // Dosyayı şifrele
                Stopwatch stopwatch = Stopwatch.StartNew();

                using (FileStream inputStream = new(inputFilePath, FileMode.Open, FileAccess.Read))
                using (FileStream outputStream = new(encryptedFilePath, FileMode.Create, FileAccess.Write))
                {
                    await provider.EncryptAsync(inputStream, outputStream);
                }

                stopwatch.Stop();
                long encryptionTime = stopwatch.ElapsedMilliseconds;
                result.EncryptionTime = encryptionTime;

                // Şifreli dosya bilgileri
                FileInfo encryptedInfo = new(encryptedFilePath);
                long encryptedSize = encryptedInfo.Length;
                long overhead = encryptedSize - originalSize;
                
                result.EncryptedSize = encryptedSize;
                result.Overhead = overhead;

                // Şifreyi çöz
                stopwatch.Restart();

                using (FileStream inputStream = new(encryptedFilePath, FileMode.Open, FileAccess.Read))
                using (FileStream outputStream = new(decryptedFilePath, FileMode.Create, FileAccess.Write))
                {
                    await provider.DecryptAsync(inputStream, outputStream);
                }

                stopwatch.Stop();
                long decryptionTime = stopwatch.ElapsedMilliseconds;
                result.DecryptionTime = decryptionTime;

                // Doğrulama
                bool isValid = await VerifyDecryption(inputFilePath, decryptedFilePath);
                result.IsSuccessful = isValid;

                // Sonuçları göster
                Console.WriteLine($"Orijinal boyut: {originalSize:N0} bayt");
                Console.WriteLine($"Şifrelenmiş boyut: {encryptedSize:N0} bayt");
                Console.WriteLine($"Ek yük: {overhead:N0} bayt (%{(double)overhead / originalSize * 100:F2})");
                Console.WriteLine($"Şifreleme süresi: {encryptionTime} ms");
                Console.WriteLine($"Şifre çözme süresi: {decryptionTime} ms");
                Console.WriteLine($"Doğrulama: {(isValid ? "Başarılı ✓" : "Başarısız ✗")}");
                
                // Test sonucunu listeye ekle
                _testResults.Add(result);
            }
            catch (NotSupportedException nse)
            {
                _testResults.Add(new EncryptionTestResult 
                { 
                    Method = method, 
                    FileSize = sizeCategory, 
                    IsSuccessful = false, 
                    ErrorMessage = nse.Message 
                });
                Console.WriteLine($"Desteklenmiyor: {nse.Message}");
            }
            catch (Exception ex)
            {
                _testResults.Add(new EncryptionTestResult 
                { 
                    Method = method, 
                    FileSize = sizeCategory, 
                    IsSuccessful = false, 
                    ErrorMessage = ex.Message 
                });
                Console.WriteLine($"Hata: {ex.Message}");
            }
        }

        /// <summary>
        /// Yanlış şifre ile şifre çözmeyi dener
        /// </summary>
        private static async Task TestWrongPassword(string inputFilePath, string outputDir,
            EncryptionMethod method, string correctPassword)
        {
            Console.WriteLine($"\n\nTest: {method} - Yanlış Şifre ile Çözmeyi Deneme");
            Console.WriteLine("===============================");

            try
            {
                // Şifrelenmiş dosya yolu
                string encryptedFilePath = Path.Combine(outputDir, $"encrypted_{method}_wrong.bin");

                // Şifresi çözülmüş dosya yolu
                string decryptedFilePath = Path.Combine(outputDir, $"decrypted_{method}_wrong.txt");

                // Test sonucu nesnesi
                EncryptionTestResult result = new()
                {
                    Method = method,
                    FileSize = "Küçük",
                    IsSuccessful = false,
                    IsExpectedFailure = true
                };

                // Doğru şifre ile şifrele
                EncryptionProvider encryptProvider = EncryptionProvider.Create(method, correctPassword);

                using (FileStream inputStream = new(inputFilePath, FileMode.Open, FileAccess.Read))
                using (FileStream outputStream = new(encryptedFilePath, FileMode.Create, FileAccess.Write))
                {
                    await encryptProvider.EncryptAsync(inputStream, outputStream);
                }

                Console.WriteLine("Dosya doğru şifre ile şifrelendi.");

                // Yanlış şifre
                string wrongPassword = "YanlisParola123!";
                Console.WriteLine($"Doğru şifre: {correctPassword}");
                Console.WriteLine($"Yanlış şifre: {wrongPassword}");

                // Yanlış şifre ile şifre çözmeyi dene
                EncryptionProvider decryptProvider = EncryptionProvider.Create(method, wrongPassword);

                try
                {
                    using (FileStream inputStream = new(encryptedFilePath, FileMode.Open, FileAccess.Read))
                    using (FileStream outputStream = new(decryptedFilePath, FileMode.Create, FileAccess.Write))
                    {
                        await decryptProvider.DecryptAsync(inputStream, outputStream);
                    }

                    // Şifresi yanlış çözülmüş içeriği göster
                    string decryptedContent = File.ReadAllText(decryptedFilePath);
                    Console.WriteLine("\nYanlış şifre ile çözülmüş içerik:");
                    Console.WriteLine("------------------------------");
                    Console.WriteLine(decryptedContent);
                    Console.WriteLine("------------------------------");

                    // Doğrulama
                    bool isValid = File.ReadAllText(inputFilePath) == decryptedContent;
                    result.IsSuccessful = isValid;
                    Console.WriteLine($"Doğrulama: {(isValid ? "Başarılı" : "Başarısız (beklenen)")}");
                }
                catch (Exception ex)
                {
                    result.ErrorMessage = ex.Message;
                    Console.WriteLine($"\nŞifre çözme hatası (beklenen): {ex.Message}");
                    Console.WriteLine("Bu, yanlış şifre kullanıldığında beklenen davranıştır.");
                }
                
                // Test sonucunu listeye ekle
                _testResults.Add(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hata: {ex.Message}");
            }
        }

        /// <summary>
        /// Açılan dosyanın orijinali ile aynı olup olmadığını doğrular
        /// </summary>
        private static async Task<bool> VerifyDecryption(string originalFilePath, string decryptedFilePath)
        {
            using FileStream originalStream = new(originalFilePath, FileMode.Open, FileAccess.Read);
            using FileStream decryptedStream = new(decryptedFilePath, FileMode.Open, FileAccess.Read);
            
            if (originalStream.Length != decryptedStream.Length)
            {
                return false;
            }

            const int bufferSize = 81920; // 80 KB
            byte[] originalBuffer = new byte[bufferSize];
            byte[] decryptedBuffer = new byte[bufferSize];

            int bytesRead;
            while ((bytesRead = await originalStream.ReadAsync(originalBuffer, 0, bufferSize)) > 0)
            {
                await decryptedStream.ReadAsync(decryptedBuffer, 0, bytesRead);

                for (int i = 0; i < bytesRead; i++)
                {
                    if (originalBuffer[i] != decryptedBuffer[i])
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

        /// <summary>
        /// Byte dizisini heksadesimal string'e dönüştürür
        /// </summary>
        private static string BytesToHex(ReadOnlySpan<byte> bytes)
        {
            StringBuilder hex = new(bytes.Length * 3);

            for (int i = 0; i < bytes.Length; i++)
            {
                hex.Append($"{bytes[i]:X2} ");

                // Her 16 byte'da bir satır sonu ekle
                if ((i + 1) % 16 == 0)
                {
                    hex.AppendLine();
                }
            }

            return hex.ToString();
        }
    }

    /// <summary>
    /// Şifreleme testi sonuçlarını saklamak için sınıf
    /// </summary>
    public class EncryptionTestResult
    {
        public EncryptionMethod Method { get; set; }
        public string FileSize { get; set; }
        public long OriginalSize { get; set; }
        public long EncryptedSize { get; set; }
        public long Overhead { get; set; }
        public long EncryptionTime { get; set; }
        public long DecryptionTime { get; set; }
        public bool IsSuccessful { get; set; }
        public bool IsExpectedFailure { get; set; }
        public string ErrorMessage { get; set; }
    }
}
