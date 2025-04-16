using Fragile.Core;
using System.Text;

namespace Fragile.Sample.Basic.Create
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Create Sample");
            Console.WriteLine("======================");

            // Create sample directory
            string sampleDir = "Sample";
            Directory.CreateDirectory(sampleDir);

            // Create a sample file
            string sampleFilePath = Path.Combine(sampleDir, "sample.txt");
            await File.WriteAllTextAsync(sampleFilePath, "This is a sample text file for archiving with Fragile library.");
            Console.WriteLine($"Created sample file: {sampleFilePath}");

            // Create archive
            string archivePath = Path.Combine(sampleDir, "sample_archive.frgl");
            Console.WriteLine($"\nCreating archive: {archivePath}");

            // Create new Fragile archive
            using FragileArchive archive = await FragileArchive.CreateAsync(archivePath);

            // Add file to the archive
            await archive.AddFileAsync(sampleFilePath);
            Console.WriteLine("Added file to archive");

            // Save the archive
            await archive.SaveAsync();
            archive.Dispose(); // Dispose of the archive to release resources (optional)

            Console.WriteLine("Archive saved successfully");

            // Display archive information
            FileInfo archiveInfo = new(archivePath);
            Console.WriteLine($"\nArchive size: {archiveInfo.Length} bytes");
            Console.WriteLine($"Files in archive: {archive.Entries.Count}");

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}