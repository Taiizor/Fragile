using System;
using System.Linq;

namespace Fragile.ErrorCorrection
{
    /// <summary>
    /// Reed-Solomon hata düzeltme algoritması uygulaması
    /// </summary>
    internal class ReedSolomonAlgorithm
    {
        private readonly int _dataSize;
        private readonly int _errorCorrectionSize;
        private readonly GaloisField _field;

        // Galois Alanı büyüklüğü (2^8)
        private const int FieldSize = 256;

        /// <summary>
        /// Reed-Solomon hata düzeltme algoritması oluşturur
        /// </summary>
        /// <param name="dataSize">Veri boyutu</param>
        /// <param name="errorCorrectionSize">Hata düzeltme verisi boyutu</param>
        public ReedSolomonAlgorithm(int dataSize, int errorCorrectionSize)
        {
            if (dataSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(dataSize), "Veri boyutu pozitif olmalıdır");
            
            if (errorCorrectionSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(errorCorrectionSize), "Hata düzeltme boyutu pozitif olmalıdır");
            
            if (dataSize + errorCorrectionSize > FieldSize - 1)
                throw new ArgumentException("Toplam boyut (veri + hata düzeltme) Galois alanı büyüklüğünden küçük olmalıdır");
            
            _dataSize = dataSize;
            _errorCorrectionSize = errorCorrectionSize;
            _field = new GaloisField(FieldSize);
        }

        /// <summary>
        /// Verilen veriye hata düzeltme kodlarını ekler
        /// </summary>
        /// <param name="data">Korunacak veri</param>
        /// <returns>Hata düzeltme kodları eklenmiş veri</returns>
        public byte[] Encode(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            
            if (data.Length != _dataSize)
                throw new ArgumentException($"Veri boyutu ({data.Length}) beklenen boyutla ({_dataSize}) eşleşmiyor");
            
            // Üreteç polinomu oluştur
            int[] generator = GenerateGenerator(_errorCorrectionSize);
            
            // Çıktı dizisini oluştur
            byte[] result = new byte[_dataSize + _errorCorrectionSize];
            
            // Orijinal veriyi kopyala
            Array.Copy(data, 0, result, 0, _dataSize);
            
            // Sistematik kodlama: Reed-Solomon paritelerini hesapla
            for (int i = 0; i < _dataSize; i++)
            {
                int feedback = _field.Add(result[i], 0);
                if (feedback != 0)
                {
                    for (int j = 1; j < generator.Length; j++)
                    {
                        result[i + j] = (byte)_field.Add(result[i + j], _field.Multiply(generator[j], feedback));
                    }
                }
            }
            
            // Orijinal veriyi tekrar kopyala ve parite verilerini doğru pozisyona taşı
            for (int i = _dataSize - 1; i >= 0; i--)
            {
                result[i + _errorCorrectionSize] = data[i];
            }
            
            for (int i = 0; i < _errorCorrectionSize; i++)
            {
                result[i] = result[_dataSize + i];
            }
            
            return result;
        }

        /// <summary>
        /// Hata düzeltme kodlu veriyi çözümler ve hataları düzeltir
        /// </summary>
        /// <param name="data">Hata düzeltme kodlu veri</param>
        /// <returns>Hataları düzeltilmiş veri</returns>
        public byte[] Decode(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            
            if (data.Length != _dataSize + _errorCorrectionSize)
                throw new ArgumentException($"Veri boyutu ({data.Length}) beklenen toplam boyutla ({_dataSize + _errorCorrectionSize}) eşleşmiyor");
            
            // Veriyi kopyala
            byte[] receivedData = new byte[data.Length];
            Array.Copy(data, 0, receivedData, 0, data.Length);
            
            // Sendromu hesapla
            int[] syndromes = CalculateSyndromes(receivedData);
            
            // Tüm sendrom değerleri sıfır ise hata yok demektir
            if (syndromes.All(s => s == 0))
            {
                byte[] result = new byte[_dataSize];
                Array.Copy(receivedData, _errorCorrectionSize, result, 0, _dataSize);
                return result;
            }
            
            // Hataların konumlarını bul
            int[] errorLocations = FindErrorLocations(syndromes);
            
            // Hata değerlerini bul
            int[] errorValues = FindErrorValues(syndromes, errorLocations);
            
            // Hataları düzelt
            for (int i = 0; i < errorLocations.Length; i++)
            {
                int position = _field.Log(_field.Inverse(errorLocations[i]));
                if (position < receivedData.Length)
                {
                    receivedData[position] = (byte)_field.Add(receivedData[position], errorValues[i]);
                }
            }
            
            // Sadece veri kısmını dön
            byte[] result2 = new byte[_dataSize];
            Array.Copy(receivedData, _errorCorrectionSize, result2, 0, _dataSize);
            return result2;
        }

