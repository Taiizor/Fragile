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

                // Örnek dosya oluştur
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

                // Şifreleme şifresi tanımla
                string password = "GuvenliParola123!";
                Console.WriteLine($"\nŞifreleme parolası: {password}");

                // Test tüm şifreleme metotları için yapılacak
                string outputDir = Path.Combine(tempDir, "output");
                Directory.CreateDirectory(outputDir);

                // AES128 ve AES256 şifreleme yöntemlerini test et
                await TestEncryption(testFilePath, outputDir, EncryptionMethod.None, password);
                await TestEncryption(testFilePath, outputDir, EncryptionMethod.AES128, password);
                await TestEncryption(testFilePath, outputDir, EncryptionMethod.AES256, password);

                // Yanlış şifre ile deşifrelemeyi göster
                await TestWrongPassword(testFilePath, outputDir, EncryptionMethod.AES256, password);

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
                Console.WriteLine($"\rŞifreleme: %100 - Tamamlandı ({stopwatch.ElapsedMilliseconds} ms)");

                // Şifreli dosya bilgilerini göster
                FileInfo inputInfo = new(inputFilePath);
                FileInfo encryptedInfo = new(encryptedFilePath);

                Console.WriteLine($"Orijinal boyut: {inputInfo.Length:N0} bayt");
                Console.WriteLine($"Şifrelenmiş boyut: {encryptedInfo.Length:N0} bayt");
                Console.WriteLine($"Ek yük: {encryptedInfo.Length - inputInfo.Length:N0} bayt");

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
                Console.WriteLine($"\rŞifre çözme: %100 - Tamamlandı ({stopwatch.ElapsedMilliseconds} ms)");

                // Şifresi çözülmüş içeriği göster
                string decryptedContent = File.ReadAllText(decryptedFilePath);
                Console.WriteLine("\nŞifresi çözülmüş içerik:");
                Console.WriteLine("------------------------------");
                Console.WriteLine(decryptedContent);
                Console.WriteLine("------------------------------");

                // Doğrulama
                bool isValid = File.ReadAllText(inputFilePath) == decryptedContent;
                Console.WriteLine($"Doğrulama: {(isValid ? "Başarılı ✓" : "Başarısız ✗")}");
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
                    Console.WriteLine($"Doğrulama: {(isValid ? "Başarılı" : "Başarısız (beklenen)")}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nŞifre çözme hatası (beklenen): {ex.Message}");
                    Console.WriteLine("Bu, yanlış şifre kullanıldığında beklenen davranıştır.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hata: {ex.Message}");
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
}
