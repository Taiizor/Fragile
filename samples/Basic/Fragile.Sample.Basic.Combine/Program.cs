using Fragile.Core;
using Fragile.Utils;
using System.Text;

namespace Fragile.Sample.Basic.Combine
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Combine Sample");
            Console.WriteLine("======================");
            Console.WriteLine("Bu örnek, parçalanmış arşiv dosyalarının nasıl birleştirileceğini gösterir");

            // Örnek dizin oluştur
            string sampleDir = "Sample";
            Directory.CreateDirectory(sampleDir);

            // Örnek dosyalar oluştur
            string textFilePath = Path.Combine(sampleDir, "ornek.txt");
            File.WriteAllText(textFilePath, "Bu, Fragile için örnek bir metin dosyasıdır.");

            string largeFilePath = Path.Combine(sampleDir, "buyuk_dosya.txt");
            await CreateLargeFileAsync(largeFilePath, 1 * 1024 * 1024); // 1MB boyutunda

            // Alt klasör oluştur ve içine dosya ekle
            string subfolderPath = Path.Combine(sampleDir, "alt_klasor");
            Directory.CreateDirectory(subfolderPath);
            File.WriteAllText(Path.Combine(subfolderPath, "benioku.txt"), "Bu alt klasördeki bir dosyadır.");

            // Parçalanmış arşiv dizinini oluştur
            string splitDir = "Split";
            Directory.CreateDirectory(splitDir);

            // Birleştirilmiş arşiv dizinini oluştur
            string combinedDir = "Combined";
            Directory.CreateDirectory(combinedDir);

            string archivePath = Path.Combine(splitDir, "bolunmus_arsiv.frgl");

            try
            {
                // Parçalanmış arşiv oluştur - 200KB boyutunda parçalar olsun
                Console.WriteLine("\nParçalanmış arşiv oluşturuluyor...");

                long splitSize = 200 * 1024; // 200KB
                FragileArchivePartCollection parts = await FragileUtility.CreateSplitArchiveAsync(
                    sampleDir,
                    archivePath,
                    recursive: true,
                    splitSize: splitSize);

                Console.WriteLine($"Arşiv {parts.Count} parçaya bölündü:");
                foreach (FragileArchivePart part in parts)
                {
                    Console.WriteLine($" - {Path.GetFileName(part.Path)} ({FormatFileSize(part.Size)})");
                }

                // Dosya parçalarının birleştirilmesi
                Console.WriteLine("\nArşiv parçaları birleştiriliyor...");

                string firstPartPath = Path.Combine(splitDir, Path.GetFileName(parts[0].Path));
                string combinedArchivePath = Path.Combine(combinedDir, "birlestirilmis_arsiv.frgl");

                await FragileUtility.CombinePartsAsync(firstPartPath, combinedArchivePath);

                Console.WriteLine($"Parçalar başarıyla birleştirildi: {combinedArchivePath}");
                Console.WriteLine($"Birleştirilmiş arşiv boyutu: {FormatFileSize(new FileInfo(combinedArchivePath).Length)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hata: {ex.Message}");
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Belirtilen boyutta test amaçlı büyük bir dosya oluşturur
        /// </summary>
        private static async Task CreateLargeFileAsync(string filePath, int sizeInBytes)
        {
            using (FileStream stream = File.Create(filePath))
            {
                // Her iterasyonda 4KB yazarak istenen boyuta ulaşana kadar devam et
                byte[] buffer = new byte[4096];
                Random rnd = new();

                int bytesWritten = 0;
                while (bytesWritten < sizeInBytes)
                {
                    rnd.NextBytes(buffer);
                    int bytesToWrite = Math.Min(buffer.Length, sizeInBytes - bytesWritten);
                    await stream.WriteAsync(buffer, 0, bytesToWrite);
                    bytesWritten += bytesToWrite;
                }
            }

            Console.WriteLine($"Oluşturulan test dosyası: {filePath} ({FormatFileSize(sizeInBytes)})");
        }

        /// <summary>
        /// Bayt cinsinden boyutu okunabilir bir formata dönüştürür (KB, MB, GB)
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;

            while (number >= 1024 && counter < suffixes.Length - 1)
            {
                number /= 1024;
                counter++;
            }

            return $"{number:0.##} {suffixes[counter]}";
        }
    }
}