namespace Fragile.Implementations.Providers.Encryption;

/// <summary>
/// Provides AES encryption with a 128-bit key.
/// Derives the key from a password using PBKDF2.
/// </summary>
internal class Aes128EncryptionProvider : AesEncryptionProviderBase
{
    protected override int KeySizeBits => 128;

    public Aes128EncryptionProvider(int bufferSize = 81920) : base(bufferSize)
    {
    }

    // Inherits Encrypt/Decrypt/EncryptAsync/DecryptAsync logic from base class
}