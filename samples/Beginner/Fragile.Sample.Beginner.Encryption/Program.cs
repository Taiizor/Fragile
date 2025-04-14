using Fragile.Core;
using Fragile.Encryption;
using Fragile.Models;
using System.Text;

namespace Fragile.Sample.Beginner.Encryption
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Encryption Sample");
            Console.WriteLine("========================");

            // Create sample directory
            string sampleDir = "Sample";
            Directory.CreateDirectory(sampleDir);

            // Create sample files
            CreateSampleFiles(sampleDir);

            // Set a password for encryption
            string password = "MySecretPassword123!";
            Console.WriteLine($"Using password: {password}");

            // Create encrypted archives using different methods
            await CreateEncryptedArchive(sampleDir, "aes128_encrypted.frgl", password, EncryptionMethod.AES128);
            await CreateEncryptedArchive(sampleDir, "aes256_encrypted.frgl", password, EncryptionMethod.AES256);

            // Try to extract with correct password
            Console.WriteLine("\nExtracting with correct password:");
            string extractDir1 = Path.Combine(sampleDir, "Extracted_Correct");
            await ExtractEncryptedArchive(Path.Combine(sampleDir, "aes256_encrypted.frgl"), extractDir1, password);

            // Try to extract with wrong password (should fail)
            Console.WriteLine("\nAttempting to extract with wrong password (expected to fail):");
            string extractDir2 = Path.Combine(sampleDir, "Extracted_Wrong");
            try
            {
                await ExtractEncryptedArchive(Path.Combine(sampleDir, "aes256_encrypted.frgl"), extractDir2, "WrongPassword");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred as expected: {ex.Message}");
            }

            Console.WriteLine("\nEncryption sample completed!");
            Console.WriteLine("Check the 'Sample' directory for the created files.");

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static void CreateSampleFiles(string directory)
        {
            Console.WriteLine("Creating sample files...");

            // Create a text file with sensitive information
            File.WriteAllText(
                Path.Combine(directory, "sensitive_data.txt"),
                "This file contains sensitive information that should be encrypted.\n" +
                "Credit Card: 1234-5678-9012-3456\n" +
                "SSN: 123-45-6789\n" +
                "Password: SecretPassword123!"
            );

            // Create a second file
            File.WriteAllText(
                Path.Combine(directory, "confidential.txt"),
                "CONFIDENTIAL INFORMATION\n" +
                "This document is classified and should be protected."
            );
        }

        static async Task CreateEncryptedArchive(string outputDir, string archiveName, string password, EncryptionMethod method)
        {
            Console.WriteLine($"\nCreating encrypted archive using {method}...");
            string archivePath = Path.Combine(outputDir, archiveName);

            // Configure encryption options
            FragileOptions options = new()
            {
                EnableEncryption = true,
                Password = password,
                EncryptionMethod = method
            };

            try
            {
                // Create and save the archive with encryption
                using FragileArchive archive = await FragileArchive.CreateAsync(archivePath, options);

                // Add files to the archive
                await archive.AddFileAsync(Path.Combine(outputDir, "sensitive_data.txt"));
                await archive.AddFileAsync(Path.Combine(outputDir, "confidential.txt"));

                // Save the archive
                await archive.SaveAsync();

                Console.WriteLine($"Successfully created encrypted archive: {archiveName}");
                Console.WriteLine($"Archive size: {new FileInfo(archivePath).Length:N0} bytes");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating encrypted archive: {ex.Message}");
            }
        }

        static async Task ExtractEncryptedArchive(string archivePath, string extractDir, string password)
        {
            Console.WriteLine($"Extracting archive to: {extractDir}");

            try
            {
                // Create the extraction directory
                Directory.CreateDirectory(extractDir);

                // Configure options with the password
                FragileOptions options = new()
                {
                    Password = password
                };

                // Extract the archive
                using FragileArchive archive = await FragileArchive.OpenAsync(archivePath, options);
                await archive.ExtractAllAsync(extractDir);

                Console.WriteLine($"Successfully extracted the archive with {archive.Entries.Count} files.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting archive: {ex.Message}");
                throw; // Rethrow for demonstration purposes
            }
        }
    }
}