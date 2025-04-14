namespace Fragile.Implementations.Providers.Encryption;

/// <summary>
/// Provides AES encryption with a 256-bit key.
/// Derives the key from a password using PBKDF2.
/// </summary>
internal class Aes256EncryptionProvider : AesEncryptionProviderBase
{
    protected override int KeySizeBits => 256;

    public Aes256EncryptionProvider(int bufferSize = 81920) : base(bufferSize)
    {
    }

    // Inherits Encrypt/Decrypt/EncryptAsync/DecryptAsync logic from base class
}