        /// <summary>
        /// Berlekamp-Massey algoritması kullanarak hata konumlarını bulur
        /// </summary>
        private int[] FindErrorLocations(int[] syndromes)
        {
            // Berlekamp-Massey algoritması
            int[] lambda = new int[_errorCorrectionSize + 1];
            lambda[0] = 1;
            
            int[] b = new int[_errorCorrectionSize + 1];
            b[0] = 1;
            
            int L = 0;
            int m = 1;
            
            for (int r = 0; r < _errorCorrectionSize; r++)
            {
                int delta = syndromes[r];
                for (int j = 1; j <= L; j++)
                {
                    delta = _field.Add(delta, _field.Multiply(lambda[j], syndromes[r - j]));
                }
                
                b = ShiftLeft(b);
                
                if (delta != 0)
                {
                    int[] t = new int[lambda.Length];
                    Array.Copy(lambda, 0, t, 0, lambda.Length);
                    
                    for (int i = 0; i < b.Length; i++)
                    {
                        lambda[i] = _field.Add(lambda[i], _field.Multiply(delta, b[i]));
                    }
                    
                    if (L * 2 <= r)
                    {
                        L = r + 1 - L;
                        b = t.Select(x => _field.Multiply(x, _field.Inverse(delta))).ToArray();
                        m = 1;
                    }
                    else
                    {
                        m++;
                    }
                }
                else
                {
                    m++;
                }
            }
            
            // Hata konumlarını bul
            int[] errorLocations = new int[L];
            int count = 0;
            
            for (int i = 1; i < FieldSize; i++)
            {
                int x = _field.Exp(i);
                int result = lambda[0];
                
                for (int j = 1; j <= L; j++)
                {
                    result = _field.Add(result, _field.Multiply(lambda[j], _field.Exp((i * j) % (FieldSize - 1))));
                }
                
                if (result == 0 && count < L)
                {
                    errorLocations[count++] = x;
                }
            }
            
            return errorLocations;
        }

        /// <summary>
        /// Hata değerlerini bulur
        /// </summary>
        private int[] FindErrorValues(int[] syndromes, int[] errorLocations)
        {
            int[] errorValues = new int[errorLocations.Length];
            int[] omega = new int[_errorCorrectionSize];
            
            // Forney algoritması
            for (int i = 0; i < errorLocations.Length; i++)
            {
                int xi = _field.Inverse(errorLocations[i]);
                int[] denominator = new int[errorLocations.Length];
                
                for (int j = 0; j < errorLocations.Length; j++)
                {
                    if (i != j)
                    {
                        denominator[j] = _field.Subtract(1, _field.Multiply(xi, _field.Inverse(errorLocations[j])));
                    }
                }
                
                int denominatorProduct = 1;
                for (int j = 0; j < errorLocations.Length; j++)
                {
                    if (i != j)
                    {
                        denominatorProduct = _field.Multiply(denominatorProduct, denominator[j]);
                    }
                }
                
                int numerator = syndromes[0];
                for (int j = 1; j < _errorCorrectionSize; j++)
                {
                    numerator = _field.Add(numerator, _field.Multiply(syndromes[j], _field.Exp(j * _field.Log(xi))));
                }
                
                errorValues[i] = _field.Multiply(numerator, _field.Inverse(denominatorProduct));
            }
            
            return errorValues;
        }

        /// <summary>
        /// Sendromları hesaplar
        /// </summary>
        private int[] CalculateSyndromes(byte[] data)
        {
            int[] syndromes = new int[_errorCorrectionSize];
            
            for (int i = 0; i < _errorCorrectionSize; i++)
            {
                syndromes[i] = EvaluatePolynomial(data, _field.Exp(i));
            }
            
            return syndromes;
        }

