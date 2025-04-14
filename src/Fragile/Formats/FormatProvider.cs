using Fragile.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Fragile.Formats
{
    /// <summary>
    /// Abstract base class for archive format providers
    /// </summary>
    public abstract class FormatProvider
    {
        /// <summary>
        /// The format compatibility mode provided
        /// </summary>
        public abstract FormatCompatibility Format { get; }
        
        /// <summary>
        /// Creates a format provider for the specified compatibility mode
        /// </summary>
        /// <param name="format">The format compatibility mode</param>
        /// <returns>A suitable format provider</returns>
        public static FormatProvider Create(FormatCompatibility format)
        {
            return format switch
            {
                FormatCompatibility.Native => new NativeFormatProvider(),
                // These would be implemented with additional format support
                // FormatCompatibility.Zip => new ZipFormatProvider(),
                // FormatCompatibility.Tar => new TarFormatProvider(),
                // FormatCompatibility.SevenZip => new SevenZipFormatProvider(),
                _ => throw new NotSupportedException($"Format compatibility mode {format} is not supported")
            };
        }
        
        /// <summary>
        /// Converts the input archive to the format-compatible output archive
        /// </summary>
        /// <param name="inputPath">Path to the input archive</param>
        /// <param name="outputPath">Path to the output archive</param>
        /// <param name="options">Conversion options</param>
        /// <param name="progress">Optional progress reporting</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the conversion operation</returns>
        public abstract Task ConvertAsync(string inputPath, string outputPath, FragileOptions? options = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Imports an external format archive to a Fragile archive
        /// </summary>
        /// <param name="inputPath">Path to the input archive</param>
        /// <param name="outputPath">Path to the output Fragile archive</param>
        /// <param name="options">Import options</param>
        /// <param name="progress">Optional progress reporting</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the import operation</returns>
        public abstract Task ImportAsync(string inputPath, string outputPath, FragileOptions? options = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Exports a Fragile archive to the format-compatible output archive
        /// </summary>
        /// <param name="inputPath">Path to the input Fragile archive</param>
        /// <param name="outputPath">Path to the output archive</param>
        /// <param name="options">Export options</param>
        /// <param name="progress">Optional progress reporting</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the export operation</returns>
        public abstract Task ExportAsync(string inputPath, string outputPath, FragileOptions? options = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Determines if the provider can read the specified archive
        /// </summary>
        /// <param name="archivePath">Path to the archive</param>
        /// <returns>True if the provider can read the archive</returns>
        public abstract bool CanRead(string archivePath);
        
        /// <summary>
        /// Gets the default file extension for the format
        /// </summary>
        /// <returns>Default file extension with leading dot</returns>
        public abstract string GetDefaultExtension();
    }
} 