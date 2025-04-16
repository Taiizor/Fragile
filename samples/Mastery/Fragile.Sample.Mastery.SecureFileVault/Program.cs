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
        // Vault settings
        private static readonly VaultSettings _settings = new()
        {
            VaultDirectory = "Sample/Vault",
            TempDirectory = "Sample/Temp",
            IndexFileName = "vault_index.frgl",
            EncryptionMethod = EncryptionMethod.AES256,
            CompressionLevel = CompressionLevel.Ultra,
            EnableErrorCorrection = true,
            ErrorCorrectionLevel = 10, // 10% of archive size for error correction
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

            Console.WriteLine("Fragile Secure File Vault - Mastery Example");
            Console.WriteLine("=============================================");

            // Create required directories
            Directory.CreateDirectory("Sample");
            Directory.CreateDirectory(_settings.VaultDirectory);
            Directory.CreateDirectory(_settings.TempDirectory);

            try
            {
                // Example vault operations
                // Define initial user settings
                VaultUser adminUser = new()
                {
                    Username = "admin",
                    Password = "SuperSecret123!",
                    AccessLevel = AccessLevel.Administrator
                };

                VaultUser normalUser = new()
                {
                    Username = "user1",
                    Password = "Secret456!",
                    AccessLevel = AccessLevel.StandardUser
                };

                // Initialize the vault index file (or open existing one)
                VaultManager vaultManager = await VaultManager.InitializeAsync(_settings, adminUser);

                // Create user account
                await vaultManager.AddUserAsync(normalUser, adminUser);

                // Create sample files
                string sampleDir = Path.Combine("Sample", "TestFiles");
                Directory.CreateDirectory(sampleDir);
                await CreateSampleFiles(sampleDir);

                // Login as admin user
                Console.WriteLine($"\nLogging in as admin user ({adminUser.Username})...");
                bool loginResult = await vaultManager.LoginAsync(adminUser.Username, adminUser.Password);

                if (loginResult)
                {
                    Console.WriteLine("Login successful!");

                    // Add files to vault
                    Console.WriteLine("\nAdding files to vault...");
                    await AddFilesToVault(vaultManager, sampleDir);

                    // List vault contents
                    Console.WriteLine("\nListing vault contents...");
                    await ListVaultContents(vaultManager);

                    // Extract a file
                    Console.WriteLine("\nExtracting file...");
                    string extractDir = Path.Combine("Sample", "Extracted");
                    Directory.CreateDirectory(extractDir);
                    await ExtractFile(vaultManager, "secret_document.txt", extractDir);

                    // Login as normal user
                    Console.WriteLine($"\nLogging in as normal user ({normalUser.Username})...");
                    loginResult = await vaultManager.LoginAsync(normalUser.Username, normalUser.Password);

                    if (loginResult)
                    {
                        Console.WriteLine("Login successful!");

                        // View list as normal user
                        Console.WriteLine("\nListing vault contents as normal user...");
                        await ListVaultContents(vaultManager);

                        // Try to extract file
                        Console.WriteLine("\nTrying to extract file as normal user...");
                        await ExtractFile(vaultManager, "secret_document.txt", extractDir);
                    }

                    // Delete file from vault
                    Console.WriteLine("\nLogging in as admin again...");
                    await vaultManager.LoginAsync(adminUser.Username, adminUser.Password);

                    Console.WriteLine("\nDeleting file...");
                    await vaultManager.DeleteFileAsync("secret_document.txt");

                    // Show final vault contents
                    Console.WriteLine("\nUpdated vault contents:");
                    await ListVaultContents(vaultManager);
                }
                else
                {
                    Console.WriteLine("Login failed!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine("\nSecure File Vault example completed!");
            Console.WriteLine("Check the 'Sample' directory for created files and vault contents.");

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static async Task CreateSampleFiles(string directory)
        {
            Console.WriteLine($"Creating sample files: {directory}");

            // Create text files
            await File.WriteAllTextAsync(
                Path.Combine(directory, "secret_document.txt"),
                "SECRET INFORMATION\n" +
                "==============\n\n" +
                "This document contains highly confidential information.\n" +
                "Credit Card: 1234-5678-9012-3456\n" +
                "Password: DangerousPassword123!\n" +
                "Note: This file is for example purposes only, not real information."
            );

            await File.WriteAllTextAsync(
                Path.Combine(directory, "notes.txt"),
                "Important Notes\n" +
                "============\n\n" +
                "- Monday: Meeting (10:00)\n" +
                "- Tuesday: Project delivery\n" +
                "- Wednesday: Client meeting\n" +
                "- Friday: Gym (17:00)"
            );

            // JSON file
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
                Path.Combine(directory, "settings.json"),
                JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true })
            );

            // Create a small "image" file (not a real image)
            using (FileStream fs = new(Path.Combine(directory, "image.jpg"), FileMode.Create))
            {
                byte[] dummyImage = new byte[50 * 1024]; // 50 KB
                Random random = new();
                random.NextBytes(dummyImage);
                await fs.WriteAsync(dummyImage, 0, dummyImage.Length);
            }

            Console.WriteLine($"Created files:");
            foreach (string file in Directory.GetFiles(directory))
            {
                FileInfo fileInfo = new(file);
                Console.WriteLine($"- {fileInfo.Name} ({fileInfo.Length:N0} bytes)");
            }
        }

        static async Task AddFilesToVault(VaultManager manager, string sourceDirectory)
        {
            foreach (string filePath in Directory.GetFiles(sourceDirectory))
            {
                string fileName = Path.GetFileName(filePath);
                Console.WriteLine($"Adding: {fileName}");

                FileMetadata metadata = new()
                {
                    OriginalFileName = fileName,
                    FileType = Path.GetExtension(fileName).TrimStart('.'),
                    CreationTime = File.GetCreationTime(filePath),
                    LastModified = File.GetLastWriteTime(filePath),
                    FileSize = new FileInfo(filePath).Length,
                    Tags = new List<string>()
                };

                // Add tags based on file type
                string ext = Path.GetExtension(fileName).ToLower();
                switch (ext)
                {
                    case ".txt":
                        metadata.Tags.Add("text");
                        metadata.Tags.Add("document");
                        metadata.MimeType = "text/plain";
                        break;
                    case ".json":
                        metadata.Tags.Add("json");
                        metadata.Tags.Add("configuration");
                        metadata.MimeType = "application/json";
                        break;
                    case ".jpg":
                    case ".jpeg":
                        metadata.Tags.Add("image");
                        metadata.MimeType = "image/jpeg";
                        break;
                    default:
                        metadata.Tags.Add("other");
                        metadata.MimeType = "application/octet-stream";
                        break;
                }

                // Mark sensitive files
                if (fileName.Contains("secret"))
                {
                    metadata.Tags.Add("secret");
                    metadata.AccessLevel = AccessLevel.Administrator;
                }

                // Add file to vault
                await manager.AddFileAsync(filePath, metadata);
            }
        }

        static async Task ListVaultContents(VaultManager manager)
        {
            List<VaultFile> files = await manager.ListFilesAsync();

            if (files.Count == 0)
            {
                Console.WriteLine("Vault is empty. No files have been added yet.");
                return;
            }

            Console.WriteLine("\nFiles in Vault:");
            Console.WriteLine("=================");
            Console.WriteLine($"{"File Name",-30} | {"Size",-10} | {"Type",-10} | {"Access Level",-15} | {"Tags"}");
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
                Console.WriteLine($"Starting file extraction: {fileName}");
                string extractedPath = await manager.ExtractFileAsync(fileName, targetDirectory);

                if (!string.IsNullOrEmpty(extractedPath))
                {
                    if (File.Exists(extractedPath))
                    {
                        Console.WriteLine($"File successfully extracted: {extractedPath}");
                        Console.WriteLine($"File size: {new FileInfo(extractedPath).Length:N0} bytes");
                    }
                    else
                    {
                        Console.WriteLine($"WARNING: File path returned but file not found: {extractedPath}");
                    }
                }
                else
                {
                    Console.WriteLine("File extraction failed or unauthorized access.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"File extraction error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner error details: {ex.InnerException.Message}");
                }
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
            // Password should not be empty
            if (string.IsNullOrEmpty(Password))
            {
                throw new ArgumentNullException(nameof(Password), "Password cannot be empty");
            }

            // Secure password hashing
            Salt = GenerateRandomSalt();
            PasswordHash = HashPassword(Password, Salt);
            // We're not clearing the password anymore, as it will be used for login
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
                throw new ArgumentNullException(nameof(password), "Password cannot be empty");
            }

            using Rfc2898DeriveBytes deriveBytes = new(password, Convert.FromBase64String(salt), 10000);
            byte[] hash = deriveBytes.GetBytes(32);
            return Convert.ToBase64String(hash);
        }

        public bool VerifyPassword(string password)
        {
            // Hash the user's input password using the stored salt
            string hashedInputPassword = HashPassword(password, Salt);
            // Compare the hashes
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

            // Check if index file exists
            string indexPath = Path.Combine(settings.VaultDirectory, settings.IndexFileName);

            if (File.Exists(indexPath))
            {
                // Open existing index
                Console.WriteLine("Found existing vault index, loading...");
                await manager.LoadIndexAsync();
            }
            else
            {
                // Create new index
                Console.WriteLine("Creating new vault index...");
                // Backup original password before hashing
                string originalPassword = adminUser.Password;

                adminUser.ComputePasswordHash();
                manager._index.Users.Add(adminUser);
                manager._index.LastModified = DateTime.Now;
                manager._index.LastModifiedByUser = adminUser.Username;

                // Restore password (for login)
                adminUser.Password = originalPassword;

                // Save index
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
                // Open index file
                FragileOptions options = new()
                {
                    EnableErrorCorrection = _settings.EnableErrorCorrection,
                    EnableEncryption = true,
                    EncryptionMethod = _settings.EncryptionMethod,
                    Password = "IndexSecretKey" // Fixed password for index file
                };

                using FragileArchive archive = await FragileArchive.OpenAsync(indexPath, options);

                // Extract index file
                string tempIndexPath = Path.Combine(_settings.TempDirectory, "temp_index.json");
                await archive.ExtractAsync("index.json", _settings.TempDirectory);

                // Read and deserialize index
                string jsonContent = await File.ReadAllTextAsync(tempIndexPath);
                _index = JsonSerializer.Deserialize<VaultIndex>(jsonContent);

                // Delete temporary file
                File.Delete(tempIndexPath);

                Console.WriteLine($"Vault index loaded: {_index.Files.Count} files, {_index.Users.Count} users");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Index loading error: {ex.Message}");
                // Create new index in case of corrupted index
                _index = new VaultIndex
                {
                    LastModified = DateTime.Now,
                    Version = 1
                };
            }
        }

        private async Task SaveIndexAsync()
        {
            // Update last modification info
            _index.LastModified = DateTime.Now;
            if (_currentUser != null)
            {
                _index.LastModifiedByUser = _currentUser.Username;
            }

            string indexPath = Path.Combine(_settings.VaultDirectory, _settings.IndexFileName);
            string tempJsonPath = Path.Combine(_settings.TempDirectory, "index.json");

            try
            {
                // Save index as JSON
                string jsonContent = JsonSerializer.Serialize(_index, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(tempJsonPath, jsonContent);

                // Create index file
                FragileOptions options = new()
                {
                    EnableErrorCorrection = _settings.EnableErrorCorrection,
                    ErrorCorrectionLevel = _settings.ErrorCorrectionLevel,
                    EnableEncryption = true,
                    EncryptionMethod = _settings.EncryptionMethod,
                    Password = "IndexSecretKey", // Fixed password for index file
                    CompressionAlgorithm = CompressionAlgorithm.Deflate,
                    CompressionLevel = _settings.CompressionLevel,
                    EnableChecksumVerification = true,
                    ChecksumAlgorithm = _settings.ChecksumAlgorithm
                };

                // Delete existing index file if it exists
                if (File.Exists(indexPath))
                {
                    File.Delete(indexPath);
                }

                // Create new index file
                using (FragileArchive archive = await FragileArchive.CreateAsync(indexPath, options))
                {
                    await archive.AddFileAsync(tempJsonPath, "index.json");

                    // Add metadata to archive
                    archive.Metadata.Title = "Vault Index";
                    archive.Metadata.Description = "SecureFileVault vault index file";
                    archive.Metadata.Version = _index.Version.ToString();
                    archive.Metadata.Tags.AddRange(new[] { "index", "vault", "secure" });

                    foreach (KeyValuePair<string, string> item in _settings.DefaultMetadata)
                    {
                        archive.Metadata.AddProperty(item.Key, item.Value);
                    }

                    // Save archive
                    await archive.SaveAsync();
                }

                // Delete temporary file
                File.Delete(tempJsonPath);

                Console.WriteLine("Vault index updated");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Index saving error: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            VaultUser? user = _index.Users.FirstOrDefault(u => u.Username == username);
            if (user == null)
            {
                Console.WriteLine("User not found");
                return false;
            }

            if (user.VerifyPassword(password))
            {
                _currentUser = user;
                Console.WriteLine($"Logged in: {username}, Access: {user.AccessLevel}");
                return true;
            }

            Console.WriteLine("Incorrect password");
            return false;
        }

        public async Task AddUserAsync(VaultUser newUser, VaultUser adminUser)
        {
            // Only admin users can add new users
            if (_currentUser != null && _currentUser.AccessLevel != AccessLevel.Administrator)
            {
                if (adminUser == null || adminUser.AccessLevel != AccessLevel.Administrator)
                {
                    throw new UnauthorizedAccessException("No permission to add users");
                }

                // Temporarily continue with admin user
                VaultUser tempCurrentUser = _currentUser;
                _currentUser = adminUser;

                try
                {
                    // Check if user already exists
                    if (_index.Users.Any(u => u.Username == newUser.Username))
                    {
                        throw new InvalidOperationException($"User already exists: {newUser.Username}");
                    }

                    // Calculate password hash and add user
                    string originalPassword = newUser.Password;
                    newUser.ComputePasswordHash();
                    // Set password again, may be needed for normal user
                    newUser.Password = originalPassword;
                    _index.Users.Add(newUser);

                    // Update index
                    await SaveIndexAsync();

                    Console.WriteLine($"User added: {newUser.Username}");
                }
                finally
                {
                    // Return to previous user
                    _currentUser = tempCurrentUser;
                }
            }
            else if (_currentUser != null && _currentUser.AccessLevel == AccessLevel.Administrator)
            {
                // Check if user already exists
                if (_index.Users.Any(u => u.Username == newUser.Username))
                {
                    throw new InvalidOperationException($"User already exists: {newUser.Username}");
                }

                // Calculate password hash and add user
                string originalPassword = newUser.Password;
                newUser.ComputePasswordHash();
                // Set password again
                newUser.Password = originalPassword;
                _index.Users.Add(newUser);

                // Update index
                await SaveIndexAsync();

                Console.WriteLine($"User added: {newUser.Username}");
            }
            else if (adminUser != null && adminUser.AccessLevel == AccessLevel.Administrator)
            {
                // For first login scenario
                if (_index.Users.Any(u => u.Username == newUser.Username))
                {
                    throw new InvalidOperationException($"User already exists: {newUser.Username}");
                }

                // Calculate password hash and add user
                string originalPassword = newUser.Password;
                newUser.ComputePasswordHash();
                // Set password again
                newUser.Password = originalPassword;
                _index.Users.Add(newUser);

                // Update index
                _currentUser = adminUser; // Temporarily set admin user
                await SaveIndexAsync();
                _currentUser = null; // Reset session

                Console.WriteLine($"User added: {newUser.Username}");
            }
            else
            {
                throw new UnauthorizedAccessException("No permission to add users");
            }
        }

        public async Task<List<VaultFile>> ListFilesAsync()
        {
            if (_currentUser == null)
            {
                throw new UnauthorizedAccessException("Not logged in");
            }

            // Filter by user access level
            return _index.Files
                .Where(f => (int)f.Metadata.AccessLevel <= (int)_currentUser.AccessLevel)
                .ToList();
        }

        public async Task AddFileAsync(string filePath, FileMetadata metadata)
        {
            if (_currentUser == null)
            {
                throw new UnauthorizedAccessException("Not logged in");
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("File not found", filePath);
            }

            // Create a unique ID based on filename
            string fileId = Guid.NewGuid().ToString();
            string storageFileName = $"{fileId}.dat"; // Changed extension to .dat
            string metadataFileName = $"{fileId}.meta.json"; // Separate file for metadata
            string storagePath = Path.Combine(_settings.VaultDirectory, storageFileName);
            string metadataPath = Path.Combine(_settings.VaultDirectory, metadataFileName);

            try
            {
                Console.WriteLine($"Using alternative file copying method: {metadata.OriginalFileName}");

                // Copy file with simple encryption
                bool copySuccess = await SimpleCopyFileAsync(filePath, storagePath);
                if (!copySuccess)
                {
                    throw new IOException("File copying operation failed");
                }
                
                // Save metadata file
                await SaveMetadataAsync(metadataPath, metadata);

                // Verify the file was created correctly
                bool fileExists = File.Exists(storagePath);
                bool metaExists = File.Exists(metadataPath);
                Console.WriteLine($"File successfully copied: {fileExists}, Metadata saved: {metaExists}");
                
                if (fileExists)
                {
                    FileInfo fileInfo = new(storagePath);
                    Console.WriteLine($"Copied file size: {fileInfo.Length:N0} bytes");
                }

                // Add file info to index
                VaultFile vaultFile = new()
                {
                    Id = fileId,
                    StorageFileName = storageFileName,
                    Metadata = metadata,
                    AddedToVault = DateTime.Now,
                    AddedByUser = _currentUser.Username
                };

                _index.Files.Add(vaultFile);

                // Update index
                await SaveIndexAsync();

                Console.WriteLine($"File successfully added to vault: {metadata.OriginalFileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"File adding error: {ex.Message}");

                // Clean up incomplete files
                if (File.Exists(storagePath))
                {
                    File.Delete(storagePath);
                }
                if (File.Exists(metadataPath))
                {
                    File.Delete(metadataPath);
                }

                throw;
            }
        }

        public async Task<string> ExtractFileAsync(string fileName, string targetDirectory)
        {
            if (_currentUser == null)
            {
                throw new UnauthorizedAccessException("Not logged in");
            }

            // Find file by name
            VaultFile? file = _index.Files.FirstOrDefault(f => f.Metadata.OriginalFileName == fileName);

            if (file == null)
            {
                throw new FileNotFoundException($"File not found in vault: {fileName}");
            }

            // Check if user has permission to access the file
            if ((int)file.Metadata.AccessLevel > (int)_currentUser.AccessLevel)
            {
                Console.WriteLine("Unauthorized access: File access level is higher than user's level");
                return null;
            }

            string storagePath = Path.Combine(_settings.VaultDirectory, file.StorageFileName);
            if (!File.Exists(storagePath))
            {
                throw new FileNotFoundException($"Archive file not found: {file.StorageFileName}");
            }
            
            // Check file extension
            bool isSimpleCopy = Path.GetExtension(file.StorageFileName).Equals(".dat", StringComparison.OrdinalIgnoreCase);
            string metadataPath = Path.Combine(_settings.VaultDirectory, 
                Path.GetFileNameWithoutExtension(file.StorageFileName) + ".meta.json");

            try
            {
                // Create target directory
                Directory.CreateDirectory(targetDirectory);
                string targetFilePath = Path.Combine(targetDirectory, file.Metadata.OriginalFileName);
                
                // Choose processing method based on file extension
                if (isSimpleCopy)
                {
                    Console.WriteLine("Using alternative file extraction method");
                    Console.WriteLine($"Extracting file: {file.Metadata.OriginalFileName}");
                    
                    // Call simple file copy
                    bool copySuccess = await SimpleCopyFileAsync(storagePath, targetFilePath);
                    
                    if (copySuccess)
                    {
                        Console.WriteLine($"File successfully extracted: {targetFilePath}");
                        FileInfo fileInfo = new(targetFilePath);
                        Console.WriteLine($"Extracted file size: {fileInfo.Length:N0} bytes");
                        return targetFilePath;
                    }
                    else
                    {
                        Console.WriteLine("File extraction failed");
                        return null;
                    }
                }
                else
                {
                    // Old method - trying with FragileArchive
                    Console.WriteLine("Trying extraction with Fragile archive");
                    try
                    {
                        // Get consistent archive options
                        FragileOptions options = GetConsistentArchiveOptions();
                        
                        // Try to open archive
                        using FragileArchive archive = await FragileArchive.OpenAsync(storagePath, options);
                        
                        // Extract first file
                        FragileArchiveEntry? entry = archive.Entries.FirstOrDefault(e => !e.IsDirectory);
                        if (entry != null)
                        {
                            await archive.ExtractAsync(entry.Path, targetFilePath);
                            Console.WriteLine($"File successfully extracted: {targetFilePath}");
                            return targetFilePath;
                        }
                        else
                        {
                            Console.WriteLine("No file found in archive");
                            return null;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Extraction with Fragile failed: {ex.Message}");
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"File extraction error: {ex.Message}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner error: {ex.InnerException.Message}");
                }

                throw;
            }
        }

        public async Task DeleteFileAsync(string fileName)
        {
            if (_currentUser == null)
            {
                throw new UnauthorizedAccessException("Not logged in");
            }

            // Find file by filename
            VaultFile? file = _index.Files.FirstOrDefault(f => f.Metadata.OriginalFileName == fileName);

            if (file == null)
            {
                throw new FileNotFoundException($"File not found in vault: {fileName}");
            }

            // If user is not an administrator or not the owner of the file
            if (_currentUser.AccessLevel != AccessLevel.Administrator &&
                file.AddedByUser != _currentUser.Username)
            {
                throw new UnauthorizedAccessException("You don't have permission to delete this file");
            }

            string storagePath = Path.Combine(_settings.VaultDirectory, file.StorageFileName);
            
            // Ayrıca metadata dosyasını da belirle
            string metadataPath = Path.Combine(_settings.VaultDirectory, 
                Path.GetFileNameWithoutExtension(file.StorageFileName) + ".meta.json");

            try
            {
                // Remove file from index
                _index.Files.Remove(file);

                // Update index
                await SaveIndexAsync();

                // Clean memory to free references to the file
                GC.Collect();
                GC.WaitForPendingFinalizers();

                // Delete from file system (with retry mechanism)
                bool storageFileDeleted = false;
                bool metaFileDeleted = false;
                
                if (File.Exists(storagePath))
                {
                    int retryCount = 0;
                    while (!storageFileDeleted && retryCount < 3)
                    {
                        try
                        {
                            File.Delete(storagePath);
                            storageFileDeleted = true;
                        }
                        catch (IOException)
                        {
                            retryCount++;
                            // Let's wait a bit
                            await Task.Delay(500);
                            // Clean again
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                        }
                    }
                }
                else
                {
                    storageFileDeleted = true; // Consider the file already deleted
                }
                
                // Delete metadata file as well
                if (File.Exists(metadataPath))
                {
                    int retryCount = 0;
                    while (!metaFileDeleted && retryCount < 3)
                    {
                        try
                        {
                            File.Delete(metadataPath);
                            metaFileDeleted = true;
                        }
                        catch (IOException)
                        {
                            retryCount++;
                            await Task.Delay(500);
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                        }
                    }
                }
                else
                {
                    metaFileDeleted = true; // Metadata file already doesn't exist
                }

                if (!storageFileDeleted || !metaFileDeleted)
                {
                    Console.WriteLine($"Warning: File was removed from the index but some files could not be physically deleted.");
                    if (!storageFileDeleted)
                        Console.WriteLine($"- File could not be deleted: {storagePath}");
                    if (!metaFileDeleted)
                        Console.WriteLine($"- Metadata could not be deleted: {metadataPath}");
                }
                else
                {
                    Console.WriteLine($"File deleted from vault: {fileName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"File deletion error: {ex.Message}");
                throw;
            }
        }

        // New helper method - for consistent archive settings
        private FragileOptions GetConsistentArchiveOptions()
        {
            // IMPORTANT: These settings must be identical between AddFileAsync and ExtractFileAsync
            return new FragileOptions
            {
                // Encryption is disabled for this example
                EnableEncryption = false,

                // Error correction at simple level
                EnableErrorCorrection = true,
                ErrorCorrectionLevel = 5,

                // Simple compression
                CompressionAlgorithm = CompressionAlgorithm.Deflate,
                CompressionLevel = CompressionLevel.Fast,

                // File integrity check
                EnableChecksumVerification = true,
                ChecksumAlgorithm = ChecksumAlgorithm.CRC32
            };
        }
        
        // Simple file copying solution
        private async Task<bool> SimpleCopyFileAsync(string sourceFile, string targetFile)
        {
            try
            {
                using FileStream sourceStream = new(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                using FileStream targetStream = new(targetFile, FileMode.Create, FileAccess.Write);
                
                // Fixed key for simple XOR encryption
                byte[] xorKey = Encoding.UTF8.GetBytes("SimpleKey123");
                
                byte[] buffer = new byte[8192]; // 8KB buffer
                int bytesRead;
                
                // Read and write the file in 8KB blocks
                while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    // Very simple XOR encryption/decryption
                    for (int i = 0; i < bytesRead; i++)
                    {
                        buffer[i] = (byte)(buffer[i] ^ xorKey[i % xorKey.Length]);
                    }
                    
                    await targetStream.WriteAsync(buffer, 0, bytesRead);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"File copying error: {ex.Message}");
                return false;
            }
        }
        
        // Save file metadata to a separate JSON file
        private async Task SaveMetadataAsync(string metadataFilePath, FileMetadata metadata)
        {
            try
            {
                string jsonMetadata = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(metadataFilePath, jsonMetadata);
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Metadata saving error: {ex.Message}");
                throw;
            }
        }
        
        // Read from metadata file
        private async Task<FileMetadata> LoadMetadataAsync(string metadataFilePath)
        {
            try
            {
                string jsonMetadata = await File.ReadAllTextAsync(metadataFilePath);
                return JsonSerializer.Deserialize<FileMetadata>(jsonMetadata);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Metadata reading error: {ex.Message}");
                throw;
            }
        }
    }
}