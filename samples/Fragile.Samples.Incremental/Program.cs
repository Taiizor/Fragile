using Fragile.Compression;
using Fragile.Core;
using Fragile.Models;
using System.Diagnostics;
using System.Text;

namespace Fragile.Samples.Incremental
{
    /// <summary>
    /// Fragile kütüphanesinin arşivleri kademeli olarak oluşturma, güncelleme ve yönetme 
    /// özelliklerini gösteren örnek uygulama
    /// </summary>
    public class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Kademeli Arşivleme Örneği");
            Console.WriteLine("=================================");

            try
            {
                // Geçici dizin oluştur
                string tempDir = Path.Combine(Path.GetTempPath(), "FragileIncrementalSample");
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
                Directory.CreateDirectory(tempDir);

                // Test dizini
                string sourceDir = Path.Combine(tempDir, "Source");
                Directory.CreateDirectory(sourceDir);

                // Arşiv dosyası
                string archivePath = Path.Combine(tempDir, "incremental_archive.frgl");

                // Kademeli arşivleme testleri
                await RunIncrementalArchiveTests(sourceDir, archivePath);
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
        /// Kademeli arşivleme testlerini çalıştırır
        /// </summary>
        private static async Task RunIncrementalArchiveTests(string sourceDir, string archivePath)
        {
            Console.WriteLine("\n🔄 Kademeli Arşivleme Testleri");
            Console.WriteLine("==============================");

            // İlk versiyonu oluştur
            await CreateInitialFiles(sourceDir, "v1");
            await CreateInitialArchive(sourceDir, archivePath);

            // İnceleme
            await ExamineArchive(archivePath);

            // İlk güncelleme - Yeni dosyalar ekle
            await AddNewFiles(sourceDir, "v2");
            await UpdateArchiveWithNewFiles(sourceDir, archivePath);

            // İkinci güncelleme - Mevcut dosyaları değiştir
            await ModifyExistingFiles(sourceDir);
            await UpdateArchiveWithModifiedFiles(sourceDir, archivePath);

            // Üçüncü güncelleme - Bazı dosyaları sil
            await DeleteSomeFiles(sourceDir);
            await UpdateArchiveWithDeletedFiles(sourceDir, archivePath);

            // Son güncelleme - Karma değişiklikler
            await MixedChanges(sourceDir, "v3");
            await UpdateArchiveWithMixedChanges(sourceDir, archivePath);

            // Son durumu incele
            await ExamineArchive(archivePath);

            // Arşiv versiyonları arasında gezin
            await TestArchiveVersions(archivePath, Path.Combine(Path.GetDirectoryName(archivePath), "extracted"));
        }

        /// <summary>
        /// İlk dosya setini oluşturur
        /// </summary>
        private static async Task CreateInitialFiles(string sourceDir, string version)
        {
            Console.WriteLine($"\n📁 İlk dosya seti oluşturuluyor ({version})...");

            // Örnek klasör yapısı
            Directory.CreateDirectory(Path.Combine(sourceDir, "docs"));
            Directory.CreateDirectory(Path.Combine(sourceDir, "images"));
            Directory.CreateDirectory(Path.Combine(sourceDir, "data"));

            // Metin dosyaları
            await File.WriteAllTextAsync(Path.Combine(sourceDir, "readme.txt"), $"Bu bir örnek readme dosyasıdır. Versiyon: {version}");
            await File.WriteAllTextAsync(Path.Combine(sourceDir, "docs", "document1.txt"), $"Belge 1 içeriği. Versiyon: {version}");
            await File.WriteAllTextAsync(Path.Combine(sourceDir, "docs", "document2.txt"), $"Belge 2 içeriği. Versiyon: {version}");

            // Daha büyük dosyalar
            await CreateTestFile(Path.Combine(sourceDir, "data", "data1.dat"), 100 * 1024, $"data1-{version}"); // 100 KB
            await CreateTestFile(Path.Combine(sourceDir, "data", "data2.dat"), 200 * 1024, $"data2-{version}"); // 200 KB

            // Dosya sayısını kontrol et
            int fileCount = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories).Length;
            Console.WriteLine($"Oluşturulan dosya sayısı: {fileCount}");
        }

