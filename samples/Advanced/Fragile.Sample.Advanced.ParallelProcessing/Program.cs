using Fragile.Compression;
using Fragile.Core;
using Fragile.Models;
using System.Diagnostics;
using System.Text;

namespace Fragile.Sample.Advanced.ParallelProcessing
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Fragile Advanced Parallel Processing Sample");
            Console.WriteLine("===========================================");

            // Create sample directory
            string sampleDir = "Sample";
            Directory.CreateDirectory(sampleDir);

            // Generate test data for benchmarking
            await GenerateTestFiles(sampleDir);

            // Run benchmarks
            await RunComparisonBenchmarks(sampleDir);

            Console.WriteLine("\nParallel processing sample completed!");
            Console.WriteLine("Check the 'Sample' directory for the created files and benchmark results.");

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static async Task GenerateTestFiles(string directory)
        {
            Console.WriteLine("Generating test files for benchmarking...");

            // Create a directory for test files
            string testFilesDir = Path.Combine(directory, "TestData");
            Directory.CreateDirectory(testFilesDir);

            // Generate test files of various types
            await GenerateTextFiles(testFilesDir);
            await GenerateBinaryFiles(testFilesDir);

            // Count and report total files
            string[] allFiles = Directory.GetFiles(testFilesDir, "*", SearchOption.AllDirectories);
            long totalSize = allFiles.Sum(f => new FileInfo(f).Length);

            Console.WriteLine($"Generated {allFiles.Length} test files with total size: {totalSize:N0} bytes");
        }

        static async Task GenerateTextFiles(string directory)
        {
            string textDir = Path.Combine(directory, "Text");
            Directory.CreateDirectory(textDir);

            // Create a large number of small text files
            for (int i = 1; i <= 50; i++)
            {
                string filePath = Path.Combine(textDir, $"text_{i:D3}.txt");
                using StreamWriter writer = new(filePath);

                for (int j = 0; j < 500; j++)
                {
                    await writer.WriteLineAsync($"This is line {j + 1} of file {i}. Adding some content to make the line longer and more compressible.");
                }
            }

            // Create a few larger text files
            for (int i = 1; i <= 5; i++)
            {
                string filePath = Path.Combine(textDir, $"large_text_{i}.txt");
                using StreamWriter writer = new(filePath);

                for (int j = 0; j < 10000; j++)
                {
                    await writer.WriteLineAsync($"This is line {j + 1} of large file {i}. Adding some content to make the line longer and more compressible. " +
                        $"The quick brown fox jumps over the lazy dog. Lorem ipsum dolor sit amet, consectetur adipiscing elit.");
                }
            }
        }

        static async Task GenerateBinaryFiles(string directory)
        {
            string binaryDir = Path.Combine(directory, "Binary");
            Directory.CreateDirectory(binaryDir);

            Random random = new(42); // Use fixed seed for reproducibility

            // Create several binary files of different sizes
            await CreateRandomBinaryFile(Path.Combine(binaryDir, "binary_1mb.dat"), 1 * 1024 * 1024, random);
            await CreateRandomBinaryFile(Path.Combine(binaryDir, "binary_2mb.dat"), 2 * 1024 * 1024, random);
            await CreateRandomBinaryFile(Path.Combine(binaryDir, "binary_5mb.dat"), 5 * 1024 * 1024, random);
            await CreateRandomBinaryFile(Path.Combine(binaryDir, "binary_10mb.dat"), 10 * 1024 * 1024, random);
        }

        static async Task CreateRandomBinaryFile(string filePath, int sizeInBytes, Random random)
        {
            Console.WriteLine($"Creating binary file: {Path.GetFileName(filePath)} ({sizeInBytes / 1024 / 1024} MB)");

            using FileStream stream = new(filePath, FileMode.Create, FileAccess.Write);
            byte[] buffer = new byte[64 * 1024]; // 64 KB buffer

            int remainingBytes = sizeInBytes;
            while (remainingBytes > 0)
            {
                int bytesToWrite = Math.Min(buffer.Length, remainingBytes);
                random.NextBytes(buffer);
                await stream.WriteAsync(buffer, 0, bytesToWrite);
                remainingBytes -= bytesToWrite;
            }
        }

        static async Task RunComparisonBenchmarks(string directory)
        {
            Console.WriteLine("\nRunning benchmarks to compare parallel vs. sequential processing...");

            string testDataDir = Path.Combine(directory, "TestData");
            if (!Directory.Exists(testDataDir))
            {
                throw new DirectoryNotFoundException($"Test data directory not found: {testDataDir}");
            }

            string resultsDir = Path.Combine(directory, "BenchmarkResults");
            Directory.CreateDirectory(resultsDir);

            // Define the benchmark configurations
            List<BenchmarkConfig> benchmarks = new()
            {
                new BenchmarkConfig
                {
                    Name = "Sequential - Normal Compression",
                    UseParallelProcessing = false,
                    CompressionLevel = CompressionLevel.Normal,
                    MaxThreads = 1
                },
                new BenchmarkConfig
                {
                    Name = "Parallel - Normal Compression (2 threads)",
                    UseParallelProcessing = true,
                    CompressionLevel = CompressionLevel.Normal,
                    MaxThreads = 2
                },
                new BenchmarkConfig
                {
                    Name = "Parallel - Normal Compression (4 threads)",
                    UseParallelProcessing = true,
                    CompressionLevel = CompressionLevel.Normal,
                    MaxThreads = 4
                },
                new BenchmarkConfig
                {
                    Name = "Parallel - Normal Compression (Max threads)",
                    UseParallelProcessing = true,
                    CompressionLevel = CompressionLevel.Normal,
                    MaxThreads = Environment.ProcessorCount
                },
                new BenchmarkConfig
                {
                    Name = "Sequential - Ultra Compression",
                    UseParallelProcessing = false,
                    CompressionLevel = CompressionLevel.Ultra,
                    MaxThreads = 1
                },
                new BenchmarkConfig
                {
                    Name = "Parallel - Ultra Compression (Max threads)",
                    UseParallelProcessing = true,
                    CompressionLevel = CompressionLevel.Ultra,
                    MaxThreads = Environment.ProcessorCount
                }
            };

            // Run each benchmark
            List<BenchmarkResult> results = new();
            foreach (BenchmarkConfig config in benchmarks)
            {
                BenchmarkResult result = await RunBenchmark(testDataDir, resultsDir, config);
                results.Add(result);
            }

            // Display and save results
            DisplayBenchmarkResults(results);
            await SaveBenchmarkResults(results, Path.Combine(resultsDir, "benchmark_results.csv"));
        }

        static async Task<BenchmarkResult> RunBenchmark(string sourceDir, string resultsDir, BenchmarkConfig config)
        {
            Console.WriteLine($"\nRunning benchmark: {config.Name}");

            string archivePath = Path.Combine(resultsDir, $"archive_{config.Name.Replace(" ", "_").Replace("-", "_").Replace("(", "").Replace(")", "")}.frgl");

            // Configure options based on the benchmark config
            FragileOptions options = new()
            {
                CompressionAlgorithm = CompressionAlgorithm.Deflate,
                CompressionLevel = config.CompressionLevel,
                UseParallelProcessing = config.UseParallelProcessing,
                MaxThreads = config.MaxThreads
            };

            Console.WriteLine($"Configuration: {(config.UseParallelProcessing ? "Parallel" : "Sequential")}, " +
                $"Compression: {config.CompressionLevel}, Threads: {config.MaxThreads}");

            // Measure compression time
            Stopwatch stopwatch = Stopwatch.StartNew();

            BenchmarkResult result = new()
            {
                ConfigName = config.Name,
                UseParallelProcessing = config.UseParallelProcessing,
                CompressionLevel = config.CompressionLevel,
                MaxThreads = config.MaxThreads
            };

            try
            {
                // Create and populate the archive
                using FragileArchive archive = await FragileArchive.CreateAsync(archivePath, options);

                // Add all files from the test data directory
                Console.WriteLine("Adding files to archive...");
                int fileCount = await archive.AddDirectoryAsync(sourceDir, recursive: true);
                result.FileCount = fileCount;

                // Save the archive
                Console.WriteLine("Saving archive...");
                await archive.SaveAsync();

                // Stop the timer
                stopwatch.Stop();
                result.CompressionTimeMs = stopwatch.ElapsedMilliseconds;

                // Get archive size and original size
                FileInfo archiveFile = new(archivePath);
                result.ArchiveSizeBytes = archiveFile.Length;

                // Calculate original size
                long originalSize = 0;
                foreach (FragileArchiveEntry entry in archive.Entries)
                {
                    originalSize += entry.Size;
                }
                result.OriginalSizeBytes = originalSize;

                // Calculate compression ratio
                result.CompressionRatio = (double)originalSize / archiveFile.Length;

                // Measure extraction time
                string extractDir = Path.Combine(resultsDir, $"Extracted_{Path.GetFileNameWithoutExtension(archivePath)}");
                Directory.CreateDirectory(extractDir);

                Console.WriteLine("Extracting archive to measure extraction time...");
                stopwatch.Restart();
                await archive.ExtractAllAsync(extractDir);
                stopwatch.Stop();

                result.ExtractionTimeMs = stopwatch.ElapsedMilliseconds;
                result.Success = true;

                // Clean up extraction directory to save disk space
                try
                {
                    Directory.Delete(extractDir, true);
                }
                catch
                {
                    // Ignore errors when cleaning up
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during benchmark: {ex.Message}");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        static void DisplayBenchmarkResults(List<BenchmarkResult> results)
        {
            Console.WriteLine("\nBenchmark Results:");
            Console.WriteLine("=================");

            Console.WriteLine("\n{0,-45} | {1,10} | {2,10} | {3,10} | {4,15} | {5,15}",
                "Configuration", "Comp. Time", "Extr. Time", "Ratio", "Original Size", "Archive Size");

            Console.WriteLine(new string('-', 120));

            foreach (BenchmarkResult? result in results.Where(r => r.Success))
            {
                Console.WriteLine("{0,-45} | {1,10:N0} ms | {2,10:N0} ms | {3,10:F2}x | {4,15:N0} | {5,15:N0}",
                    result.ConfigName,
                    result.CompressionTimeMs,
                    result.ExtractionTimeMs,
                    result.CompressionRatio,
                    result.OriginalSizeBytes,
                    result.ArchiveSizeBytes);
            }

            // Display failed benchmarks, if any
            List<BenchmarkResult> failed = results.Where(r => !r.Success).ToList();
            if (failed.Count > 0)
            {
                Console.WriteLine("\nFailed Benchmarks:");
                foreach (BenchmarkResult? result in failed)
                {
                    Console.WriteLine($"- {result.ConfigName}: {result.ErrorMessage}");
                }
            }

            // Calculate speed improvements
            if (results.Count >= 2 && results[0].Success && results[1].Success)
            {
                BenchmarkResult? sequential = results.FirstOrDefault(r => !r.UseParallelProcessing && r.CompressionLevel == CompressionLevel.Normal);
                BenchmarkResult? parallel = results.FirstOrDefault(r => r.UseParallelProcessing && r.CompressionLevel == CompressionLevel.Normal && r.MaxThreads == Environment.ProcessorCount);

                if (sequential != null && parallel != null)
                {
                    double speedup = (double)sequential.CompressionTimeMs / parallel.CompressionTimeMs;
                    Console.WriteLine($"\nParallel processing speedup: {speedup:F2}x faster than sequential processing");
                }
            }
        }

        static async Task SaveBenchmarkResults(List<BenchmarkResult> results, string filePath)
        {
            using StreamWriter writer = new(filePath);

            // Write CSV header
            await writer.WriteLineAsync("Configuration,UseParallelProcessing,CompressionLevel,MaxThreads," +
                "CompressionTimeMs,ExtractionTimeMs,OriginalSizeBytes,ArchiveSizeBytes,CompressionRatio,FileCount,Success,ErrorMessage");

            // Write result rows
            foreach (BenchmarkResult result in results)
            {
                await writer.WriteLineAsync(
                    $"\"{result.ConfigName}\",{result.UseParallelProcessing},{result.CompressionLevel},{result.MaxThreads}," +
                    $"{result.CompressionTimeMs},{result.ExtractionTimeMs},{result.OriginalSizeBytes},{result.ArchiveSizeBytes}," +
                    $"{result.CompressionRatio},{result.FileCount},{result.Success},\"{result.ErrorMessage}\"");
            }

            Console.WriteLine($"\nBenchmark results saved to: {filePath}");
        }
    }

    class BenchmarkConfig
    {
        public string Name { get; set; }
        public bool UseParallelProcessing { get; set; }
        public CompressionLevel CompressionLevel { get; set; }
        public int MaxThreads { get; set; }
    }

    class BenchmarkResult
    {
        public string ConfigName { get; set; }
        public bool UseParallelProcessing { get; set; }
        public CompressionLevel CompressionLevel { get; set; }
        public int MaxThreads { get; set; }
        public long CompressionTimeMs { get; set; }
        public long ExtractionTimeMs { get; set; }
        public long OriginalSizeBytes { get; set; }
        public long ArchiveSizeBytes { get; set; }
        public double CompressionRatio { get; set; }
        public int FileCount { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }
}