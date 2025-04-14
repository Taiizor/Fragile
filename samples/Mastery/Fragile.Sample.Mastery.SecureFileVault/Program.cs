using Fragile.Compression;
using Fragile.Core;
using Fragile.Encryption;
using Fragile.Models;
using Fragile.Verification;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fragile.Sample.Mastery.SecureFileVault
{
    class Program
    {
        // Kasa ayarları
        private static readonly VaultSettings _settings = new()
        {
            VaultDirectory = "Sample/Vault",
            TempDirectory = "Sample/Temp",
            IndexFileName = "vault_index.frgl",
            EncryptionMethod = EncryptionMethod.AES256,
            CompressionLevel = CompressionLevel.Ultra,
            EnableErrorCorrection = true,
            ErrorCorrectionLevel = 10, // Arşiv boyutunun %10'u kadar hata düzeltme
            ChecksumAlgorithm = ChecksumAlgorithm.SHA256,
            DefaultMetadata = new Dictionary<string, string>
            {
                { "Creator", "Fragile.Sample.Mastery.SecureFileVault" },
                { "SecurityLevel", "High" },
                { "CreatedBy", Environment.UserName },
                { "Platform", Environment.OSVersion.ToString() }
            }
        };

        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Güvenli Dosya Kasası - Ustalık Örneği");
            Console.WriteLine("=============================================");

            // Gerekli dizinleri oluştur
            Directory.CreateDirectory("Sample");
            Directory.CreateDirectory(_settings.VaultDirectory);
            Directory.CreateDirectory(_settings.TempDirectory);

            try
            {
                // Örnek kasa işlemleri
                // İlk kullanıcı ayarlarını tanımla
                VaultUser adminUser = new()
                {
                    Username = "admin",
                    Password = "SuperGizli123!",
                    AccessLevel = AccessLevel.Administrator
                };

                VaultUser normalUser = new()
                {
                    Username = "kullanici1",
                    Password = "Gizli456!",
                    AccessLevel = AccessLevel.StandardUser
                };

                // Kasanın indeks dosyasını başlat (veya var olanı aç)
                VaultManager vaultManager = await VaultManager.InitializeAsync(_settings, adminUser);

                // Kullanıcı hesabı oluştur
                await vaultManager.AddUserAsync(normalUser, adminUser);

                // Örnek dosyaları oluştur
                string sampleDir = Path.Combine("Sample", "TestFiles");
                Directory.CreateDirectory(sampleDir);
                await CreateSampleFiles(sampleDir);

                // Admin kullanıcısı olarak oturum aç
                Console.WriteLine($"\nAdmin kullanıcısı olarak oturum açılıyor ({adminUser.Username})...");
                bool loginResult = await vaultManager.LoginAsync(adminUser.Username, adminUser.Password);

                if (loginResult)
                {
                    Console.WriteLine("Oturum açma başarılı!");

                    // Kasaya dosya ekle
                    Console.WriteLine("\nDosyalar kasaya ekleniyor...");
                    await AddFilesToVault(vaultManager, sampleDir);

                    // Kasa içeriğini listele
                    Console.WriteLine("\nKasa içeriği listeleniyor...");
                    await ListVaultContents(vaultManager);

                    // Bir dosyayı dışarı çıkar
                    Console.WriteLine("\nDosya dışarı çıkarılıyor...");
                    string extractDir = Path.Combine("Sample", "Extracted");
                    Directory.CreateDirectory(extractDir);
                    await ExtractFile(vaultManager, "gizli_belge.txt", extractDir);

                    // Normal kullanıcı olarak oturum aç
                    Console.WriteLine($"\nNormal kullanıcı olarak oturum açılıyor ({normalUser.Username})...");
                    loginResult = await vaultManager.LoginAsync(normalUser.Username, normalUser.Password);

                    if (loginResult)
                    {
                        Console.WriteLine("Oturum açma başarılı!");

                        // Normal kullanıcı olarak listeyi görüntüle
                        Console.WriteLine("\nNormal kullanıcı olarak kasa içeriği listeleniyor...");
                        await ListVaultContents(vaultManager);

                        // Dosya çıkarma işlemini dene
                        Console.WriteLine("\nNormal kullanıcı olarak dosya çıkarma deneniyor...");
                        await ExtractFile(vaultManager, "gizli_belge.txt", extractDir);
                    }

                    // Dosyayı kasadan sil
                    Console.WriteLine("\nAdmin olarak tekrar oturum açılıyor...");
                    await vaultManager.LoginAsync(adminUser.Username, adminUser.Password);

                    Console.WriteLine("\nDosya siliniyor...");
                    await vaultManager.DeleteFileAsync("gizli_belge.txt");

                    // Son kasa içeriğini göster
                    Console.WriteLine("\nGüncellenmiş kasa içeriği:");
                    await ListVaultContents(vaultManager);
                }
                else
                {
                    Console.WriteLine("Oturum açma başarısız!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hata: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine("\nGüvenli Dosya Kasası örneği tamamlandı!");
            Console.WriteLine("'Sample' dizininde oluşturulan dosyaları ve kasa içeriğini kontrol edebilirsiniz.");

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static async Task CreateSampleFiles(string directory)
        {
            Console.WriteLine($"Örnek dosyalar oluşturuluyor: {directory}");

            // Metin dosyaları oluştur
            await File.WriteAllTextAsync(
                Path.Combine(directory, "gizli_belge.txt"),
                "GİZLİ BİLGİLER\n" +
                "==============\n\n" +
                "Bu belge çok gizli bilgiler içermektedir.\n" +
                "Kredi Kartı: 1234-5678-9012-3456\n" +
                "Şifre: TehlikeliSifre123!\n" +
                "Not: Bu dosya sadece örnek amaçlıdır, gerçek bilgiler değildir."
            );

            await File.WriteAllTextAsync(
                Path.Combine(directory, "notlar.txt"),
                "Önemli Notlar\n" +
                "============\n\n" +
                "- Pazartesi: Toplantı (10:00)\n" +
                "- Salı: Proje teslimi\n" +
                "- Çarşamba: Müşteri görüşmesi\n" +
                "- Cuma: Spor salonu (17:00)"
            );

            // JSON dosyası
            var settings = new
            {
                ApiKey = "ak_12345abcdef",
                ApiSecret = "as_67890ghijkl",
                Endpoints = new[] {
                    "https://api.example.com/v1",
                    "https://backup-api.example.com/v1"
                },
                RateLimits = new
                {
                    PerSecond = 10,
                    PerMinute = 100,
                    PerHour = 1000
                }
            };

            await File.WriteAllTextAsync(
                Path.Combine(directory, "ayarlar.json"),
                JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true })
            );

            // Küçük bir "resim" dosyası oluştur (gerçek bir resim değil)
            using (FileStream fs = new(Path.Combine(directory, "resim.jpg"), FileMode.Create))
            {
                byte[] dummyImage = new byte[50 * 1024]; // 50 KB
                Random random = new();
                random.NextBytes(dummyImage);
                await fs.WriteAsync(dummyImage, 0, dummyImage.Length);
            }

            Console.WriteLine($"Oluşturulan dosyalar:");
            foreach (string file in Directory.GetFiles(directory))
            {
                FileInfo fileInfo = new(file);
                Console.WriteLine($"- {fileInfo.Name} ({fileInfo.Length:N0} bayt)");
            }
        }

        static async Task AddFilesToVault(VaultManager manager, string sourceDirectory)
        {
            foreach (string filePath in Directory.GetFiles(sourceDirectory))
            {
                string fileName = Path.GetFileName(filePath);
                Console.WriteLine($"Ekleniyor: {fileName}");

                FileMetadata metadata = new()
                {
                    OriginalFileName = fileName,
                    FileType = Path.GetExtension(fileName).TrimStart('.'),
                    CreationTime = File.GetCreationTime(filePath),
                    LastModified = File.GetLastWriteTime(filePath),
                    FileSize = new FileInfo(filePath).Length,
                    Tags = new List<string>()
                };

                // Dosya türüne göre etiketler ekle
                string ext = Path.GetExtension(fileName).ToLower();
                switch (ext)
                {
                    case ".txt":
                        metadata.Tags.Add("metin");
                        metadata.Tags.Add("döküman");
                        metadata.MimeType = "text/plain";
                        break;
                    case ".json":
                        metadata.Tags.Add("json");
                        metadata.Tags.Add("yapılandırma");
                        metadata.MimeType = "application/json";
                        break;
                    case ".jpg":
                    case ".jpeg":
                        metadata.Tags.Add("resim");
                        metadata.MimeType = "image/jpeg";
                        break;
                    default:
                        metadata.Tags.Add("diğer");
                        metadata.MimeType = "application/octet-stream";
                        break;
                }

                // Hassas dosyaları işaretle
                if (fileName.Contains("gizli"))
                {
                    metadata.Tags.Add("gizli");
                    metadata.AccessLevel = AccessLevel.Administrator;
                }

                // Dosyayı kasaya ekle
                await manager.AddFileAsync(filePath, metadata);
            }
        }

        static async Task ListVaultContents(VaultManager manager)
        {
            List<VaultFile> files = await manager.ListFilesAsync();

            if (files.Count == 0)
            {
                Console.WriteLine("Kasa boş. Henüz dosya eklenmemiş.");
                return;
            }

            Console.WriteLine("\nKasadaki Dosyalar:");
            Console.WriteLine("=================");
            Console.WriteLine($"{"Dosya Adı",-30} | {"Boyut",-10} | {"Tür",-10} | {"Erişim Seviyesi",-15} | {"Etiketler"}");
            Console.WriteLine(new string('-', 90));

            foreach (VaultFile file in files)
            {
                string tags = string.Join(", ", file.Metadata.Tags);
                Console.WriteLine($"{file.Metadata.OriginalFileName,-30} | {file.Metadata.FileSize,8:N0} B | {file.Metadata.FileType,-10} | {file.Metadata.AccessLevel,-15} | {tags}");
            }
        }

        static async Task ExtractFile(VaultManager manager, string fileName, string targetDirectory)
        {
            try
            {
                string extractedPath = await manager.ExtractFileAsync(fileName, targetDirectory);
                if (!string.IsNullOrEmpty(extractedPath))
                {
                    Console.WriteLine($"Dosya başarıyla çıkarıldı: {extractedPath}");
                    Console.WriteLine($"Dosya boyutu: {new FileInfo(extractedPath).Length:N0} bayt");
                }
                else
                {
                    Console.WriteLine("Dosya çıkarma başarısız oldu veya yetkisiz erişim.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dosya çıkarma hatası: {ex.Message}");
            }
        }
    }

    public enum AccessLevel
    {
        StandardUser,
        PowerUser,
        Administrator
    }

    public class VaultSettings
    {
        public string VaultDirectory { get; set; }
        public string TempDirectory { get; set; }
        public string IndexFileName { get; set; }
        public EncryptionMethod EncryptionMethod { get; set; }
        public CompressionLevel CompressionLevel { get; set; }
        public bool EnableErrorCorrection { get; set; }
        public int ErrorCorrectionLevel { get; set; }
        public ChecksumAlgorithm ChecksumAlgorithm { get; set; }
        public Dictionary<string, string> DefaultMetadata { get; set; }
    }

    public class VaultUser
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public AccessLevel AccessLevel { get; set; }
        public string Salt { get; private set; }
        public string PasswordHash { get; private set; }

        public void ComputePasswordHash()
        {
            // Şifre boş olmamalı
            if (string.IsNullOrEmpty(Password))
            {
                throw new ArgumentNullException(nameof(Password), "Şifre boş olamaz");
            }

            // Güvenli şifre hashleme
            Salt = GenerateRandomSalt();
            PasswordHash = HashPassword(Password, Salt);
            // Artık şifreyi silmiyoruz, çünkü giriş işleminde kullanılacak
            // Password = null;
        }

        private string GenerateRandomSalt()
        {
            byte[] salt = new byte[16];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            return Convert.ToBase64String(salt);
        }

        private string HashPassword(string password, string salt)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentNullException(nameof(password), "Şifre boş olamaz");
            }

            using Rfc2898DeriveBytes deriveBytes = new(password, Convert.FromBase64String(salt), 10000);
            byte[] hash = deriveBytes.GetBytes(32);
            return Convert.ToBase64String(hash);
        }

        public bool VerifyPassword(string password)
        {
            // Kullanıcının girdiği şifreyi, kaydedilmiş salt değeri kullanarak hash'le
            string hashedInputPassword = HashPassword(password, Salt);
            // Hash'leri karşılaştır
            return hashedInputPassword == PasswordHash;
        }
    }

    public class FileMetadata
    {
        public string OriginalFileName { get; set; }
        public string FileType { get; set; }
        public string MimeType { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime LastModified { get; set; }
        public long FileSize { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public AccessLevel AccessLevel { get; set; } = AccessLevel.StandardUser;
        public Dictionary<string, string> CustomProperties { get; set; } = new Dictionary<string, string>();
    }

    public class VaultFile
    {
        public string Id { get; set; }
        public string StorageFileName { get; set; }
        public FileMetadata Metadata { get; set; }
        public DateTime AddedToVault { get; set; }
        public string AddedByUser { get; set; }
    }

    public class VaultIndex
    {
        public List<VaultUser> Users { get; set; } = new List<VaultUser>();
        public List<VaultFile> Files { get; set; } = new List<VaultFile>();
        public DateTime LastModified { get; set; }
        public string LastModifiedByUser { get; set; }
        public int Version { get; set; } = 1;
    }

    public class VaultManager
    {
        private readonly VaultSettings _settings;
        private VaultIndex _index;
        private VaultUser _currentUser;
        private bool _isInitialized;

        private VaultManager(VaultSettings settings)
        {
            _settings = settings;
            _index = new VaultIndex();
            _isInitialized = false;
        }

        public static async Task<VaultManager> InitializeAsync(VaultSettings settings, VaultUser adminUser)
        {
            VaultManager manager = new(settings);

            // İndeks dosyası mevcut mu kontrol et
            string indexPath = Path.Combine(settings.VaultDirectory, settings.IndexFileName);

            if (File.Exists(indexPath))
            {
                // Mevcut indeksi aç
                Console.WriteLine("Mevcut kasa indeksi bulundu, yükleniyor...");
                await manager.LoadIndexAsync();
            }
            else
            {
                // Yeni indeks oluştur
                Console.WriteLine("Yeni kasa indeksi oluşturuluyor...");
                // Şifre hash'leme öncesinde orijinal şifreyi yedekle
                string originalPassword = adminUser.Password;

                adminUser.ComputePasswordHash();
                manager._index.Users.Add(adminUser);
                manager._index.LastModified = DateTime.Now;
                manager._index.LastModifiedByUser = adminUser.Username;

                // Şifreyi geri yükle (giriş işlemi için)
                adminUser.Password = originalPassword;

                // İndeksi kaydet
                await manager.SaveIndexAsync();
            }

            manager._isInitialized = true;
            return manager;
        }

        private async Task LoadIndexAsync()
        {
            string indexPath = Path.Combine(_settings.VaultDirectory, _settings.IndexFileName);

            try
            {
                // İndeks dosyasını aç
                FragileOptions options = new()
                {
                    EnableErrorCorrection = _settings.EnableErrorCorrection,
                    EnableEncryption = true,
                    EncryptionMethod = _settings.EncryptionMethod,
                    Password = "IndexSecretKey" // İndeks dosyası için sabit şifre
                };

                using FragileArchive archive = await FragileArchive.OpenAsync(indexPath, options);

                // İndeks dosyasını çıkar
                string tempIndexPath = Path.Combine(_settings.TempDirectory, "temp_index.json");
                await archive.ExtractAsync("index.json", _settings.TempDirectory);

                // İndeksi oku ve deserialize et
                string jsonContent = await File.ReadAllTextAsync(tempIndexPath);
                _index = JsonSerializer.Deserialize<VaultIndex>(jsonContent);

                // Geçici dosyayı sil
                File.Delete(tempIndexPath);

                Console.WriteLine($"Kasa indeksi yüklendi: {_index.Files.Count} dosya, {_index.Users.Count} kullanıcı");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"İndeks yükleme hatası: {ex.Message}");
                // Bozuk indeks durumunda yeni bir indeks oluştur
                _index = new VaultIndex
                {
                    LastModified = DateTime.Now,
                    Version = 1
                };
            }
        }

        private async Task SaveIndexAsync()
        {
            // İndeksin son değişiklik bilgilerini güncelle
            _index.LastModified = DateTime.Now;
            if (_currentUser != null)
            {
                _index.LastModifiedByUser = _currentUser.Username;
            }

            string indexPath = Path.Combine(_settings.VaultDirectory, _settings.IndexFileName);
            string tempJsonPath = Path.Combine(_settings.TempDirectory, "index.json");

            try
            {
                // İndeksi JSON olarak kaydet
                string jsonContent = JsonSerializer.Serialize(_index, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(tempJsonPath, jsonContent);

                // İndeks dosyasını oluştur
                FragileOptions options = new()
                {
                    EnableErrorCorrection = _settings.EnableErrorCorrection,
                    ErrorCorrectionLevel = _settings.ErrorCorrectionLevel,
                    EnableEncryption = true,
                    EncryptionMethod = _settings.EncryptionMethod,
                    Password = "IndexSecretKey", // İndeks dosyası için sabit şifre
                    CompressionAlgorithm = CompressionAlgorithm.Deflate,
                    CompressionLevel = _settings.CompressionLevel,
                    EnableChecksumVerification = true,
                    ChecksumAlgorithm = _settings.ChecksumAlgorithm
                };

                // Eğer indeks dosyası zaten varsa, sil
                if (File.Exists(indexPath))
                {
                    File.Delete(indexPath);
                }

                // Yeni indeks dosyasını oluştur
                using (FragileArchive archive = await FragileArchive.CreateAsync(indexPath, options))
                {
                    await archive.AddFileAsync(tempJsonPath, "index.json");

                    // Arşive metadata ekle
                    archive.Metadata.Title = "Kasa İndeksi";
                    archive.Metadata.Description = "SecureFileVault kasa indeks dosyası";
                    archive.Metadata.Version = _index.Version.ToString();
                    archive.Metadata.Tags.AddRange(new[] { "index", "vault", "secure" });

                    foreach (KeyValuePair<string, string> item in _settings.DefaultMetadata)
                    {
                        archive.Metadata.AddProperty(item.Key, item.Value);
                    }

                    // Arşivi kaydet
                    await archive.SaveAsync();
                }

                // Geçici dosyayı sil
                File.Delete(tempJsonPath);

                Console.WriteLine("Kasa indeksi güncellendi");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"İndeks kaydetme hatası: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            VaultUser? user = _index.Users.FirstOrDefault(u => u.Username == username);
            if (user == null)
            {
                Console.WriteLine("Kullanıcı bulunamadı");
                return false;
            }

            if (user.VerifyPassword(password))
            {
                _currentUser = user;
                Console.WriteLine($"Oturum açıldı: {username}, Erişim: {user.AccessLevel}");
                return true;
            }

            Console.WriteLine("Hatalı şifre");
            return false;
        }

        public async Task AddUserAsync(VaultUser newUser, VaultUser adminUser)
        {
            // Sadece admin kullanıcılar yeni kullanıcı ekleyebilir
            if (_currentUser != null && _currentUser.AccessLevel != AccessLevel.Administrator)
            {
                if (adminUser == null || adminUser.AccessLevel != AccessLevel.Administrator)
                {
                    throw new UnauthorizedAccessException("Kullanıcı ekleme yetkisi yok");
                }

                // Geçici olarak admin kullanıcı ile devam et
                VaultUser tempCurrentUser = _currentUser;
                _currentUser = adminUser;

                try
                {
                    // Kullanıcı zaten var mı kontrol et
                    if (_index.Users.Any(u => u.Username == newUser.Username))
                    {
                        throw new InvalidOperationException($"Kullanıcı zaten mevcut: {newUser.Username}");
                    }

                    // Şifre hash'i hesapla ve kullanıcıyı ekle
                    string originalPassword = newUser.Password;
                    newUser.ComputePasswordHash();
                    // Şifreyi tekrar ayarla, normal kullanıcı için de gerekebilir
                    newUser.Password = originalPassword;
                    _index.Users.Add(newUser);

                    // İndeksi güncelle
                    await SaveIndexAsync();

                    Console.WriteLine($"Kullanıcı eklendi: {newUser.Username}");
                }
                finally
                {
                    // Önceki kullanıcıya geri dön
                    _currentUser = tempCurrentUser;
                }
            }
            else if (_currentUser != null && _currentUser.AccessLevel == AccessLevel.Administrator)
            {
                // Kullanıcı zaten var mı kontrol et
                if (_index.Users.Any(u => u.Username == newUser.Username))
                {
                    throw new InvalidOperationException($"Kullanıcı zaten mevcut: {newUser.Username}");
                }

                // Şifre hash'i hesapla ve kullanıcıyı ekle
                string originalPassword = newUser.Password;
                newUser.ComputePasswordHash();
                // Şifreyi tekrar ayarla
                newUser.Password = originalPassword;
                _index.Users.Add(newUser);

                // İndeksi güncelle
                await SaveIndexAsync();

                Console.WriteLine($"Kullanıcı eklendi: {newUser.Username}");
            }
            else if (adminUser != null && adminUser.AccessLevel == AccessLevel.Administrator)
            {
                // İlk giriş durumu için
                if (_index.Users.Any(u => u.Username == newUser.Username))
                {
                    throw new InvalidOperationException($"Kullanıcı zaten mevcut: {newUser.Username}");
                }

                // Şifre hash'i hesapla ve kullanıcıyı ekle
                string originalPassword = newUser.Password;
                newUser.ComputePasswordHash();
                // Şifreyi tekrar ayarla
                newUser.Password = originalPassword;
                _index.Users.Add(newUser);

                // İndeksi güncelle
                _currentUser = adminUser; // Geçici olarak admin kullanıcıyı ayarla
                await SaveIndexAsync();
                _currentUser = null; // Oturumu sıfırla

                Console.WriteLine($"Kullanıcı eklendi: {newUser.Username}");
            }
            else
            {
                throw new UnauthorizedAccessException("Kullanıcı ekleme yetkisi yok");
            }
        }

        public async Task<List<VaultFile>> ListFilesAsync()
        {
            if (_currentUser == null)
            {
                throw new UnauthorizedAccessException("Oturum açılmamış");
            }

            // Kullanıcı erişim seviyesine göre filtreleme yap
            return _index.Files
                .Where(f => (int)f.Metadata.AccessLevel <= (int)_currentUser.AccessLevel)
                .ToList();
        }

        public async Task AddFileAsync(string filePath, FileMetadata metadata)
        {
            if (_currentUser == null)
            {
                throw new UnauthorizedAccessException("Oturum açılmamış");
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Dosya bulunamadı", filePath);
            }

            // Dosya adına göre benzersiz bir kimlik oluştur
            string fileId = Guid.NewGuid().ToString();
            string storageFileName = $"{fileId}.frgl";
            string storagePath = Path.Combine(_settings.VaultDirectory, storageFileName);

            // Dosya arşivini oluştur
            FragileOptions options = new()
            {
                EnableErrorCorrection = _settings.EnableErrorCorrection,
                ErrorCorrectionLevel = _settings.ErrorCorrectionLevel,
                EnableEncryption = true,
                EncryptionMethod = _settings.EncryptionMethod,
                Password = _currentUser.PasswordHash, // Kullanıcının password hash'ini şifre olarak kullan
                CompressionAlgorithm = CompressionAlgorithm.Deflate,
                CompressionLevel = _settings.CompressionLevel,
                EnableChecksumVerification = true,
                ChecksumAlgorithm = _settings.ChecksumAlgorithm
            };

            try
            {
                // Dosyayı arşivle
                using (FragileArchive archive = await FragileArchive.CreateAsync(storagePath, options))
                {
                    // Dosyayı ekle
                    await archive.AddFileAsync(filePath);

                    // Arşiv metadatasını ayarla
                    archive.Metadata.Title = metadata.OriginalFileName;
                    archive.Metadata.Description = $"Encrypted file: {metadata.OriginalFileName}";
                    foreach (string tag in metadata.Tags)
                    {
                        archive.Metadata.Tags.Add(tag);
                    }

                    // Dosya metadatasını özel propertylere ekle
                    archive.Metadata.AddProperty("OriginalFileName", metadata.OriginalFileName);
                    archive.Metadata.AddProperty("FileType", metadata.FileType);
                    archive.Metadata.AddProperty("MimeType", metadata.MimeType);
                    archive.Metadata.AddProperty("CreationTime", metadata.CreationTime.ToString("o"));
                    archive.Metadata.AddProperty("LastModified", metadata.LastModified.ToString("o"));
                    archive.Metadata.AddProperty("AccessLevel", metadata.AccessLevel.ToString());

                    foreach (KeyValuePair<string, string> prop in metadata.CustomProperties)
                    {
                        archive.Metadata.AddProperty(prop.Key, prop.Value);
                    }

                    // Arşivi kaydet
                    await archive.SaveAsync();
                }

                // Dosya bilgisini indekse ekle
                VaultFile vaultFile = new()
                {
                    Id = fileId,
                    StorageFileName = storageFileName,
                    Metadata = metadata,
                    AddedToVault = DateTime.Now,
                    AddedByUser = _currentUser.Username
                };

                _index.Files.Add(vaultFile);

                // İndeksi güncelle
                await SaveIndexAsync();

                Console.WriteLine($"Dosya kasaya eklendi: {metadata.OriginalFileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dosya ekleme hatası: {ex.Message}");

                // Yarım kalan dosyayı temizle
                if (File.Exists(storagePath))
                {
                    File.Delete(storagePath);
                }

                throw;
            }
        }

        public async Task<string> ExtractFileAsync(string fileName, string targetDirectory)
        {
            if (_currentUser == null)
            {
                throw new UnauthorizedAccessException("Oturum açılmamış");
            }

            // Dosya adına göre dosyayı bul
            VaultFile? file = _index.Files.FirstOrDefault(f => f.Metadata.OriginalFileName == fileName);

            if (file == null)
            {
                throw new FileNotFoundException($"Dosya kasada bulunamadı: {fileName}");
            }

            // Kullanıcının dosyaya erişim yetkisi var mı?
            if ((int)file.Metadata.AccessLevel > (int)_currentUser.AccessLevel)
            {
                Console.WriteLine("Yetkisiz erişim: Dosyanın erişim seviyesi kullanıcının seviyesinden yüksek");
                return null;
            }

            string storagePath = Path.Combine(_settings.VaultDirectory, file.StorageFileName);
            if (!File.Exists(storagePath))
            {
                throw new FileNotFoundException($"Arşiv dosyası bulunamadı: {file.StorageFileName}");
            }

            // Arşiv dosyasını aç
            FragileOptions options = new()
            {
                EnableErrorCorrection = _settings.EnableErrorCorrection,
                EnableEncryption = true,
                EncryptionMethod = _settings.EncryptionMethod,
                Password = _currentUser.PasswordHash, // Kullanıcının password hash'ini şifre olarak kullan
                EnableChecksumVerification = true,
                ChecksumAlgorithm = _settings.ChecksumAlgorithm,
                CompressionAlgorithm = CompressionAlgorithm.Deflate,
                CompressionLevel = _settings.CompressionLevel
            };

            try
            {
                // Hedef dizini oluştur
                Directory.CreateDirectory(targetDirectory);

                string targetFilePath = Path.Combine(targetDirectory, file.Metadata.OriginalFileName);

                // Arşivi aç ve dosyayı çıkar
                using FragileArchive archive = await FragileArchive.OpenAsync(storagePath, options);

                // İlk dosyayı çıkar (her arşivde sadece bir dosya var)
                FragileArchiveEntry? entry = archive.Entries.FirstOrDefault(e => !e.IsDirectory);
                if (entry != null)
                {
                    await archive.ExtractAsync(entry.Path, targetFilePath);
                    Console.WriteLine($"Dosya çıkarıldı: {targetFilePath}");
                    return targetFilePath;
                }
                else
                {
                    throw new InvalidOperationException("Arşivde dosya bulunamadı");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dosya çıkarma hatası: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteFileAsync(string fileName)
        {
            if (_currentUser == null)
            {
                throw new UnauthorizedAccessException("Oturum açılmamış");
            }

            // Dosya adına göre dosyayı bul
            VaultFile? file = _index.Files.FirstOrDefault(f => f.Metadata.OriginalFileName == fileName);

            if (file == null)
            {
                throw new FileNotFoundException($"Dosya kasada bulunamadı: {fileName}");
            }

            // Kullanıcı yönetici değilse veya dosyanın sahibi değilse
            if (_currentUser.AccessLevel != AccessLevel.Administrator &&
                file.AddedByUser != _currentUser.Username)
            {
                throw new UnauthorizedAccessException("Dosyayı silme yetkiniz yok");
            }

            string storagePath = Path.Combine(_settings.VaultDirectory, file.StorageFileName);

            try
            {
                // Dosyayı indeksten kaldır
                _index.Files.Remove(file);

                // İndeksi güncelle
                await SaveIndexAsync();

                // Belleği temizleyerek dosyaya olan referansları serbest bırakalım
                GC.Collect();
                GC.WaitForPendingFinalizers();

                // Dosya sisteminden sil (retry mekanizması ile)
                if (File.Exists(storagePath))
                {
                    int retryCount = 0;
                    bool deleted = false;
                    while (!deleted && retryCount < 3)
                    {
                        try
                        {
                            File.Delete(storagePath);
                            deleted = true;
                        }
                        catch (IOException)
                        {
                            retryCount++;
                            // Biraz bekleyelim
                            await Task.Delay(500);
                            // Tekrar temizleyelim
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                        }
                    }

                    if (!deleted)
                    {
                        Console.WriteLine($"Uyarı: Dosya indeksten kaldırıldı ancak fiziksel olarak silinemedi: {storagePath}");
                    }
                    else
                    {
                        Console.WriteLine($"Dosya kasadan silindi: {fileName}");
                    }
                }
                else
                {
                    Console.WriteLine($"Dosya kasadan silindi: {fileName} (Fiziksel dosya zaten mevcut değil)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dosya silme hatası: {ex.Message}");
                throw;
            }
        }
    }
}