using Fragile.Models;
using System.Text;

namespace Fragile.Helpers
{
    /// <summary>
    /// Helper class for checking the archive signature of a given file path
    /// </summary>
    internal class FragileSignature
    {
        /// <summary>
        /// Checks the archive signature of a given file path
        /// </summary>
        /// <param name="filePath">File path to check</param>
        /// <param name="options">Archive options</param>
        /// <returns>True if valid archive signature</returns>
        public static bool CheckArchiveSignature(string filePath, FragileOptions options = null)
        {
            if (!FragilePath.IsFile(filePath))
            {
                return false;
            }

            options ??= new FragileOptions();

            try
            {
                using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                if (stream.Length < options.Signature.Length)
                {
                    return false;
                }

                byte[] signature = new byte[options.Signature.Length];
                stream.Read(signature, 0, options.Signature.Length);

                return Encoding.ASCII.GetString(signature) == options.Signature;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks the archive signature of a given file path asynchronously
        /// </summary>
        /// <param name="filePath">File path to check</param>
        /// <param name="options">Archive options</param>
        /// <returns>Task that returns true if valid archive signature</returns>
        public static async Task<bool> CheckArchiveSignatureAsync(string filePath, FragileOptions options = null)
        {
            if (!FragilePath.IsFile(filePath))
            {
                return false;
            }

            options ??= new FragileOptions();

            try
            {
                // Check file header
                using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                if (stream.Length < options.Signature.Length)
                {
                    return false;
                }

                byte[] signature = new byte[options.Signature.Length];

#if NET48_OR_GREATER || NETSTANDARD2_0
                await stream.ReadAsync(signature, 0, options.Signature.Length);
#else
                await stream.ReadAsync(signature.AsMemory(0, options.Signature.Length));
#endif

                return Encoding.ASCII.GetString(signature) == options.Signature;
            }
            catch
            {
                return false;
            }
        }
    }
}