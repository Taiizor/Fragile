using Fragile.Metadata;
using System.Text;
using System.Text.Json;

namespace Fragile.Samples.Metadata
{
    /// <summary>
    /// Fragile metadata özelliklerini gösteren örnek uygulama
    /// </summary>
    public class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Metadata Örneği");
            Console.WriteLine("=======================");

            try
            {
                // Geçici dizin oluştur
                string tempDir = Path.Combine(Path.GetTempPath(), "FragileMetadataSample");
                Directory.CreateDirectory(tempDir);

                // 1. Arşiv metadata örneği
                await DemoArchiveMetadata(tempDir);

                Console.WriteLine("\nPress any key to continue to the next demo...");
                Console.ReadKey();
                Console.Clear();

                // 2. Dosya metadata örneği
                await DemoEntryMetadata(tempDir);

                // 3. Metadata JSON serileştirme ve filtreleme
                await DemoMetadataQueryAndFiltering(tempDir);
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
        /// Arşiv metadata özellikleri örneği
        /// </summary>
        private static async Task DemoArchiveMetadata(string outputDir)
        {
            Console.WriteLine("\n📚 Arşiv Metadata Örneği");
            Console.WriteLine("========================");

            // Yeni arşiv metadata'sı oluştur
            ArchiveMetadata archiveMetadata = new()
            {
                Title = "Proje Dosyaları",
                Description = "Örnek proje için kaynak kodları ve dokümanlar",
                Author = "Fatma Yılmaz",
                Creator = "Fragile Library",
                Version = "1.2.0",
                CreationTime = DateTime.Now,
                LastModifiedTime = DateTime.Now
            };

            // Etiketler ekle
            archiveMetadata.Tags.Add("proje");
            archiveMetadata.Tags.Add("kaynak");
            archiveMetadata.Tags.Add("dokümanlar");

            // Özel özellikler ekle
            archiveMetadata.AddProperty("company", "Acme Ltd.");
            archiveMetadata.AddProperty("department", "Ar-Ge");
            archiveMetadata.AddProperty("security-level", "gizli");

            // Uygulama özgü veri ekle
            archiveMetadata.AddApplicationData("app_version", "1.5.2");
            archiveMetadata.AddApplicationData("ui_theme", "dark");

            // Bilgileri görüntüle
            Console.WriteLine("\n📌 Arşiv Özellikleri:");
            Console.WriteLine($"Başlık: {archiveMetadata.Title}");
            Console.WriteLine($"Açıklama: {archiveMetadata.Description}");
            Console.WriteLine($"Yazar: {archiveMetadata.Author}");
            Console.WriteLine($"Oluşturan: {archiveMetadata.Creator}");
            Console.WriteLine($"Sürüm: {archiveMetadata.Version}");
            Console.WriteLine($"Oluşturma Zamanı: {archiveMetadata.CreationTime}");
            Console.WriteLine($"Son Değiştirme: {archiveMetadata.LastModifiedTime}");

            Console.WriteLine("\n🏷️ Etiketler:");
            foreach (string tag in archiveMetadata.Tags)
            {
                Console.WriteLine($"- {tag}");
            }

            Console.WriteLine("\n🔑 Özel Özellikler:");
            foreach (KeyValuePair<string, string> kvp in archiveMetadata.CustomProperties)
            {
                Console.WriteLine($"- {kvp.Key}: {kvp.Value}");
            }

            Console.WriteLine("\n📱 Uygulama Verileri:");
            foreach (KeyValuePair<string, string> kvp in archiveMetadata.ApplicationData)
            {
                Console.WriteLine($"- {kvp.Key}: {kvp.Value}");
            }

            // JSON'a dönüştür
            string jsonOutput = archiveMetadata.ToJson();
            string jsonFilePath = Path.Combine(outputDir, "archive_metadata.json");
            await File.WriteAllTextAsync(jsonFilePath, jsonOutput);

            Console.WriteLine($"\n💾 Metadata JSON dosyası kaydedildi: {jsonFilePath}");

            // JSON çıktısını göster
            Console.WriteLine("\n📄 JSON Çıktısı:");
            Console.WriteLine(JsonToPrettyString(jsonOutput));

            // JSON'dan geri yükle
            Console.WriteLine("\n♻️ JSON'dan Metadata Yükleme:");
            ArchiveMetadata loadedMetadata = ArchiveMetadata.FromJson(jsonOutput);
            Console.WriteLine("Başarıyla yüklendi!");
            Console.WriteLine($"Yüklenen başlık: {loadedMetadata.Title}");
            Console.WriteLine($"Yüklenen açıklama: {loadedMetadata.Description}");
        }

        /// <summary>
        /// Dosya/dizin girişi metadata örneği
        /// </summary>
        private static async Task DemoEntryMetadata(string outputDir)
        {
            Console.WriteLine("\n📂 Dosya Metadata Örneği");
            Console.WriteLine("========================");

            // Bir belge dosyası için metadata oluştur
            EntryMetadata documentMetadata = new()
            {
                CreationTime = DateTime.Now.AddDays(-30),
                LastAccessTime = DateTime.Now.AddDays(-2),
                Attributes = "Archive,ReadOnly",
                Owner = "ahmet.yilmaz",
                Group = "developers",
                MimeType = "application/pdf",
                Comment = "Proje teknik şartnamesi"
            };

            // Etiketler ekle
            documentMetadata.AddTag("doküman");
            documentMetadata.AddTag("şartname");
            documentMetadata.AddTag("teknik");

            // Özel özellikler ekle
            documentMetadata.AddProperty("version", "2.1");
            documentMetadata.AddProperty("status", "approved");
            documentMetadata.AddProperty("language", "tr-TR");
            documentMetadata.AddProperty("approval-date", DateTime.Now.ToString("yyyy-MM-dd"));

            // Bilgileri görüntüle
            Console.WriteLine("\n📌 Dosya Özellikleri:");
            Console.WriteLine($"Oluşturma Zamanı: {documentMetadata.CreationTime}");
            Console.WriteLine($"Son Erişim: {documentMetadata.LastAccessTime}");
            Console.WriteLine($"Nitelikler: {documentMetadata.Attributes}");
            Console.WriteLine($"Sahibi: {documentMetadata.Owner}");
            Console.WriteLine($"Grup: {documentMetadata.Group}");
            Console.WriteLine($"MIME Türü: {documentMetadata.MimeType}");
            Console.WriteLine($"Yorum: {documentMetadata.Comment}");

            Console.WriteLine("\n🏷️ Etiketler:");
            foreach (string tag in documentMetadata.Tags)
            {
                Console.WriteLine($"- {tag}");
            }

            Console.WriteLine("\n🔑 Özel Özellikler:");
            foreach (KeyValuePair<string, string> kvp in documentMetadata.CustomProperties)
            {
                Console.WriteLine($"- {kvp.Key}: {kvp.Value}");
            }

            // JSON'a dönüştür
            string jsonOutput = documentMetadata.ToJson();
            string jsonFilePath = Path.Combine(outputDir, "document_metadata.json");
            await File.WriteAllTextAsync(jsonFilePath, jsonOutput);

            Console.WriteLine($"\n💾 Metadata JSON dosyası kaydedildi: {jsonFilePath}");

            // JSON çıktısını göster
            Console.WriteLine("\n📄 JSON Çıktısı:");
            Console.WriteLine(JsonToPrettyString(jsonOutput));

            // Resim dosyası için başka bir metadata oluştur
            EntryMetadata imageMetadata = new()
            {
                CreationTime = DateTime.Now.AddDays(-5),
                LastAccessTime = DateTime.Now.AddHours(-2),
                MimeType = "image/jpeg",
                Comment = "Ürün fotoğrafı"
            };

            imageMetadata.AddTag("resim");
            imageMetadata.AddTag("ürün");
            imageMetadata.AddProperty("dimensions", "1920x1080");
            imageMetadata.AddProperty("camera", "Canon EOS 5D");

            Console.WriteLine("\n📷 Resim Dosyası Metadata Örneği:");
            Console.WriteLine($"MIME Türü: {imageMetadata.MimeType}");
            Console.WriteLine($"Boyutlar: {imageMetadata.GetProperty("dimensions")}");
            Console.WriteLine($"Kamera: {imageMetadata.GetProperty("camera")}");
        }

        /// <summary>
        /// Metadata sorgulama ve filtreleme örneği
        /// </summary>
        private static async Task DemoMetadataQueryAndFiltering(string outputDir)
        {
            Console.WriteLine("\n🔍 Metadata Sorgulama ve Filtreleme");
            Console.WriteLine("==================================");

            // Çeşitli dosyalar için örnek metadata koleksiyonu oluştur
            List<EntryMetadata> entries = GenerateSampleEntries();

            // Koleksiyonu JSON olarak kaydet
            JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
            string jsonCollection = JsonSerializer.Serialize(entries, jsonOptions);
            string jsonFilePath = Path.Combine(outputDir, "entries_collection.json");
            await File.WriteAllTextAsync(jsonFilePath, jsonCollection);

            Console.WriteLine($"\n💾 Metadata koleksiyonu kaydedildi: {jsonFilePath}");

            // Örnekler: Metadata üzerinde sorgulama
            Console.WriteLine("\n🔍 Metadata Sorgulama Örnekleri:");

            // 1. PDF dosyalarını bul
            Console.WriteLine("\n📄 PDF Dosyaları:");
            List<EntryMetadata> pdfFiles = entries.FindAll(e => e.MimeType == "application/pdf");
            foreach (EntryMetadata entry in pdfFiles)
            {
                Console.WriteLine($"- {entry.GetProperty("filename")} | {entry.Comment}");
            }

            // 2. Belirli bir etiket içeren dosyaları bul
            string searchTag = "proje";
            Console.WriteLine($"\n🏷️ '{searchTag}' Etiketli Dosyalar:");
            List<EntryMetadata> taggedFiles = entries.FindAll(e => e.Tags.Contains(searchTag));
            foreach (EntryMetadata entry in taggedFiles)
            {
                Console.WriteLine($"- {entry.GetProperty("filename")} | {string.Join(", ", entry.Tags)}");
            }

            // 3. Belirli bir tarihten sonra değiştirilen dosyaları bul
            DateTime cutoffDate = DateTime.Now.AddDays(-7);
            Console.WriteLine($"\n⏰ {cutoffDate:d} Sonrası Oluşturulan Dosyalar:");
            List<EntryMetadata> recentFiles = entries.FindAll(e => e.CreationTime > cutoffDate);
            foreach (EntryMetadata entry in recentFiles)
            {
                Console.WriteLine($"- {entry.GetProperty("filename")} | {entry.CreationTime:g}");
            }

            // 4. Belirli bir sahibi olan dosyaları bul
            string owner = "ahmet.yilmaz";
            Console.WriteLine($"\n👤 Sahibi '{owner}' Olan Dosyalar:");
            List<EntryMetadata> ownerFiles = entries.FindAll(e => e.Owner == owner);
            foreach (EntryMetadata entry in ownerFiles)
            {
                Console.WriteLine($"- {entry.GetProperty("filename")} | {entry.Owner}");
            }

            // 5. Gelişmiş sorgulama (birden fazla kriter)
            Console.WriteLine("\n🔍 Gelişmiş Sorgulama (PDF ve son 30 günde ve 'proje' etiketli):");
            List<EntryMetadata> advancedQuery = entries.FindAll(e =>
                e.MimeType == "application/pdf" &&
                e.CreationTime > DateTime.Now.AddDays(-30) &&
                e.Tags.Contains("proje"));

            foreach (EntryMetadata entry in advancedQuery)
            {
                Console.WriteLine($"- {entry.GetProperty("filename")} | {entry.CreationTime:d} | {string.Join(", ", entry.Tags)}");
            }
        }

        /// <summary>
        /// Örnek metadata koleksiyonu oluşturur
        /// </summary>
        private static List<EntryMetadata> GenerateSampleEntries()
        {
            List<EntryMetadata> entries = new();

            // Belge 1
            EntryMetadata doc1 = new()
            {
                CreationTime = DateTime.Now.AddDays(-40),
                LastAccessTime = DateTime.Now.AddDays(-5),
                Owner = "mehmet.demir",
                Group = "management",
                MimeType = "application/pdf",
                Comment = "Firma yıllık raporu"
            };
            doc1.AddTag("rapor");
            doc1.AddTag("yıllık");
            doc1.AddProperty("filename", "YillikRapor2023.pdf");
            doc1.AddProperty("pageCount", "42");
            entries.Add(doc1);

            // Belge 2
            EntryMetadata doc2 = new()
            {
                CreationTime = DateTime.Now.AddDays(-10),
                LastAccessTime = DateTime.Now.AddDays(-1),
                Owner = "ahmet.yilmaz",
                Group = "developers",
                MimeType = "application/pdf",
                Comment = "Proje teknik şartnamesi"
            };
            doc2.AddTag("proje");
            doc2.AddTag("şartname");
            doc2.AddProperty("filename", "TeknikSartname.pdf");
            doc2.AddProperty("version", "2.1");
            entries.Add(doc2);

            // Belge 3
            EntryMetadata doc3 = new()
            {
                CreationTime = DateTime.Now.AddDays(-5),
                LastAccessTime = DateTime.Now.AddHours(-12),
                Owner = "ahmet.yilmaz",
                Group = "developers",
                MimeType = "text/plain",
                Comment = "Proje log dosyası"
            };
            doc3.AddTag("proje");
            doc3.AddTag("log");
            doc3.AddProperty("filename", "debug.log");
            entries.Add(doc3);

            // Resim 1
            EntryMetadata img1 = new()
            {
                CreationTime = DateTime.Now.AddDays(-3),
                LastAccessTime = DateTime.Now.AddHours(-6),
                Owner = "ayse.kaya",
                Group = "design",
                MimeType = "image/png",
                Comment = "Proje logo tasarımı"
            };
            img1.AddTag("proje");
            img1.AddTag("tasarım");
            img1.AddTag("logo");
            img1.AddProperty("filename", "logo.png");
            img1.AddProperty("dimensions", "512x512");
            entries.Add(img1);

            // Belge 4
            EntryMetadata doc4 = new()
            {
                CreationTime = DateTime.Now.AddDays(-2),
                LastAccessTime = DateTime.Now.AddHours(-1),
                Owner = "fatma.yilmaz",
                Group = "management",
                MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                Comment = "Bütçe planlaması"
            };
            doc4.AddTag("bütçe");
            doc4.AddTag("finans");
            doc4.AddProperty("filename", "ButcePlan2024.xlsx");
            entries.Add(doc4);

            return entries;
        }

        /// <summary>
        /// JSON string'i okunabilir formata dönüştürür
        /// </summary>
        private static string JsonToPrettyString(string json)
        {
            try
            {
                // JSON'ı deserialize et
                JsonDocument jsonDocument = JsonDocument.Parse(json);

                // Pretty print format ile serialize et
                JsonSerializerOptions options = new() { WriteIndented = true };
                return JsonSerializer.Serialize(jsonDocument, options);
            }
            catch
            {
                // Hata durumunda orjinal JSON'ı dön
                return json;
            }
        }
    }
}
