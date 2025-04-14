namespace Fragile.Formats
{
    /// <summary>
    /// Format compatibility modes for Fragile archives
    /// </summary>
    public enum FormatCompatibility
    {
        /// <summary>
        /// Native Fragile format with all features
        /// </summary>
        Native = 0,
        
        /// <summary>
        /// ZIP compatible mode (limited feature set)
        /// </summary>
        Zip = 1,
        
        /// <summary>
        /// TAR compatible mode (limited feature set)
        /// </summary>
        Tar = 2,
        
        /// <summary>
        /// 7z compatibility mode (limited feature set)
        /// </summary>
        SevenZip = 3
    }
} 