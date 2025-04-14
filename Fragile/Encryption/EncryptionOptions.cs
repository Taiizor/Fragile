using System;

namespace Fragile.Encryption
{
    /// <summary>
    /// Defines the encryption algorithms supported by the Fragile library.
    /// </summary>
    public enum EncryptionAlgorithm
    {
        /// <summary>
        /// AES encryption with 128-bit key.
        /// </summary>
        AES128,

        /// <summary>
        /// AES encryption with 256-bit key.
        /// </summary>
        AES256,

        /// <summary>
        /// ChaCha20 encryption (not currently implemented).
        /// </summary>
        ChaCha20,

        /// <summary>
        /// Twofish encryption (not currently implemented).
        /// </summary>
        Twofish
    }

    /// <summary>
    /// Configuration options for encryption operations.
    /// </summary>
    public class EncryptionOptions
    {
        /// <summary>
        /// Gets or sets the encryption algorithm to use.
        /// </summary>
        public EncryptionAlgorithm Algorithm { get; set; } = EncryptionAlgorithm.AES256;

        /// <summary>
        /// Gets or sets the password for encryption.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the value is null or empty.</exception>
        public string Password
        {
            get => _password;
            set
            {
                if (string.IsNullOrEmpty(value))
                    throw new ArgumentNullException(nameof(Password), "Password cannot be null or empty.");
                _password = value;
            }
        }

        private string _password = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether to use per-file encryption settings.
        /// </summary>
        public bool UsePerFileEncryption { get; set; } = false;

        /// <summary>
        /// Gets or sets the salt for key derivation.
        /// </summary>
        public byte[]? Salt { get; set; }

        /// <summary>
        /// Gets or sets the number of iterations for PBKDF2 key derivation.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than 1000.</exception>
        public int Pbkdf2Iterations
        {
            get => _pbkdf2Iterations;
            set
            {
                if (value < 1000)
                    throw new ArgumentOutOfRangeException(nameof(Pbkdf2Iterations), "PBKDF2 iterations must be at least 1000.");
                _pbkdf2Iterations = value;
            }
        }

        private int _pbkdf2Iterations = 100000;

        /// <summary>
        /// Initializes a new instance of the <see cref="EncryptionOptions"/> class with default values.
        /// </summary>
        public EncryptionOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EncryptionOptions"/> class with the specified algorithm and password.
        /// </summary>
        /// <param name="algorithm">The encryption algorithm to use.</param>
        /// <param name="password">The password for encryption.</param>
        public EncryptionOptions(EncryptionAlgorithm algorithm, string password)
        {
            Algorithm = algorithm;
            Password = password;
        }
    }
} 