        /// <summary>
        /// Polinomu değerlendirir
        /// </summary>
        private int EvaluatePolynomial(byte[] poly, int x)
        {
            int result = poly[0];
            
            for (int i = 1; i < poly.Length; i++)
            {
                result = _field.Add(_field.Multiply(result, x), poly[i]);
            }
            
            return result;
        }

        /// <summary>
        /// Üreteç polinomu oluşturur
        /// </summary>
        private int[] GenerateGenerator(int numRoots)
        {
            int[] g = { 1 };
            
            for (int i = 0; i < numRoots; i++)
            {
                int[] p = { 1, _field.Exp(i) };
                g = MultiplyPolynomials(g, p);
            }
            
            return g;
        }

        /// <summary>
        /// İki polinomu çarpar
        /// </summary>
        private int[] MultiplyPolynomials(int[] p1, int[] p2)
        {
            int[] result = new int[p1.Length + p2.Length - 1];
            
            for (int i = 0; i < p1.Length; i++)
            {
                for (int j = 0; j < p2.Length; j++)
                {
                    result[i + j] = _field.Add(result[i + j], _field.Multiply(p1[i], p2[j]));
                }
            }
            
            return result;
        }

        /// <summary>
        /// Polinomu sola kaydırır
        /// </summary>
        private int[] ShiftLeft(int[] poly)
        {
            int[] result = new int[poly.Length];
            
            for (int i = 1; i < poly.Length; i++)
            {
                result[i - 1] = poly[i];
            }
            
            return result;
        }
    }

    /// <summary>
    /// Sonlu (Galois) alanı işlemleri için yardımcı sınıf
    /// </summary>
    internal class GaloisField
    {
        private readonly int[] _expTable;
        private readonly int[] _logTable;
        private readonly int _size;

        /// <summary>
        /// Galois alanı oluşturur
        /// </summary>
        /// <param name="fieldSize">Alan büyüklüğü (2^n)</param>
        public GaloisField(int fieldSize)
        {
            _size = fieldSize;
            _expTable = new int[fieldSize * 2];
            _logTable = new int[fieldSize];
            
            // Primitive polinomial: x^8 + x^4 + x^3 + x^2 + 1 (0x11D)
            int primitive = 0x11D;
            
            int x = 1;
            for (int i = 0; i < fieldSize - 1; i++)
            {
                _expTable[i] = x;
                _logTable[x] = i;
                
                x <<= 1;
                if (x >= fieldSize)
                {
                    x ^= primitive;
                }
            }
            
            // Exp tablosunu uzat
            for (int i = fieldSize - 1; i < fieldSize * 2 - 1; i++)
            {
                _expTable[i] = _expTable[i - (fieldSize - 1)];
            }
        }

        /// <summary>
        /// İki sayıyı toplar (XOR)
        /// </summary>
        public int Add(int a, int b)
        {
            return a ^ b;
        }

        /// <summary>
        /// İki sayının farkını bulur (XOR)
        /// </summary>
        public int Subtract(int a, int b)
        {
            return a ^ b; // Sonlu alanda toplama ve çıkarma aynıdır
        }

        /// <summary>
        /// İki sayıyı çarpar
        /// </summary>
        public int Multiply(int a, int b)
        {
            if (a == 0 || b == 0)
                return 0;
            
            return _expTable[(_logTable[a] + _logTable[b]) % (_size - 1)];
        }

        /// <summary>
        /// Sayının tersini alır
        /// </summary>
        public int Inverse(int a)
        {
            if (a == 0)
                throw new ArgumentException("Sıfırın tersi yoktur");
            
            return _expTable[_size - 1 - _logTable[a]];
        }

        /// <summary>
        /// Sayının üssünü alır
        /// </summary>
        public int Exp(int power)
        {
            return _expTable[power % (_size - 1)];
        }

        /// <summary>
        /// Sayının logaritmasını alır
        /// </summary>
        public int Log(int value)
        {
            if (value == 0)
                throw new ArgumentException("Sıfırın logaritması yoktur");
            
            return _logTable[value];
        }
    }
} 