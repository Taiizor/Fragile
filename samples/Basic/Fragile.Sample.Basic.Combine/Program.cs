using Fragile.Core;
using Fragile.Utils;
using System.Text;

namespace Fragile.Sample.Basic.Combine
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Combine Sample");
            Console.WriteLine("======================");
            Console.WriteLine("This example shows how to combine split archive files");

            // Create sample directory
            string sampleDir = "Sample";
            Directory.CreateDirectory(sampleDir);

            // Create sample files
            string textFilePath = Path.Combine(sampleDir, "sample.txt");
            File.WriteAllText(textFilePath, "This is a sample text file for Fragile.");

            string largeFilePath = Path.Combine(sampleDir, "large_file.txt");
            await CreateLargeFileAsync(largeFilePath, 1 * 1024 * 1024); // 1MB size

            // Create subfolder and add file to it
            string subfolderPath = Path.Combine(sampleDir, "subfolder");
            Directory.CreateDirectory(subfolderPath);
            File.WriteAllText(Path.Combine(subfolderPath, "readme.txt"), "This is a file in the subfolder.");

            // Create directory for split archives
            string splitDir = "Split";
            Directory.CreateDirectory(splitDir);

            // Create directory for combined archives
            string combinedDir = "Combined";
            Directory.CreateDirectory(combinedDir);

            string archivePath = Path.Combine(splitDir, "split_archive.frgl");

            try
            {
                // Create split archive - with 200KB parts
                Console.WriteLine("\nCreating split archive...");

                long splitSize = 200 * 1024; // 200KB
                FragileArchivePartCollection parts = await FragileUtility.CreateSplitArchiveAsync(
                    sampleDir,
                    archivePath,
                    recursive: true,
                    splitSize: splitSize);

                Console.WriteLine($"Archive split into {parts.Count} parts:");
                foreach (FragileArchivePart part in parts)
                {
                    Console.WriteLine($" - {Path.GetFileName(part.Path)} ({FormatFileSize(part.Size)})");
                }

                // Combining file parts
                Console.WriteLine("\nCombining archive parts...");

                string firstPartPath = Path.Combine(splitDir, Path.GetFileName(parts[0].Path));
                string combinedArchivePath = Path.Combine(combinedDir, "combined_archive.frgl");

                await FragileUtility.CombinePartsAsync(firstPartPath, combinedArchivePath);

                Console.WriteLine($"Parts successfully combined: {combinedArchivePath}");
                Console.WriteLine($"Combined archive size: {FormatFileSize(new FileInfo(combinedArchivePath).Length)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Creates a large test file with the specified size
        /// </summary>
        private static async Task CreateLargeFileAsync(string filePath, int sizeInBytes)
        {
            using (FileStream stream = File.Create(filePath))
            {
                // Write 4KB in each iteration until reaching the desired size
                byte[] buffer = new byte[4096];
                Random rnd = new();

                int bytesWritten = 0;
                while (bytesWritten < sizeInBytes)
                {
                    rnd.NextBytes(buffer);
                    int bytesToWrite = Math.Min(buffer.Length, sizeInBytes - bytesWritten);
                    await stream.WriteAsync(buffer, 0, bytesToWrite);
                    bytesWritten += bytesToWrite;
                }
            }

            Console.WriteLine($"Created test file: {filePath} ({FormatFileSize(sizeInBytes)})");
        }

        /// <summary>
        /// Converts size in bytes to a readable format (KB, MB, GB)
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;

            while (number >= 1024 && counter < suffixes.Length - 1)
            {
                number /= 1024;
                counter++;
            }

            return $"{number:0.##} {suffixes[counter]}";
        }
    }
}