namespace Fragile.Encryption
{
    /// <summary>
    /// Encryption methods supported by Fragile
    /// </summary>
    public enum EncryptionMethod
    {
        /// <summary>
        /// No encryption
        /// </summary>
        None = 0,

        /// <summary>
        /// AES encryption with 128-bit key
        /// </summary>
        AES128 = 1,

        /// <summary>
        /// AES encryption with 256-bit key
        /// </summary>
        AES256 = 2,

        /// <summary>
        /// ChaCha20 encryption algorithm
        /// </summary>
        ChaCha20 = 3,

        /// <summary>
        /// Twofish encryption
        /// </summary>
        Twofish = 4
    }
}