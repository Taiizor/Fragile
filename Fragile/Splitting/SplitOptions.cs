using System;

namespace Fragile.Splitting
{
    /// <summary>
    /// Configuration options for archive splitting operations.
    /// </summary>
    public class SplitOptions
    {
        /// <summary>
        /// Gets or sets the maximum size of each archive part in bytes.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than 1MB.</exception>
        public long MaxPartSize
        {
            get => _maxPartSize;
            set
            {
                if (value < 1024 * 1024) // 1MB minimum
                    throw new ArgumentOutOfRangeException(nameof(MaxPartSize), "Maximum part size must be at least 1MB.");
                _maxPartSize = value;
            }
        }

        private long _maxPartSize = 1024 * 1024 * 100; // 100MB default

        /// <summary>
        /// Gets or sets a value indicating whether to use parallel processing for splitting operations.
        /// </summary>
        public bool UseParallelProcessing { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to process parts concurrently during extraction.
        /// </summary>
        public bool UseConcurrentProcessing { get; set; } = true;

        /// <summary>
        /// Gets or sets the naming pattern for split archive parts.
        /// </summary>
        public string PartNamingPattern { get; set; } = "{0}.part{1:000}";

        /// <summary>
        /// Initializes a new instance of the <see cref="SplitOptions"/> class with default values.
        /// </summary>
        public SplitOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SplitOptions"/> class with the specified maximum part size.
        /// </summary>
        /// <param name="maxPartSize">The maximum size of each archive part in bytes.</param>
        public SplitOptions(long maxPartSize)
        {
            MaxPartSize = maxPartSize;
        }
    }
} 