        /// <summary>
        /// İlk arşivi oluşturur
        /// </summary>
        private static async Task CreateInitialArchive(string sourceDir, string archivePath)
        {
            Console.WriteLine("\n📦 İlk arşiv oluşturuluyor...");

            Stopwatch timer = Stopwatch.StartNew();

            // Arşiv oluştur
            using (FragileArchive archive = new(archivePath, FragileArchiveMode.Create))
            {
                // Dizini arşive ekle
                archive.AddDirectory(sourceDir, "", new AddDirectoryOptions
                {
                    SearchOption = SearchOption.AllDirectories,
                    CompressionAlgorithm = CompressionAlgorithm.Deflate,
                    CompressionLevel = CompressionLevel.Normal
                });

                // Arşiv meta verilerini ayarla
                archive.Metadata.Version = "1.0";
                archive.Metadata.Description = "İlk arşiv versiyonu";
                archive.Metadata.CreationTime = DateTime.Now;
                archive.Metadata.LastModifiedTime = DateTime.Now;
                archive.Metadata.SetCustomProperty("IncrementalVersion", "1");

                // Arşivi kaydet
                archive.Save();
            }

            timer.Stop();

            // Arşiv bilgisini göster
            FileInfo archiveInfo = new(archivePath);
            Console.WriteLine($"Arşiv oluşturuldu: {archiveInfo.Name}, {archiveInfo.Length:N0} bayt");
            Console.WriteLine($"İşlem süresi: {timer.ElapsedMilliseconds:N0} ms");
        }

        /// <summary>
        /// Arşiv içeriğini inceler
        /// </summary>
        private static async Task ExamineArchive(string archivePath)
        {
            Console.WriteLine("\n🔍 Arşiv inceleniyor...");

            using (FragileArchive archive = new(archivePath, FragileArchiveMode.Read))
            {
                // Arşiv meta verilerini göster
                Console.WriteLine("\nArşiv Meta Verileri:");
                Console.WriteLine($"  Versiyon: {archive.Metadata.Version}");
                Console.WriteLine($"  Açıklama: {archive.Metadata.Description}");
                Console.WriteLine($"  Oluşturulma: {archive.Metadata.CreationTime}");
                Console.WriteLine($"  Son değişiklik: {archive.Metadata.LastModifiedTime}");
                
                if (archive.Metadata.HasCustomProperty("IncrementalVersion"))
                {
                    Console.WriteLine($"  Kademeli Versiyon: {archive.Metadata.GetCustomProperty("IncrementalVersion")}");
                }
                
                // Arşiv girdilerini göster
                Console.WriteLine("\nArşiv Girdileri:");
                int fileCount = 0;
                int dirCount = 0;
                long totalSize = 0;
                long compressedSize = 0;

                foreach (var entry in archive.Entries)
                {
                    if (entry.IsDirectory)
                    {
                        dirCount++;
                    }
                    else
                    {
                        fileCount++;
                        totalSize += entry.Size;
                        compressedSize += entry.CompressedSize;
                    }
                }

                Console.WriteLine($"  Toplam girdiler: {archive.Entries.Count}");
                Console.WriteLine($"  Klasörler: {dirCount}");
                Console.WriteLine($"  Dosyalar: {fileCount}");
                Console.WriteLine($"  Toplam boyut: {totalSize:N0} bayt");
                Console.WriteLine($"  Sıkıştırılmış boyut: {compressedSize:N0} bayt");

                if (totalSize > 0)
                {
                    double ratio = (1.0 - ((double)compressedSize / totalSize)) * 100;
                    Console.WriteLine($"  Sıkıştırma oranı: %{ratio:F2}");
                }
            }
        }

