using Fragile.Core;
using Fragile.Models;
using System.Text;

namespace Fragile.Sample.Basic.Add
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Add Sample");
            Console.WriteLine("======================");
            Console.WriteLine("This sample demonstrates how to add files to an archive");

            // Create sample directory
            string sampleDir = "Sample";
            Directory.CreateDirectory(sampleDir);

            // Create sample files
            string textFilePath = Path.Combine(sampleDir, "example.txt");
            File.WriteAllText(textFilePath, "This is a sample text file for Fragile.");
            
            string imageFilePath = Path.Combine(sampleDir, "subfolder");
            Directory.CreateDirectory(imageFilePath);
            File.WriteAllText(Path.Combine(imageFilePath, "readme.txt"), "This is a file in subfolder.");
            
            string archivePath = "example.frgl";

            try
            {
                // Create a new archive and add files
                Console.WriteLine("\nCreating a new archive and adding files...");
                using (var archive = new FragileArchive(archivePath, FragileArchiveMode.Create))
                {
                    // Add a file to the archive
                    Console.WriteLine($"Adding file: {textFilePath}");
                    var entry = archive.AddFile(textFilePath);
                    Console.WriteLine($"Added entry: {entry.Path}, Size: {entry.Size} bytes");

                    // Add the same file with a different name in the archive
                    Console.WriteLine($"Adding file with custom entry name");
                    var customEntry = archive.AddFile(textFilePath, "documents/readme.txt");
                    Console.WriteLine($"Added entry: {customEntry.Path}, Size: {customEntry.Size} bytes");
                    
                    // Add a directory
                    Console.WriteLine($"Adding directory: {imageFilePath}");
                    archive.AddDirectory(imageFilePath, recursive: true);
                    
                    // Save the archive with files
                    Console.WriteLine("\nSaving the archive...");
                    await archive.SaveAsync();
                }
                Console.WriteLine($"Archive created and saved: {archivePath}");

                // Show the archive contents
                Console.WriteLine("\nArchive contents:");
                using (var archive = new FragileArchive(archivePath, FragileArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        Console.WriteLine($" - {entry.Path} ({entry.Size} bytes)");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}