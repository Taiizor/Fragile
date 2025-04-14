using Fragile.Core;
using Fragile.Models;
using Fragile.Utils;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Fragile.Sample.Beginner.BasicArchiving
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Fragile Basic Archiving Sample");
            Console.WriteLine("==============================");

            // Create sample directory and files
            string sampleDir = "Sample";
            Directory.CreateDirectory(sampleDir);

            // Create a few sample text files
            CreateSampleFiles(sampleDir);

            // Create a basic archive
            string archivePath = Path.Combine(sampleDir, "MyFirstArchive.frgl");
            Console.WriteLine($"Creating archive: {archivePath}");

            // Using the utility methods (easiest approach)
            int fileCount = FragileUtility.CreateArchive(
                sampleDir, 
                archivePath, 
                recursive: true);

            Console.WriteLine($"Added {fileCount} files to the archive.");

            // Extract the archive
            string extractDir = Path.Combine(sampleDir, "Extracted");
            Console.WriteLine($"Extracting archive to: {extractDir}");
            
            FragileUtility.ExtractArchive(archivePath, extractDir);
            Console.WriteLine("Archive extracted successfully!");

            // List files in the archive
            Console.WriteLine("\nListing archive contents:");
            await ListArchiveContents(archivePath);

            Console.WriteLine("\nBasic archiving operations completed successfully!");
            Console.WriteLine("Check the 'Sample' directory for the created files.");
        }

        static void CreateSampleFiles(string directory)
        {
            // Create a few text files with sample content
            File.WriteAllText(Path.Combine(directory, "hello.txt"), "Hello, Fragile!");
            File.WriteAllText(Path.Combine(directory, "info.txt"), "This is a sample file for testing Fragile archiving.");
            
            // Create a subdirectory with a file
            string subDir = Path.Combine(directory, "SubFolder");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, "nested.txt"), "This file is inside a subfolder.");
        }

        static async Task ListArchiveContents(string archivePath)
        {
            // Open the archive and list its contents
            using FragileArchive archive = await FragileArchive.OpenAsync(archivePath);
            
            foreach (var entry in archive.Entries)
            {
                string entryType = entry.IsDirectory ? "Directory" : "File";
                Console.WriteLine($"- {entry.Path} ({entryType}, Size: {entry.Size} bytes)");
            }
        }
    }
} 