        /// <summary>
        /// Arşive yeni dosyalar ekler
        /// </summary>
        private static async Task AddNewFiles(string sourceDir, string version)
        {
            Console.WriteLine($"\n📄 Yeni dosyalar ekleniyor ({version})...");

            // Yeni klasörler oluştur
            Directory.CreateDirectory(Path.Combine(sourceDir, "scripts"));
            Directory.CreateDirectory(Path.Combine(sourceDir, "configs"));

            // Yeni dosyalar ekle
            await File.WriteAllTextAsync(Path.Combine(sourceDir, "scripts", "script1.js"), $"console.log('Bu bir test scriptidir. Versiyon: {version}');");
            await File.WriteAllTextAsync(Path.Combine(sourceDir, "configs", "config1.json"), $"{{ \"version\": \"{version}\", \"name\": \"Test Config\" }}");
            await File.WriteAllTextAsync(Path.Combine(sourceDir, "configs", "config2.xml"), $"<config version=\"{version}\"><name>Test Config</name></config>");

            // Yeni bir büyük dosya ekle
            await CreateTestFile(Path.Combine(sourceDir, "data", "data3.dat"), 300 * 1024, $"data3-{version}"); // 300 KB

            // Dosya sayısını kontrol et
            int newFileCount = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories).Length;
            Console.WriteLine($"Güncel dosya sayısı: {newFileCount}");
        }

        /// <summary>
        /// Arşivi yeni dosyalarla günceller
        /// </summary>
        private static async Task UpdateArchiveWithNewFiles(string sourceDir, string archivePath)
        {
            Console.WriteLine("\n🔄 Arşiv yeni dosyalarla güncelleniyor...");

            Stopwatch timer = Stopwatch.StartNew();

            // Arşivi aç ve güncelle
            using (FragileArchive archive = new(archivePath, FragileArchiveMode.Update))
            {
                // Yalnızca yeni dosyaları kontrol et ve ekle
                Console.WriteLine("Yeni dosyalar ekleniyor...");
                
                // scripts klasörü
                string scriptsDir = Path.Combine(sourceDir, "scripts");
                if (Directory.Exists(scriptsDir))
                {
                    archive.AddDirectory(scriptsDir, "scripts", new AddDirectoryOptions
                    {
                        SearchOption = SearchOption.AllDirectories,
                        CompressionAlgorithm = CompressionAlgorithm.Deflate,
                        CompressionLevel = CompressionLevel.Normal
                    });
                }
                
                // configs klasörü
                string configsDir = Path.Combine(sourceDir, "configs");
                if (Directory.Exists(configsDir))
                {
                    archive.AddDirectory(configsDir, "configs", new AddDirectoryOptions
                    {
                        SearchOption = SearchOption.AllDirectories,
                        CompressionAlgorithm = CompressionAlgorithm.Deflate,
                        CompressionLevel = CompressionLevel.Normal
                    });
                }
                
                // Sadece yeni veri dosyasını ekle
                string newDataFile = Path.Combine(sourceDir, "data", "data3.dat");
                if (File.Exists(newDataFile))
                {
                    archive.AddFile(newDataFile, "data/data3.dat");
                }

                // Meta verileri güncelle
                archive.Metadata.Version = "1.1";
                archive.Metadata.Description = "Yeni dosyalar eklendi";
                archive.Metadata.LastModifiedTime = DateTime.Now;
                archive.Metadata.SetCustomProperty("IncrementalVersion", "2");

                // Arşivi kaydet
                archive.Save();
            }

            timer.Stop();

            // Arşiv bilgisini göster
            FileInfo archiveInfo = new(archivePath);
            Console.WriteLine($"Arşiv güncellendi: {archiveInfo.Name}, {archiveInfo.Length:N0} bayt");
            Console.WriteLine($"İşlem süresi: {timer.ElapsedMilliseconds:N0} ms");
        }

        /// <summary>
        /// Mevcut dosyaları değiştirir
        /// </summary>
        private static async Task ModifyExistingFiles(string sourceDir)
        {
            Console.WriteLine("\n✏️ Mevcut dosyalar değiştiriliyor...");

            // Metin dosyaları güncelle
            string readmePath = Path.Combine(sourceDir, "readme.txt");
            if (File.Exists(readmePath))
            {
                string content = await File.ReadAllTextAsync(readmePath);
                await File.WriteAllTextAsync(readmePath, content + "\nBu satır değiştirildi! " + DateTime.Now);
                Console.WriteLine($"  Güncellendi: {Path.GetFileName(readmePath)}");
            }

            string doc1Path = Path.Combine(sourceDir, "docs", "document1.txt");
            if (File.Exists(doc1Path))
            {
                string content = await File.ReadAllTextAsync(doc1Path);
                await File.WriteAllTextAsync(doc1Path, content + "\nDeğiştirilmiş içerik: " + DateTime.Now);
                Console.WriteLine($"  Güncellendi: {Path.GetFileName(doc1Path)}");
            }

            // Veri dosyasını güncelle
            string data1Path = Path.Combine(sourceDir, "data", "data1.dat");
            if (File.Exists(data1Path))
            {
                // Dosyayı yeniden oluşturup değiştir
                await CreateTestFile(data1Path, 120 * 1024, "data1-modified");
                Console.WriteLine($"  Güncellendi: {Path.GetFileName(data1Path)}");
            }
        }

        /// <summary>
        /// Arşivi değiştirilmiş dosyalarla günceller
        /// </summary>
        private static async Task UpdateArchiveWithModifiedFiles(string sourceDir, string archivePath)
        {
            Console.WriteLine("\n🔄 Arşiv değiştirilmiş dosyalarla güncelleniyor...");

            Stopwatch timer = Stopwatch.StartNew();

            // Arşivi aç ve güncelle
            using (FragileArchive archive = new(archivePath, FragileArchiveMode.Update))
            {
                // Değiştirilen dosyaları güncelle
                Console.WriteLine("Değiştirilen dosyalar güncelleniyor...");
                
                // readme.txt dosyasını güncelle
                string readmePath = Path.Combine(sourceDir, "readme.txt");
                if (File.Exists(readmePath))
                {
                    archive.UpdateFile(readmePath, "readme.txt");
                }
                
                // document1.txt dosyasını güncelle
                string doc1Path = Path.Combine(sourceDir, "docs", "document1.txt");
                if (File.Exists(doc1Path))
                {
                    archive.UpdateFile(doc1Path, "docs/document1.txt");
                }
                
                // data1.dat dosyasını güncelle
                string data1Path = Path.Combine(sourceDir, "data", "data1.dat");
                if (File.Exists(data1Path))
                {
                    archive.UpdateFile(data1Path, "data/data1.dat");
                }

                // Meta verileri güncelle
                archive.Metadata.Version = "1.2";
                archive.Metadata.Description = "Mevcut dosyalar güncellendi";
                archive.Metadata.LastModifiedTime = DateTime.Now;
                archive.Metadata.SetCustomProperty("IncrementalVersion", "3");

                // Arşivi kaydet
                archive.Save();
            }

            timer.Stop();

            // Arşiv bilgisini göster
            FileInfo archiveInfo = new(archivePath);
            Console.WriteLine($"Arşiv güncellendi: {archiveInfo.Name}, {archiveInfo.Length:N0} bayt");
            Console.WriteLine($"İşlem süresi: {timer.ElapsedMilliseconds:N0} ms");
        }

        /// <summary>
        /// Bazı dosyaları siler
        /// </summary>
        private static async Task DeleteSomeFiles(string sourceDir)
        {
            Console.WriteLine("\n🗑️ Bazı dosyalar siliniyor...");

            // Bir metin belgesini sil
            string doc2Path = Path.Combine(sourceDir, "docs", "document2.txt");
            if (File.Exists(doc2Path))
            {
                File.Delete(doc2Path);
                Console.WriteLine($"  Silindi: {Path.GetFileName(doc2Path)}");
            }

            // Bir veri dosyasını sil
            string data2Path = Path.Combine(sourceDir, "data", "data2.dat");
            if (File.Exists(data2Path))
            {
                File.Delete(data2Path);
                Console.WriteLine($"  Silindi: {Path.GetFileName(data2Path)}");
            }

            // Bir klasörü tamamen sil
            string configsDir = Path.Combine(sourceDir, "configs");
            if (Directory.Exists(configsDir))
            {
                Directory.Delete(configsDir, true);
                Console.WriteLine($"  Silindi: configs/ klasörü");
            }
        }

        /// <summary>
        /// Arşivi silinen dosyalar dikkate alınarak günceller
        /// </summary>
        private static async Task UpdateArchiveWithDeletedFiles(string sourceDir, string archivePath)
        {
            Console.WriteLine("\n🔄 Arşiv silinen dosyalar dikkate alınarak güncelleniyor...");

            Stopwatch timer = Stopwatch.StartNew();

            // Arşivi aç ve güncelle
            using (FragileArchive archive = new(archivePath, FragileArchiveMode.Update))
            {
                // Silinen dosyaları arşivden kaldır
                Console.WriteLine("Silinen dosyalar arşivden kaldırılıyor...");
                
                // Silinen dosyaları kontrol et ve kaldır
                var entries = archive.Entries.ToList(); // Değiştirilecek koleksiyonu kopyala
                
                foreach (var entry in entries)
                {
                    string fullPath = Path.Combine(sourceDir, entry.Path.Replace('/', Path.DirectorySeparatorChar));
                    
                    if (!entry.IsDirectory && !File.Exists(fullPath))
                    {
                        Console.WriteLine($"  Arşivden kaldırılıyor: {entry.Path}");
                        archive.RemoveEntry(entry.Path);
                    }
                    else if (entry.IsDirectory && !Directory.Exists(fullPath))
                    {
                        Console.WriteLine($"  Arşivden kaldırılıyor: {entry.Path}/");
                        archive.RemoveEntry(entry.Path);
                    }
                }

                // Meta verileri güncelle
                archive.Metadata.Version = "1.3";
                archive.Metadata.Description = "Silinen dosyalar kaldırıldı";
                archive.Metadata.LastModifiedTime = DateTime.Now;
                archive.Metadata.SetCustomProperty("IncrementalVersion", "4");

                // Arşivi kaydet
                archive.Save();
            }

            timer.Stop();

            // Arşiv bilgisini göster
            FileInfo archiveInfo = new(archivePath);
            Console.WriteLine($"Arşiv güncellendi: {archiveInfo.Name}, {archiveInfo.Length:N0} bayt");
            Console.WriteLine($"İşlem süresi: {timer.ElapsedMilliseconds:N0} ms");
        }

        /// <summary>
        /// Karma değişiklikler yapar (ekleme, değiştirme, silme birlikte)
        /// </summary>
        private static async Task MixedChanges(string sourceDir, string version)
        {
            Console.WriteLine($"\n🔀 Karma değişiklikler yapılıyor ({version})...");

            // Yeni bir klasör oluştur
            Directory.CreateDirectory(Path.Combine(sourceDir, "media"));
            await File.WriteAllTextAsync(Path.Combine(sourceDir, "media", "info.txt"), $"Medya bilgileri versiyon: {version}");
            await CreateTestFile(Path.Combine(sourceDir, "media", "sample.mp3"), 250 * 1024, $"sample-{version}");

            // Bir dosyayı değiştir
            string scriptPath = Path.Combine(sourceDir, "scripts", "script1.js");
            if (File.Exists(scriptPath))
            {
                string content = await File.ReadAllTextAsync(scriptPath);
                await File.WriteAllTextAsync(scriptPath, content + $"\nconsole.log('Güncellendi: {DateTime.Now}');");
            }

            // Bir dosyayı sil
            string data3Path = Path.Combine(sourceDir, "data", "data3.dat");
            if (File.Exists(data3Path))
            {
                File.Delete(data3Path);
            }

            Console.WriteLine("Karma değişiklikler tamamlandı.");
        }

        /// <summary>
        /// Arşivi karma değişikliklerle günceller
        /// </summary>
        private static async Task UpdateArchiveWithMixedChanges(string sourceDir, string archivePath)
        {
            Console.WriteLine("\n🔄 Arşiv karma değişikliklerle güncelleniyor...");

            Stopwatch timer = Stopwatch.StartNew();

            // Arşivi aç ve güncelle
            using (FragileArchive archive = new(archivePath, FragileArchiveMode.Update))
            {
                // Yeni klasör ve dosyaları ekle
                string mediaDir = Path.Combine(sourceDir, "media");
                if (Directory.Exists(mediaDir))
                {
                    archive.AddDirectory(mediaDir, "media", new AddDirectoryOptions
                    {
                        SearchOption = SearchOption.AllDirectories,
                        CompressionAlgorithm = CompressionAlgorithm.Deflate,
                        CompressionLevel = CompressionLevel.Normal
                    });
                }

                // Değiştirilen dosyaları güncelle
                string scriptPath = Path.Combine(sourceDir, "scripts", "script1.js");
                if (File.Exists(scriptPath))
                {
                    archive.UpdateFile(scriptPath, "scripts/script1.js");
                }

                // Silinen dosyaları kontrol et ve kaldır
                var entries = archive.Entries.ToList(); // Değiştirilecek koleksiyonu kopyala
                
                foreach (var entry in entries)
                {
                    if (!entry.IsDirectory)
                    {
                        string fullPath = Path.Combine(sourceDir, entry.Path.Replace('/', Path.DirectorySeparatorChar));
                        if (!File.Exists(fullPath))
                        {
                            Console.WriteLine($"  Arşivden kaldırılıyor: {entry.Path}");
                            archive.RemoveEntry(entry.Path);
                        }
                    }
                }

                // Meta verileri güncelle
                archive.Metadata.Version = "2.0";
                archive.Metadata.Description = "Karma değişiklikler uygulandı";
                archive.Metadata.LastModifiedTime = DateTime.Now;
                archive.Metadata.SetCustomProperty("IncrementalVersion", "5");

                // Arşivi kaydet
                archive.Save();
            }

            timer.Stop();

            // Arşiv bilgisini göster
            FileInfo archiveInfo = new(archivePath);
            Console.WriteLine($"Arşiv güncellendi: {archiveInfo.Name}, {archiveInfo.Length:N0} bayt");
            Console.WriteLine($"İşlem süresi: {timer.ElapsedMilliseconds:N0} ms");
        }

        /// <summary>
        /// Arşiv versiyonları arasında gezinmeyi test eder
        /// </summary>
        private static async Task TestArchiveVersions(string archivePath, string extractDir)
        {
            Console.WriteLine("\n⏱️ Arşiv Versiyonlarını Test Etme");
            Console.WriteLine("-------------------------------");

            if (!Directory.Exists(extractDir))
            {
                Directory.CreateDirectory(extractDir);
            }

            using (FragileArchive archive = new(archivePath, FragileArchiveMode.Read))
            {
                // Versiyonları kontrol et (Bu bir simülasyondur, gerçek versiyonlama daha karmaşık olacaktır)
                string versionStr = archive.Metadata.GetCustomProperty("IncrementalVersion");
                if (int.TryParse(versionStr, out int currentVersion))
                {
                    Console.WriteLine($"Mevcut arşiv versiyonu: {currentVersion}");
                    Console.WriteLine($"Arşiv açıklaması: {archive.Metadata.Description}");
                    
                    // Her versiyonda arşivi temsil edin (simülasyon)
                    for (int ver = 1; ver <= currentVersion; ver++)
                    {
                        Console.WriteLine($"\nVersiyon {ver} simülasyonu:");
                        SimulateArchiveVersion(ver);
                        
                        // Bu versiyonu çıkarmak için bir dizin oluştur
                        string versionExtractDir = Path.Combine(extractDir, $"version_{ver}");
                        if (Directory.Exists(versionExtractDir))
                        {
                            Directory.Delete(versionExtractDir, true);
                        }
                        Directory.CreateDirectory(versionExtractDir);
                        
                        Console.WriteLine($"Versiyon {ver} çıkarıldı: {versionExtractDir}");
                    }
                    
                    Console.WriteLine("\nNot: Gerçek bir versiyonlama sisteminde, her arşiv versiyonu için delta değişiklikleri saklanır.");
                }
                else
                {
                    Console.WriteLine("Arşiv versiyonu bilgisi bulunamadı!");
                }
            }
        }

        /// <summary>
        /// Belirli bir arşiv versiyonunu simüle eder
        /// </summary>
        private static void SimulateArchiveVersion(int version)
        {
            string description = "";
            int fileCount = 0;
            int dirCount = 0;
            
            switch (version)
            {
                case 1:
                    description = "İlk arşiv versiyonu";
                    fileCount = 5;
                    dirCount = 3;
                    break;
                case 2:
                    description = "Yeni dosyalar eklendi";
                    fileCount = 9;
                    dirCount = 5;
                    break;
                case 3:
                    description = "Mevcut dosyalar güncellendi";
                    fileCount = 9;
                    dirCount = 5;
                    break;
                case 4:
                    description = "Silinen dosyalar kaldırıldı";
                    fileCount = 6;
                    dirCount = 3;
                    break;
                case 5:
                    description = "Karma değişiklikler uygulandı";
                    fileCount = 7;
                    dirCount = 4;
                    break;
                default:
                    description = "Bilinmeyen versiyon";
                    break;
            }
            
            Console.WriteLine($"  Açıklama: {description}");
            Console.WriteLine($"  Dosya sayısı: {fileCount}");
            Console.WriteLine($"  Klasör sayısı: {dirCount}");
            Console.WriteLine($"  Oluşturulma zamanı: {DateTime.Now.AddDays(-5).AddHours(version)}");
        }

        /// <summary>
        /// Test için belirli boyutta bir dosya oluşturur
        /// </summary>
        private static async Task CreateTestFile(string filePath, int size, string pattern)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            
            using (FileStream fs = new(filePath, FileMode.Create, FileAccess.Write))
            {
                // İçeriği oluştur
                byte[] buffer = new byte[4096];
                byte[] patternBytes = Encoding.UTF8.GetBytes(pattern);
                
                // Paterni buffer'a kopyala
                for (int i = 0; i < buffer.Length && i < patternBytes.Length; i++)
                {
                    buffer[i] = patternBytes[i % patternBytes.Length];
                }
                
                // Dosyayı istenen boyuta kadar yaz
                int bytesRemaining = size;
                while (bytesRemaining > 0)
                {
                    int bytesToWrite = Math.Min(buffer.Length, bytesRemaining);
                    await fs.WriteAsync(buffer, 0, bytesToWrite);
                    bytesRemaining -= bytesToWrite;
                }
            }
        }
    }
}
