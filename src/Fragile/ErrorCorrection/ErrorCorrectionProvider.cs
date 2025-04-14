using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Fragile.ErrorCorrection
{
    /// <summary>
    /// Hata düzeltme sağlayıcı algoritması için soyut temel sınıf
    /// </summary>
    public abstract class ErrorCorrectionProvider
    {
        /// <summary>
        /// Hata düzeltme seviyesi (yüzde olarak)
        /// </summary>
        public int CorrectionLevel { get; }
        
        /// <summary>
        /// Yeni bir hata düzeltme sağlayıcı oluşturur
        /// </summary>
        /// <param name="correctionLevel">Hata düzeltme seviyesi (yüzde olarak)</param>
        protected ErrorCorrectionProvider(int correctionLevel)
        {
            if (correctionLevel < 0 || correctionLevel > 50)
                throw new ArgumentOutOfRangeException(nameof(correctionLevel), "Hata düzeltme seviyesi 0-50 arasında olmalıdır");
            
            CorrectionLevel = correctionLevel;
        }
        
        /// <summary>
        /// Belirtilen seviyede hata düzeltme sağlayıcı oluşturur
        /// </summary>
        /// <param name="correctionLevel">Hata düzeltme seviyesi (0-50 arası)</param>
        /// <returns>Hata düzeltme sağlayıcısı</returns>
        public static ErrorCorrectionProvider Create(int correctionLevel)
        {
            if (correctionLevel <= 0)
                return new NoneErrorCorrectionProvider();
            
            return new ReedSolomonErrorCorrectionProvider(correctionLevel);
        }
        
        /// <summary>
        /// Veriye hata düzeltme kodları ekler
        /// </summary>
        /// <param name="input">Hata düzeltme uygulanacak veri akışı</param>
        /// <param name="output">Hata düzeltme kodları eklenmiş veri akışı</param>
        /// <param name="progress">İlerleme bildirimi</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Yazılan toplam bayt sayısı</returns>
        public abstract Task<long> AddErrorCorrectionAsync(Stream input, Stream output, 
            IProgress<double>? progress = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Veriyi düzeltir ve hata düzeltme kodlarını çıkarır
        /// </summary>
        /// <param name="input">Hata düzeltme kodlu veri akışı</param>
        /// <param name="output">Düzeltilmiş veri akışı</param>
        /// <param name="reportRepairs">Onarım bildirimi geri çağırma işlevi</param>
        /// <param name="progress">İlerleme bildirimi</param>
        /// <param name="cancellationToken">İptal jetonu</param>
        /// <returns>Yazılan toplam bayt sayısı ve düzeltilen bayt sayısını içeren (yazılan, düzeltilen) değer çifti</returns>
        public abstract Task<(long bytesWritten, int bytesRepaired)> CorrectErrorsAsync(Stream input, Stream output, 
            Action<long, int>? reportRepairs = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Hata düzeltme için gerekli ek veri boyutunu hesaplar
        /// </summary>
        /// <param name="dataSize">Orijinal veri boyutu</param>
        /// <returns>Hata düzeltme verileri için gerekli ek boyut</returns>
        public abstract long CalculateOverhead(long dataSize);
    }
    
    /// <summary>
    /// Hata düzeltme uygulamayan boş sağlayıcı
    /// </summary>
    internal class NoneErrorCorrectionProvider : ErrorCorrectionProvider
    {
        /// <summary>
        /// Yeni bir boş hata düzeltme sağlayıcısı oluşturur
        /// </summary>
        public NoneErrorCorrectionProvider() : base(0) { }
        
        /// <summary>
        /// Veriyi değiştirmeden kopyalar (hata düzeltme yok)
        /// </summary>
        public override async Task<long> AddErrorCorrectionAsync(Stream input, Stream output, 
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long initialPosition = output.Position;
            
            // Hata düzeltme yapmadan doğrudan kopyala
            await CopyStreamAsync(input, output, progress, cancellationToken);
            
            return output.Position - initialPosition;
        }
        
        /// <summary>
        /// Veriyi değiştirmeden kopyalar (hata düzeltme yok)
        /// </summary>
        public override async Task<(long bytesWritten, int bytesRepaired)> CorrectErrorsAsync(Stream input, Stream output, 
            Action<long, int>? reportRepairs = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long initialPosition = output.Position;
            
            // Hata düzeltme yapmadan doğrudan kopyala
            await CopyStreamAsync(input, output, progress, cancellationToken);
            
            return (output.Position - initialPosition, 0);
        }
        
        /// <summary>
        /// Hata düzeltme ek boyutunu döndürür (hiç ek yok)
        /// </summary>
        public override long CalculateOverhead(long dataSize)
        {
            return 0; // Hata düzeltme yok, ek veri de yok
        }
        
        /// <summary>
        /// Akış kopyalama yardımcı metodu
        /// </summary>
        private static async Task CopyStreamAsync(Stream input, Stream output, 
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            byte[] buffer = new byte[81920]; // 80 KB arabellek
            
            // Giriş akışı konumlandırılabilirse ilerleme bildirebiliriz
            bool canReportProgress = input.CanSeek;
            long totalBytes = canReportProgress ? input.Length : 0;
            long totalBytesRead = 0;
            
            int bytesRead;
            while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                
                // Mümkünse ilerleme bildir
                if (canReportProgress && progress != null)
                {
                    totalBytesRead += bytesRead;
                    double progressValue = (double)totalBytesRead / totalBytes;
                    progress.Report(progressValue);
                }
                
                // İptal kontrolü
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }
    
    /// <summary>
    /// Reed-Solomon algoritması kullanan hata düzeltme sağlayıcı
    /// </summary>
    internal class ReedSolomonErrorCorrectionProvider : ErrorCorrectionProvider
    {
        // Maximum 80 KB blok boyutu
        private const int MaxBlockSize = 80 * 1024;
        
        // Reed-Solomon algoritması maksimum düzeltebileceği hata yüzdesi
        private const double MaxCorrectableErrorPercentage = 0.5;
        
        /// <summary>
        /// Yeni bir Reed-Solomon hata düzeltme sağlayıcısı oluşturur
        /// </summary>
        /// <param name="correctionLevel">Hata düzeltme seviyesi (1-50 arası)</param>
        public ReedSolomonErrorCorrectionProvider(int correctionLevel) : base(correctionLevel) { }
        
        /// <summary>
        /// Veriye Reed-Solomon hata düzeltme kodlarını ekler
        /// </summary>
        public override async Task<long> AddErrorCorrectionAsync(Stream input, Stream output, 
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long initialPosition = output.Position;
            
            // Giriş akışı konumlandırılabilirse, toplam boyutu öğrenebiliriz
            long totalBytes = input.CanSeek ? input.Length : 0;
            long processedBytes = 0;
            
            // Blok boyutunu, akış boyutuna göre ayarla
            int blockSize = CalculateOptimalBlockSize(totalBytes);
            
            // Reed-Solomon veri ve hata düzeltme boyutlarını hesapla
            int dataSize = blockSize;
            int ecSize = CalculateErrorCorrectionSize(dataSize);
            
            // Hata düzeltme bilgisini başlığa yaz
            await WriteHeaderAsync(output, blockSize, ecSize, cancellationToken);
            
            // Reed-Solomon kodlayıcı oluştur
            var rs = new ReedSolomonAlgorithm(dataSize, ecSize);
            
            // Girişi bloklara ayır ve her bloğu kodla
            byte[] buffer = new byte[blockSize];
            
            while (true)
            {
                int bytesRead = await ReadExactlyAsync(input, buffer, 0, blockSize, cancellationToken);
                if (bytesRead == 0)
                    break;
                
                // Son blok tam değilse, kalan kısmı sıfırla
                if (bytesRead < blockSize)
                {
                    Array.Clear(buffer, bytesRead, blockSize - bytesRead);
                }
                
                // Reed-Solomon ile kodla
                byte[] encoded = rs.Encode(buffer);
                
                // Kodlanmış veriyi yaz
                await output.WriteAsync(encoded, 0, encoded.Length, cancellationToken);
                
                // İlerleme bildir
                processedBytes += bytesRead;
                if (totalBytes > 0 && progress != null)
                {
                    double progressValue = (double)processedBytes / totalBytes;
                    progress.Report(progressValue);
                }
                
                // İptal kontrolü
                cancellationToken.ThrowIfCancellationRequested();
                
                // Son blok tam değilse döngüden çık
                if (bytesRead < blockSize)
                    break;
            }
            
            // Son bir ilerleme güncellemesi
            progress?.Report(1.0);
            
            return output.Position - initialPosition;
        }
        
        /// <summary>
        /// Reed-Solomon hata düzeltme kodlarını kullanarak veriyi düzeltir
        /// </summary>
        public override async Task<(long bytesWritten, int bytesRepaired)> CorrectErrorsAsync(Stream input, Stream output, 
            Action<long, int>? reportRepairs = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            long initialPosition = output.Position;
            int totalRepaired = 0;
            
            // Giriş akışı konumlandırılabilirse, toplam boyutu öğrenebiliriz
            long totalBytes = input.CanSeek ? input.Length : 0;
            long processedBytes = 0;
            
            try
            {
                // Başlığı oku
                var (blockSize, ecSize) = await ReadHeaderAsync(input, cancellationToken);
                
                // Reed-Solomon kodlayıcı oluştur
                var rs = new ReedSolomonAlgorithm(blockSize, ecSize);
                
                // Kodlanmış blok boyutu
                int encodedBlockSize = blockSize + ecSize;
                
                // Giriş verisini bloklara ayır ve her bloğu çöz
                byte[] encodedBuffer = new byte[encodedBlockSize];
                
                while (true)
                {
                    int bytesRead = await ReadExactlyAsync(input, encodedBuffer, 0, encodedBlockSize, cancellationToken);
                    if (bytesRead == 0)
                        break;
                    
                    // Son blok tam değilse, işlemi tamamla
                    if (bytesRead < encodedBlockSize)
                    {
                        // Kalan veriyi doğrudan kopyala
                        await output.WriteAsync(encodedBuffer, 0, Math.Min(bytesRead, blockSize), cancellationToken);
                        break;
                    }
                    
                    try
                    {
                        // Reed-Solomon ile çöz ve hataları düzelt
                        byte[] decoded = rs.Decode(encodedBuffer);
                        
                        // Düzeltme yapıldı mı kontrol et
                        int repairedCount = CountRepairs(encodedBuffer, decoded, blockSize, ecSize);
                        
                        // Çözülmüş veriyi yaz
                        await output.WriteAsync(decoded, 0, blockSize, cancellationToken);
                        
                        // Onarım raporla
                        if (repairedCount > 0)
                        {
                            totalRepaired += repairedCount;
                            reportRepairs?.Invoke(processedBytes, repairedCount);
                        }
                    }
                    catch (Exception)
                    {
                        // Hata düzeltme başarısız olursa, mümkün olduğunca veriyi kurtar
                        await output.WriteAsync(encodedBuffer, 0, Math.Min(bytesRead, blockSize), cancellationToken);
                    }
                    
                    // İlerleme bildir
                    processedBytes += encodedBlockSize;
                    if (totalBytes > 0 && progress != null)
                    {
                        double progressValue = (double)processedBytes / totalBytes;
                        progress.Report(progressValue);
                    }
                    
                    // İptal kontrolü
                    cancellationToken.ThrowIfCancellationRequested();
                }
                
                // Son bir ilerleme güncellemesi
                progress?.Report(1.0);
            }
            catch (Exception)
            {
                // Hata düzeltme tamamen başarısız olursa, kalan veriyi olduğu gibi kopyala
                input.CopyTo(output);
            }
            
            return (output.Position - initialPosition, totalRepaired);
        }
        
        /// <summary>
        /// Hata düzeltme için gerekli ek veri boyutunu hesaplar
        /// </summary>
        public override long CalculateOverhead(long dataSize)
        {
            if (dataSize <= 0)
                return 0;
            
            // Başlık boyutu
            int headerSize = 8;
            
            // Kullanılacak blok boyutunu belirle
            int blockSize = CalculateOptimalBlockSize(dataSize);
            
            // Blok başına hata düzeltme verisi boyutu
            int ecSize = CalculateErrorCorrectionSize(blockSize);
            
            // Toplam blok sayısı (yukarı yuvarla)
            long numBlocks = (dataSize + blockSize - 1) / blockSize;
            
            // Toplam ek veri boyutu
            return headerSize + (numBlocks * ecSize);
        }
        
        /// <summary>
        /// Optimum blok boyutunu hesaplar
        /// </summary>
        private static int CalculateOptimalBlockSize(long dataSize)
        {
            // Küçük dosyalar için daha küçük bloklar kullan
            if (dataSize < 1024)
                return 64;
            if (dataSize < 10 * 1024)
                return 256;
            if (dataSize < 100 * 1024)
                return 1024;
            if (dataSize < 1024 * 1024)
                return 4 * 1024;
            
            // Büyük dosyalar için maksimum blok boyutu kullan
            return MaxBlockSize;
        }
        
        /// <summary>
        /// Blok boyutuna göre hata düzeltme verisi boyutunu hesaplar
        /// </summary>
        private int CalculateErrorCorrectionSize(int blockSize)
        {
            // Hata düzeltme seviyesine göre ek veri boyutu
            int ecSize = (int)(blockSize * CorrectionLevel / 100.0);
            
            // En az 4 bayt, en fazla veri boyutunun yarısı kadar
            ecSize = Math.Max(4, Math.Min(ecSize, blockSize / 2));
            
            return ecSize;
        }
        
        /// <summary>
        /// Düzeltilen bayt sayısını hesaplar
        /// </summary>
        private static int CountRepairs(byte[] encoded, byte[] decoded, int dataSize, int ecSize)
        {
            int repairedCount = 0;
            
            // Orijinal veri kısmını karşılaştır
            for (int i = 0; i < Math.Min(dataSize, decoded.Length); i++)
            {
                if (encoded[i + ecSize] != decoded[i])
                {
                    repairedCount++;
                }
            }
            
            return repairedCount;
        }
        
        /// <summary>
        /// Hata düzeltme başlık bilgisini yazar
        /// </summary>
        private static async Task WriteHeaderAsync(Stream output, int blockSize, int ecSize, CancellationToken cancellationToken)
        {
            byte[] header = new byte[8];
            
            // Sihirli bayt (RS)
            header[0] = (byte)'R';
            header[1] = (byte)'S';
            
            // Blok boyutu (4 bayt, little-endian)
            header[2] = (byte)(blockSize & 0xFF);
            header[3] = (byte)((blockSize >> 8) & 0xFF);
            header[4] = (byte)((blockSize >> 16) & 0xFF);
            header[5] = (byte)((blockSize >> 24) & 0xFF);
            
            // Hata düzeltme boyutu (2 bayt, little-endian)
            header[6] = (byte)(ecSize & 0xFF);
            header[7] = (byte)((ecSize >> 8) & 0xFF);
            
            await output.WriteAsync(header, 0, header.Length, cancellationToken);
        }
        
        /// <summary>
        /// Hata düzeltme başlık bilgisini okur
        /// </summary>
        private static async Task<(int blockSize, int ecSize)> ReadHeaderAsync(Stream input, CancellationToken cancellationToken)
        {
            byte[] header = new byte[8];
            
            if (await input.ReadAsync(header, 0, header.Length, cancellationToken) != header.Length)
            {
                throw new EndOfStreamException("Beklenmeyen dosya sonu - başlık okunamadı");
            }
            
            // Sihirli baytları kontrol et
            if (header[0] != 'R' || header[1] != 'S')
            {
                throw new InvalidDataException("Geçersiz hata düzeltme başlığı");
            }
            
            // Blok boyutunu oku
            int blockSize = header[2] | (header[3] << 8) | (header[4] << 16) | (header[5] << 24);
            
            // Hata düzeltme boyutunu oku
            int ecSize = header[6] | (header[7] << 8);
            
            return (blockSize, ecSize);
        }
        
        /// <summary>
        /// Akıştan tam olarak belirtilen sayıda bayt okur
        /// </summary>
        private static async Task<int> ReadExactlyAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int totalBytesRead = 0;
            
            while (totalBytesRead < count)
            {
                int bytesRead = await stream.ReadAsync(buffer, offset + totalBytesRead, count - totalBytesRead, cancellationToken);
                
                if (bytesRead == 0)
                {
                    // Akış sonuna ulaşıldı
                    break;
                }
                
                totalBytesRead += bytesRead;
            }
            
            return totalBytesRead;
        }
    }
} 