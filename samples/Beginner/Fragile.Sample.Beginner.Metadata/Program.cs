using Fragile.Core;
using Fragile.Metadata;
using Fragile.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Fragile.Sample.Beginner.Metadata
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Fragile Metadata Sample");
            Console.WriteLine("======================");

            // Create sample directory
            string sampleDir = "Sample";
            Directory.CreateDirectory(sampleDir);

            // Create sample files
            CreateSampleFiles(sampleDir);

            // Create an archive with metadata
            string archivePath = Path.Combine(sampleDir, "metadata_archive.frgl");
            await CreateArchiveWithMetadata(sampleDir, archivePath);

            // Read the metadata from the archive
            await ReadArchiveMetadata(archivePath);

            Console.WriteLine("\nMetadata sample completed!");
            Console.WriteLine("Check the 'Sample' directory for the created files.");
        }

        static void CreateSampleFiles(string directory)
        {
            Console.WriteLine("Creating sample files...");
            
            // Create a few text files
            File.WriteAllText(
                Path.Combine(directory, "document1.txt"), 
                "This is the first document."
            );

            File.WriteAllText(
                Path.Combine(directory, "document2.txt"),
                "This is the second document."
            );

            // Create an image file (fake, just for metadata demonstration)
            File.WriteAllText(
                Path.Combine(directory, "image.jpg"),
                "This is a placeholder for an image file. In real use, this would be a binary image file."
            );
        }

        static async Task CreateArchiveWithMetadata(string inputDir, string archivePath)
        {
            Console.WriteLine("\nCreating archive with metadata...");
            
            // Configure options
            FragileOptions options = new FragileOptions
            {
                IncludeMetadata = true // Make sure metadata is enabled
            };

            // Create a new archive
            using FragileArchive archive = await FragileArchive.CreateAsync(archivePath, options);
            
            // Set archive-level metadata
            Console.WriteLine("Setting archive-level metadata...");
            archive.Metadata.Title = "Sample Document Collection";
            archive.Metadata.Description = "A collection of sample documents for the Fragile metadata example";
            archive.Metadata.Author = "Fragile Library User";
            archive.Metadata.Version = "1.0";
            archive.Metadata.Tags.AddRange(new[] { "sample", "documentation", "metadata" });
            
            // Add custom properties
            archive.Metadata.AddProperty("Category", "Examples");
            archive.Metadata.AddProperty("SecurityLevel", "Public");
            
            // Add files and set file-level metadata
            Console.WriteLine("Adding files with metadata...");
            
            // First document
            string file1Path = Path.Combine(inputDir, "document1.txt");
            await archive.AddFileAsync(file1Path);
            
            EntryMetadata file1Metadata = new EntryMetadata
            {
                CreationTime = DateTime.Now.AddDays(-5),
                LastAccessTime = DateTime.Now.AddDays(-1),
                MimeType = "text/plain"
            };
            file1Metadata.Tags.Add("document");
            file1Metadata.Comment = "This is the first sample document";
            archive.SetEntryMetadata(Path.GetFileName(file1Path), file1Metadata);
            
            // Second document
            string file2Path = Path.Combine(inputDir, "document2.txt");
            await archive.AddFileAsync(file2Path);
            
            EntryMetadata file2Metadata = new EntryMetadata
            {
                CreationTime = DateTime.Now.AddDays(-2),
                LastAccessTime = DateTime.Now,
                MimeType = "text/plain"
            };
            file2Metadata.Tags.Add("document");
            file2Metadata.Tags.Add("important");
            file2Metadata.Comment = "This is the second sample document";
            archive.SetEntryMetadata(Path.GetFileName(file2Path), file2Metadata);
            
            // Image file
            string imagePath = Path.Combine(inputDir, "image.jpg");
            await archive.AddFileAsync(imagePath);
            
            EntryMetadata imageMetadata = new EntryMetadata
            {
                CreationTime = DateTime.Now.AddDays(-1),
                LastAccessTime = DateTime.Now,
                MimeType = "image/jpeg"
            };
            imageMetadata.Tags.Add("image");
            imageMetadata.AddProperty("Width", "1920");
            imageMetadata.AddProperty("Height", "1080");
            imageMetadata.AddProperty("Camera", "Sample Camera");
            imageMetadata.Comment = "Sample image file";
            archive.SetEntryMetadata(Path.GetFileName(imagePath), imageMetadata);
            
            // Save the archive
            await archive.SaveAsync();
            Console.WriteLine($"Archive with metadata saved to: {archivePath}");
        }

        static async Task ReadArchiveMetadata(string archivePath)
        {
            Console.WriteLine("\nReading metadata from archive...");
            
            using FragileArchive archive = await FragileArchive.OpenAsync(archivePath);
            
            // Display archive-level metadata
            Console.WriteLine("\nArchive Metadata:");
            Console.WriteLine($"Title: {archive.Metadata.Title}");
            Console.WriteLine($"Description: {archive.Metadata.Description}");
            Console.WriteLine($"Author: {archive.Metadata.Author}");
            Console.WriteLine($"Version: {archive.Metadata.Version}");
            Console.WriteLine($"Created: {archive.Metadata.CreationTime}");
            Console.WriteLine($"Tags: {string.Join(", ", archive.Metadata.Tags)}");
            
            Console.WriteLine("\nCustom Properties:");
            foreach (var prop in archive.Metadata.CustomProperties)
            {
                Console.WriteLine($"  {prop.Key}: {prop.Value}");
            }
            
            // Display file-level metadata
            Console.WriteLine("\nFile Metadata:");
            
            foreach (var entry in archive.Entries)
            {
                if (!entry.IsDirectory)
                {
                    var extendedEntry = archive.GetExtendedEntry(entry.Path);
                    Console.WriteLine($"\n- {entry.Path}");
                    Console.WriteLine($"  Size: {entry.Size} bytes");
                    Console.WriteLine($"  Last Modified: {entry.LastModified}");
                    
                    if (extendedEntry.Metadata != null)
                    {
                        Console.WriteLine($"  MIME Type: {extendedEntry.Metadata.MimeType}");
                        Console.WriteLine($"  Created: {extendedEntry.Metadata.CreationTime}");
                        Console.WriteLine($"  Last Accessed: {extendedEntry.Metadata.LastAccessTime}");
                        Console.WriteLine($"  Comment: {extendedEntry.Metadata.Comment}");
                        
                        if (extendedEntry.Metadata.Tags.Count > 0)
                        {
                            Console.WriteLine($"  Tags: {string.Join(", ", extendedEntry.Metadata.Tags)}");
                        }
                        
                        if (extendedEntry.Metadata.CustomProperties.Count > 0)
                        {
                            Console.WriteLine("  Custom Properties:");
                            foreach (var prop in extendedEntry.Metadata.CustomProperties)
                            {
                                Console.WriteLine($"    {prop.Key}: {prop.Value}");
                            }
                        }
                    }
                }
            }
        }
    }
} 