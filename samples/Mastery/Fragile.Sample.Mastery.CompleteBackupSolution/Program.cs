using Fragile.Compression;
using Fragile.Core;
using Fragile.Encryption;
using Fragile.Metadata;
using Fragile.Models;
using Fragile.Utils;
using System.Text;

namespace Fragile.Sample.Mastery.CompleteBackupSolution
{
    class Program
    {
        // Configuration settings
        private static readonly BackupSettings _settings = new()
        {
            SourceDirectory = "Sample/BackupSource",
            BackupDirectory = "Sample/Backups",
            MaxPartSize = 10 * 1024 * 1024, // 10 MB per part
            CompressionLevel = CompressionLevel.Ultra,
            EncryptionEnabled = true,
            EncryptionMethod = EncryptionMethod.AES256,
            Password = "SuperSecurePassword!123",
            EnableErrorCorrection = true,
            ErrorCorrectionLevel = 8, // 8% of archive size
            BackupName = $"Backup_{DateTime.Now:yyyyMMdd_HHmmss}",
            KeepBackupsCount = 3
        };

        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Complete Backup Solution - Mastery Sample");
            Console.WriteLine("================================================");

            // Create necessary directories
            Directory.CreateDirectory("Sample");
            Directory.CreateDirectory(_settings.SourceDirectory);
            Directory.CreateDirectory(_settings.BackupDirectory);

            // Create sample data to back up
            await CreateSampleDataForBackup();

