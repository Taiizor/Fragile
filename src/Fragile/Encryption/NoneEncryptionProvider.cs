using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Fragile.Encryption
{
    /// <summary>
    /// Encryption provider that doesn't perform any encryption
    /// </summary>
    internal class NoneEncryptionProvider : EncryptionProvider
    {
        /// <summary>
        /// The encryption method used by this provider
        /// </summary>
        public override EncryptionMethod Method => EncryptionMethod.None;

        /// <summary>
        /// "Encrypts" the input stream to the output stream (actually just copies it)
        /// </summary>
        public override async Task<long> EncryptAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long initialPosition = output.Position;

            // No encryption, just copy the stream
            byte[] buffer = new byte[81920]; // 80 KB buffer

            // If input stream supports seeking, we can report progress
            bool canReportProgress = input.CanSeek;
            long totalBytes = canReportProgress ? input.Length : 0;
            long totalBytesRead = 0;

            int bytesRead;
            while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);

                // Report progress if possible
                if (canReportProgress && progress != null)
                {
                    totalBytesRead += bytesRead;
                    double progressValue = (double)totalBytesRead / totalBytes;
                    progress.Report(progressValue);
                }

                // Check for cancellation
                cancellationToken.ThrowIfCancellationRequested();
            }

            // Return the number of bytes written
            return output.Position - initialPosition;
        }

        /// <summary>
        /// "Decrypts" the input stream to the output stream (actually just copies it)
        /// </summary>
        public override async Task<long> DecryptAsync(Stream input, Stream output, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            // No encryption, so decryption is the same as encryption (copying)
            return await EncryptAsync(input, output, progress, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the storage overhead for the encryption (always 0 for no encryption)
        /// </summary>
        public override int GetOverheadSize()
        {
            return 0; // No overhead for no encryption
        }
    }
}