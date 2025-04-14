namespace Fragile.Models
{
    /// <summary>
    /// Specifies the Fragile archive opening mode
    /// </summary>
    public enum FragileArchiveMode
    {
        /// <summary>
        /// Read-only mode, archive file must already exist
        /// </summary>
        Read,

        /// <summary>
        /// Creates a new archive, overwrites if it exists
        /// </summary>
        Create,

        /// <summary>
        /// Opens an existing archive or creates a new one if it doesn't exist
        /// </summary>
        Update
    }
}