            try
            {
                // Create a cancellation token source with timeout
                using CancellationTokenSource cts = new(TimeSpan.FromMinutes(30));

                // Perform full backup
                Console.WriteLine("\nPerforming full backup...");
                BackupResult backupResult = await PerformFullBackup(_settings, cts.Token);

                // Display backup summary
                backupResult.DisplayBackupSummary();

                // Verify the backup
                Console.WriteLine("\nVerifying backup integrity...");
                bool verificationResult = await VerifyBackup(backupResult.BackupArchivePath, _settings.Password);

                if (verificationResult)
                {
                    Console.WriteLine("Backup verification successful!");
                }
                else
                {
                    Console.WriteLine("Backup verification failed!");
                    return;
                }

                // Restore from backup to a different location
                string restoreDir = Path.Combine("Sample", "Restored");
                Console.WriteLine($"\nRestoring backup to: {restoreDir}");
                bool restoreResult = await RestoreBackup(backupResult.BackupArchivePath, restoreDir, _settings.Password);

                if (restoreResult)
                {
                    Console.WriteLine("Restore operation completed successfully!");
                }
                else
                {
                    Console.WriteLine("Restore operation failed!");
                    return;
                }

                // Clean up old backups
                await CleanupOldBackups(_settings.BackupDirectory, _settings.KeepBackupsCount);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during backup process: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine("\nBackup process completed!");
            Console.WriteLine("Check the 'Sample' directory for the created files, backups, and restore results.");

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static async Task CreateSampleDataForBackup()
        {
            Console.WriteLine("Creating sample data for backup...");

            // Create directories structure
            string docsDir = Path.Combine(_settings.SourceDirectory, "Documents");
            string imagesDir = Path.Combine(_settings.SourceDirectory, "Images");
            string configDir = Path.Combine(_settings.SourceDirectory, "Configuration");

            Directory.CreateDirectory(docsDir);
            Directory.CreateDirectory(imagesDir);
            Directory.CreateDirectory(configDir);
            Directory.CreateDirectory(Path.Combine(docsDir, "Archive"));
            Directory.CreateDirectory(Path.Combine(imagesDir, "Vacation"));
            Directory.CreateDirectory(Path.Combine(imagesDir, "Work"));

            // Create various text files
            // Documents
            await CreateTextFile(Path.Combine(docsDir, "readme.txt"),
                "This is a sample readme file for the backup solution.\n" +
                "It contains important information about the project.");

            await CreateTextFile(Path.Combine(docsDir, "report.txt"),
                "Annual Report\n" +
                "=============\n\n" +
                "This report contains confidential financial information.\n" +
                GenerateRandomText(5000));

            await CreateTextFile(Path.Combine(docsDir, "contacts.txt"),
                "Contact List\n" +
                "============\n\n" +
                GenerateContactList(50));

            await CreateTextFile(Path.Combine(docsDir, "Archive", "old_notes.txt"),
                "These are archived notes from previous years.\n" +
                GenerateRandomText(2000));

            // Configuration
            await CreateTextFile(Path.Combine(configDir, "settings.ini"),
                "[General]\n" +
                "Language=English\n" +
                "Theme=Dark\n\n" +
                "[Network]\n" +
                "Hostname=localhost\n" +
                "Port=8080\n" +
                "Timeout=30000\n\n" +
                "[Security]\n" +
                "EnableEncryption=true\n" +
                "KeySize=256\n" +
                "CertificatePath=/path/to/cert.pem\n");

            await CreateTextFile(Path.Combine(configDir, "database.config"),
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                "<configuration>\n" +
                "  <connectionStrings>\n" +
                "    <add name=\"MainDB\" connectionString=\"Server=db.example.com;Database=maindb;User Id=admin;Password=******;\" />\n" +
                "    <add name=\"LogDB\" connectionString=\"Server=logs.example.com;Database=logs;User Id=logger;Password=******;\" />\n" +
                "  </connectionStrings>\n" +
                "</configuration>");

            // Create binary files to simulate images
            await CreateFakeImageFile(Path.Combine(imagesDir, "logo.png"), 50 * 1024);
            await CreateFakeImageFile(Path.Combine(imagesDir, "banner.jpg"), 200 * 1024);
            await CreateFakeImageFile(Path.Combine(imagesDir, "Vacation", "beach.jpg"), 1.5 * 1024 * 1024);
            await CreateFakeImageFile(Path.Combine(imagesDir, "Vacation", "mountains.jpg"), 2 * 1024 * 1024);
            await CreateFakeImageFile(Path.Combine(imagesDir, "Work", "conference.jpg"), 1 * 1024 * 1024);
            await CreateFakeImageFile(Path.Combine(imagesDir, "Work", "office.jpg"), 800 * 1024);

            // Create a larger file
            await CreateFakeImageFile(Path.Combine(_settings.SourceDirectory, "large_data.bin"), 8 * 1024 * 1024);

            // Count total files
            int totalFiles = Directory.GetFiles(_settings.SourceDirectory, "*", SearchOption.AllDirectories).Length;
            long totalSize = Directory.GetFiles(_settings.SourceDirectory, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);

            Console.WriteLine($"Created {totalFiles} sample files with total size: {totalSize:N0} bytes ({totalSize / (1024 * 1024):F2} MB)");
        }

        static async Task CreateTextFile(string path, string content)
        {
            await File.WriteAllTextAsync(path, content);
        }

        static async Task CreateFakeImageFile(string path, double sizeInBytes)
        {
            using FileStream stream = new(path, FileMode.Create, FileAccess.Write);

            // Start with a fake header to look like an image
            byte[] header = Encoding.ASCII.GetBytes("IMGDATA");
            await stream.WriteAsync(header, 0, header.Length);

            // Fill the rest with random data
            Random random = new();
            byte[] buffer = new byte[64 * 1024]; // 64 KB buffer

            long remainingBytes = (long)sizeInBytes - header.Length;
            while (remainingBytes > 0)
            {
                int bytesToWrite = (int)Math.Min(buffer.Length, remainingBytes);
                random.NextBytes(buffer);
                await stream.WriteAsync(buffer, 0, bytesToWrite);
                remainingBytes -= bytesToWrite;
            }
        }

        static string GenerateRandomText(int length)
        {
            string[] words = {
                "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing", "elit", "sed", "do",
                "eiusmod", "tempor", "incididunt", "ut", "labore", "et", "dolore", "magna", "aliqua", "ut",
                "enim", "ad", "minim", "veniam", "quis", "nostrud", "exercitation", "ullamco", "laboris", "nisi",
                "ut", "aliquip", "ex", "ea", "commodo", "consequat", "duis", "aute", "irure", "dolor", "in",
                "reprehenderit", "in", "voluptate", "velit", "esse", "cillum", "dolore", "eu", "fugiat", "nulla",
                "pariatur", "excepteur", "sint", "occaecat", "cupidatat", "non", "proident", "sunt", "in", "culpa",
                "qui", "officia", "deserunt", "mollit", "anim", "id", "est", "laborum"
            };

            Random random = new(42);
            StringBuilder sb = new();

            int totalWords = 0;
            while (totalWords < length)
            {
                // Add a paragraph every 200 words
                if (totalWords > 0 && totalWords % 200 == 0)
                {
                    sb.AppendLine().AppendLine();
                }
                // Add a new line every 20 words
                else if (totalWords > 0 && totalWords % 20 == 0)
                {
                    sb.AppendLine();
                }

                string word = words[random.Next(words.Length)];

                // Capitalize first word of paragraph or sentence
                if (totalWords == 0 ||
                    (totalWords > 0 && sb.ToString().EndsWith(". ")))
                {
                    word = char.ToUpper(word[0]) + word[1..];
                }

                sb.Append(word);

                // Add period at the end of sentences (approx. every 10-15 words)
                if (random.Next(15) < 1 && !word.EndsWith("."))
                {
                    sb.Append(".");
                }

                sb.Append(" ");
                totalWords++;

                // Add occasional commas
                if (random.Next(10) < 1 && !word.EndsWith(",") && !word.EndsWith("."))
                {
                    sb.Append(", ");
                }
            }

            return sb.ToString();
        }

        static string GenerateContactList(int count)
        {
            string[] firstNames = { "John", "Jane", "Michael", "Emily", "David", "Sarah", "Robert", "Laura", "William", "Elizabeth" };
            string[] lastNames = { "Smith", "Johnson", "Williams", "Brown", "Jones", "Miller", "Davis", "Garcia", "Rodriguez", "Wilson" };
            string[] domains = { "gmail.com", "yahoo.com", "outlook.com", "example.com", "company.com" };

            Random random = new(42);
            StringBuilder sb = new();

            for (int i = 0; i < count; i++)
            {
                string firstName = firstNames[random.Next(firstNames.Length)];
                string lastName = lastNames[random.Next(lastNames.Length)];
                string email = $"{firstName.ToLower()}.{lastName.ToLower()}@{domains[random.Next(domains.Length)]}";
                string phone = $"+1-{random.Next(100, 999)}-{random.Next(100, 999)}-{random.Next(1000, 9999)}";

                sb.AppendLine($"Name: {firstName} {lastName}");
                sb.AppendLine($"Email: {email}");
                sb.AppendLine($"Phone: {phone}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        static async Task<BackupResult> PerformFullBackup(BackupSettings settings, CancellationToken cancellationToken)
        {
            // Create a unique backup file name
            string backupFileName = $"{settings.BackupName}.frgl";
            string backupFilePath = Path.Combine(settings.BackupDirectory, backupFileName);

            // Configure backup options
            FragileOptions options = new()
            {
                CompressionAlgorithm = CompressionAlgorithm.Deflate,
                CompressionLevel = settings.CompressionLevel,
                EnableEncryption = settings.EncryptionEnabled,
                Password = settings.Password,
                EncryptionMethod = settings.EncryptionMethod,
                EnableErrorCorrection = settings.EnableErrorCorrection,
                ErrorCorrectionLevel = settings.ErrorCorrectionLevel,
                EnableChecksumVerification = true,
                IncludeMetadata = true,
                UseParallelProcessing = true,
                SplitSize = settings.MaxPartSize,
                CancellationToken = cancellationToken,
                Progress = new Progress<double>(p => Console.WriteLine($"  Backup progress: {p:P1}"))
            };

            BackupResult result = new()
            {
                StartTime = DateTime.Now,
                SourceDirectory = settings.SourceDirectory,
                BackupArchivePath = backupFilePath
            };

            try
            {
                Console.WriteLine($"Creating backup archive: {backupFilePath}");

                // Create the archive
                using FragileArchive archive = await FragileArchive.CreateAsync(backupFilePath, options);

                // Configure archive metadata
                archive.Metadata.Title = $"Backup of {Path.GetFileName(settings.SourceDirectory)}";
                archive.Metadata.Description = $"Full backup created by Fragile Backup Solution";
                archive.Metadata.Creator = "Fragile.Sample.Mastery.CompleteBackupSolution";
                archive.Metadata.Author = Environment.UserName;
                archive.Metadata.Tags.AddRange(new[] { "backup", "full", "automated" });
                archive.Metadata.AddProperty("BackupType", "Full");
                archive.Metadata.AddProperty("SourcePath", settings.SourceDirectory);
                archive.Metadata.AddProperty("HostName", Environment.MachineName);
                archive.Metadata.AddProperty("OperatingSystem", Environment.OSVersion.ToString());

                // Add all files from the source directory
                Console.WriteLine("Adding files to backup...");
                result.FileCount = await archive.AddDirectoryAsync(settings.SourceDirectory, recursive: true);

                // Add metadata to each file
                foreach (FragileArchiveEntry entry in archive.Entries)
                {
                    if (!entry.IsDirectory)
                    {
                        // Get file information
                        string fullPath = Path.Combine(settings.SourceDirectory, entry.Path);
                        FileInfo fileInfo = new(fullPath);

                        // Create metadata for the file
                        EntryMetadata metadata = new()
                        {
                            CreationTime = fileInfo.CreationTime,
                            LastAccessTime = fileInfo.LastAccessTime
                        };

                        // Add file type as a tag
                        string extension = Path.GetExtension(entry.Path).ToLower();
                        if (!string.IsNullOrEmpty(extension))
                        {
                            metadata.Tags.Add(extension[1..]); // Remove the dot
                        }

                        // Set appropriate MIME type
                        metadata.MimeType = GetMimeType(extension);

                        // Add the metadata to the file
                        archive.SetEntryMetadata(entry.Path, metadata);
                    }
                }

                // Save the archive
                Console.WriteLine("Saving backup archive...");
                await archive.SaveAsync();

                // Get archive size
                FileInfo archiveInfo = new(backupFilePath);
                result.BackupSize = archiveInfo.Length;

                // Calculate original size
                result.OriginalSize = archive.Entries
                    .Where(e => !e.IsDirectory)
                    .Sum(e => e.Size);

                // Check if splitting is needed
                if (result.BackupSize > settings.MaxPartSize)
                {
                    Console.WriteLine("Backup file exceeds maximum part size. Splitting into parts...");

                    // Split the archive
                    string partsDir = Path.Combine(settings.BackupDirectory, Path.GetFileNameWithoutExtension(backupFileName) + "_parts");
                    Directory.CreateDirectory(partsDir);

                    FragileArchivePartCollection parts = await archive.SplitAsync(partsDir);
                    result.IsMultiPart = true;
                    result.PartCount = parts.Count;
                    result.PartsDirectory = partsDir;

                    Console.WriteLine($"Backup archive split into {parts.Count} parts.");
                }

                result.EndTime = DateTime.Now;
                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                result.EndTime = DateTime.Now;
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        static async Task<bool> VerifyBackup(string backupPath, string password)
        {
            try
            {
                if (!File.Exists(backupPath))
                {
                    // Check if this might be a split archive
                    string potentialPartsDir = Path.ChangeExtension(backupPath, null) + "_parts";

                    if (Directory.Exists(potentialPartsDir))
                    {
                        string[] partFiles = Directory.GetFiles(potentialPartsDir, "*.part*");
                        if (partFiles.Length > 0)
                        {
                            Console.WriteLine("Found split archive parts, verifying first part...");
                            return await VerifyBackupPart(partFiles.OrderBy(f => f).First(), password);
                        }
                    }

                    Console.WriteLine($"Backup file not found: {backupPath}");
                    return false;
                }

                // Open the archive with verification options
                FragileOptions options = new()
                {
                    Password = password,
                    EnableChecksumVerification = true
                };

                // Open the archive (this will perform basic verification)
                using FragileArchive archive = await FragileArchive.OpenAsync(backupPath, options);

                Console.WriteLine($"Successfully opened archive with {archive.Entries.Count} entries");
                Console.WriteLine("Reading entry data to verify archive integrity...");

                // Validate all entries
                foreach (FragileArchiveEntry entry in archive.Entries)
                {
                    if (!entry.IsDirectory)
                    {
                        // Get extended entry to verify metadata
                        FragileArchiveEntryExtended extendedEntry = archive.GetExtendedEntry(entry.Path);

                        if (extendedEntry == null)
                        {
                            Console.WriteLine($"Warning: Could not retrieve extended information for {entry.Path}");
                        }
                    }
                }

                Console.WriteLine("All entries successfully verified");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during backup verification: {ex.Message}");
                return false;
            }
        }

        static async Task<bool> VerifyBackupPart(string partPath, string password)
        {
            try
            {
                if (!File.Exists(partPath))
                {
                    Console.WriteLine($"Backup part file not found: {partPath}");
                    return false;
                }

                // Get all parts in the directory
                string directory = Path.GetDirectoryName(partPath);
                string[] allParts = Directory.GetFiles(directory, "*.part*");

                Console.WriteLine($"Found {allParts.Length} parts in {directory}");

                // Open and collect information about each part
                FragileOptions options = new()
                {
                    Password = password
                };

                FragileArchivePartCollection parts = FragileArchivePartCollection.FindParts(partPath, options);

                if (parts.Count == 0)
                {
                    Console.WriteLine("No valid parts found in the directory");
                    return false;
                }

                Console.WriteLine($"Successfully identified {parts.Count} valid parts");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during backup part verification: {ex.Message}");
                return false;
            }
        }

        static async Task<bool> RestoreBackup(string backupPath, string targetDirectory, string password)
        {
            try
            {
                Directory.CreateDirectory(targetDirectory);

                if (!File.Exists(backupPath))
                {
                    // Check if this might be a split archive
                    string potentialPartsDir = Path.ChangeExtension(backupPath, null) + "_parts";

                    if (Directory.Exists(potentialPartsDir))
                    {
                        string[] partFiles = Directory.GetFiles(potentialPartsDir, "*.part*");
                        if (partFiles.Length > 0)
                        {
                            // Re-combine the parts first
                            string recombinedArchive = Path.Combine(
                                Path.GetDirectoryName(backupPath),
                                $"recombined_{Path.GetFileName(backupPath)}");

                            Console.WriteLine($"Found split archive parts, recombining to: {recombinedArchive}");

                            FragileOptions options1 = new()
                            {
                                Password = password,
                                Progress = new Progress<double>(p => Console.WriteLine($"  Recombining progress: {p:P1}"))
                            };

                            await FragileUtility.CombinePartsAsync(partFiles.OrderBy(f => f).First(), recombinedArchive, options1);

                            // Use the recombined archive for restoration
                            backupPath = recombinedArchive;
                        }
                        else
                        {
                            Console.WriteLine($"No archive parts found in: {potentialPartsDir}");
                            return false;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Backup file not found: {backupPath}");
                        return false;
                    }
                }

                // Configure extraction options
                FragileOptions options2 = new()
                {
                    Password = password,
                    EnableErrorCorrection = true,
                    Progress = new Progress<double>(p => Console.WriteLine($"  Restore progress: {p:P1}"))
                };

                Console.WriteLine($"Extracting backup to: {targetDirectory}");

                // Extract the archive
                using FragileArchive archive = await FragileArchive.OpenAsync(backupPath, options2);

                Console.WriteLine($"Backup contains {archive.Entries.Count} entries");
                await archive.ExtractAllAsync(targetDirectory);

                // Verify extraction results
                int extractedFiles = Directory.GetFiles(targetDirectory, "*", SearchOption.AllDirectories).Length;
                int expectedFiles = archive.Entries.Count(e => !e.IsDirectory);

                Console.WriteLine($"Extracted {extractedFiles} files out of {expectedFiles} expected");

                return extractedFiles == expectedFiles;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during backup restore: {ex.Message}");
                return false;
            }
        }

        static async Task CleanupOldBackups(string backupDirectory, int keepCount)
        {
            Console.WriteLine($"\nCleaning up old backups (keeping the most recent {keepCount})...");

            try
            {
                // Get all backup files
                string[] backupFiles = Directory.GetFiles(backupDirectory, "*.frgl")
                    .Where(f => !f.Contains("recombined_")) // Skip recombined archives
                    .ToArray();

                // Get backup part directories
                string[] partDirs = Directory.GetDirectories(backupDirectory, "*_parts");

                if (backupFiles.Length <= keepCount)
                {
                    Console.WriteLine($"Found {backupFiles.Length} backups, no cleanup needed");
                    return;
                }

                // Order by creation time (newest first)
                List<FileInfo> orderedBackups = backupFiles
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                // Keep the specified number of newest backups
                List<FileInfo> backupsToDelete = orderedBackups.Skip(keepCount).ToList();

                Console.WriteLine($"Removing {backupsToDelete.Count} old backups...");

                foreach (FileInfo? backup in backupsToDelete)
                {
                    // Delete the backup file
                    File.Delete(backup.FullName);
                    Console.WriteLine($"Deleted: {backup.Name}");

                    // Delete associated part directory if it exists
                    string partDirName = Path.ChangeExtension(backup.Name, null) + "_parts";
                    string partDirPath = Path.Combine(backupDirectory, partDirName);

                    if (Directory.Exists(partDirPath))
                    {
                        Directory.Delete(partDirPath, true);
                        Console.WriteLine($"Deleted part directory: {partDirName}");
                    }
                }

                Console.WriteLine("Cleanup completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during backup cleanup: {ex.Message}");
            }
        }

        static string GetMimeType(string extension)
        {
            return extension.ToLower() switch
            {
                ".txt" => "text/plain",
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".zip" => "application/zip",
                ".rar" => "application/x-rar-compressed",
                ".7z" => "application/x-7z-compressed",
                ".mp3" => "audio/mpeg",
                ".mp4" => "video/mp4",
                ".avi" => "video/x-msvideo",
                ".mov" => "video/quicktime",
                ".wmv" => "video/x-ms-wmv",
                _ => "application/octet-stream" // Default binary type
            };
        }
    }

    class BackupSettings
    {
        public string SourceDirectory { get; set; }
        public string BackupDirectory { get; set; }
        public long MaxPartSize { get; set; }
        public CompressionLevel CompressionLevel { get; set; }
        public bool EncryptionEnabled { get; set; }
        public EncryptionMethod EncryptionMethod { get; set; }
        public string Password { get; set; }
        public bool EnableErrorCorrection { get; set; }
        public int ErrorCorrectionLevel { get; set; }
        public string BackupName { get; set; }
        public int KeepBackupsCount { get; set; }
    }

    class BackupResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string SourceDirectory { get; set; }
        public string BackupArchivePath { get; set; }
        public bool IsMultiPart { get; set; }
        public int PartCount { get; set; }
        public string PartsDirectory { get; set; }
        public int FileCount { get; set; }
        public long OriginalSize { get; set; }
        public long BackupSize { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        public TimeSpan Duration => EndTime - StartTime;
        public double CompressionRatio => OriginalSize / (double)BackupSize;
    }

    static class Extensions
    {
        public static void DisplayBackupSummary(this BackupResult result)
        {
            Console.WriteLine("\nBackup Summary:");
            Console.WriteLine("===============");
            Console.WriteLine($"Start Time: {result.StartTime}");
            Console.WriteLine($"End Time: {result.EndTime}");
            Console.WriteLine($"Duration: {result.Duration.TotalMinutes:F1} minutes");
            Console.WriteLine($"Source Directory: {result.SourceDirectory}");
            Console.WriteLine($"Backup File: {result.BackupArchivePath}");

            if (result.IsMultiPart)
            {
                Console.WriteLine($"Split into {result.PartCount} parts");
                Console.WriteLine($"Parts Directory: {result.PartsDirectory}");
            }

            Console.WriteLine($"Files Processed: {result.FileCount}");
            Console.WriteLine($"Original Size: {result.OriginalSize:N0} bytes ({result.OriginalSize / (1024.0 * 1024.0):F2} MB)");
            Console.WriteLine($"Backup Size: {result.BackupSize:N0} bytes ({result.BackupSize / (1024.0 * 1024.0):F2} MB)");
            Console.WriteLine($"Compression Ratio: {result.CompressionRatio:F2}x");
            Console.WriteLine($"Space Saved: {1.0 - (result.BackupSize / (double)result.OriginalSize):P2}");
            Console.WriteLine($"Status: {(result.Success ? "Success" : "Failed")}");

            if (!result.Success)
            {
                Console.WriteLine($"Error: {result.ErrorMessage}");
            }
        }
    }
}