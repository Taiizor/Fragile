namespace Fragile.ErrorCorrection
{
    /// <summary>
    /// Implementation of Reed-Solomon error correction algorithm
    /// </summary>
    internal class ReedSolomonAlgorithm
    {
        private readonly int _dataSize;
        private readonly GaloisField _field;
        private readonly int _errorCorrectionSize;

        // Galois Field size (2^8)
        private const int FieldSize = 256;

        // Maximum block size (255 bytes for GF(2^8))
        private const int MaxBlockSize = 255;

        /// <summary>
        /// Creates a Reed-Solomon error correction algorithm
        /// </summary>
        /// <param name="dataSize">Data size</param>
        /// <param name="errorCorrectionSize">Error correction data size</param>
        public ReedSolomonAlgorithm(int dataSize, int errorCorrectionSize)
        {
            if (dataSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dataSize), "Data size must be positive");
            }

            if (errorCorrectionSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(errorCorrectionSize), "Error correction size must be positive");
            }

            if (dataSize + errorCorrectionSize > FieldSize - 1)
            {
                throw new ArgumentException($"Total size (data + error correction) must be less than {FieldSize - 1} bytes");
            }

            _dataSize = dataSize;
            _errorCorrectionSize = errorCorrectionSize;
            _field = new GaloisField(FieldSize);
        }

        /// <summary>
        /// Adds error correction codes to the given data
        /// </summary>
        /// <param name="data">Data to be protected</param>
        /// <returns>Data with error correction codes added</returns>
        public byte[] Encode(byte[] data)
        {
#if NET48_OR_GREATER || NETSTANDARD2_0_OR_GREATER
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
#else
            ArgumentNullException.ThrowIfNull(data);
#endif

            if (data.Length != _dataSize)
            {
                throw new ArgumentException($"Data size ({data.Length}) does not match expected size ({_dataSize})");
            }

            // Create generator polynomial
            int[] generator = GenerateGenerator(_errorCorrectionSize);

            // Create output array
            byte[] result = new byte[_dataSize + _errorCorrectionSize];

            // Copy original data
            Array.Copy(data, 0, result, 0, _dataSize);

            // Systematic encoding: Calculate Reed-Solomon parities
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

            // Copy original data again and move parity data to correct position
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
        /// Decodes error-corrected data and corrects errors
        /// </summary>
        /// <param name="data">Error-corrected data</param>
        /// <returns>Data with errors corrected and number of errors fixed</returns>
        public (byte[] data, int errorsFixed) Decode(byte[] data)
        {
#if NET48_OR_GREATER || NETSTANDARD2_0_OR_GREATER
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
#else
            ArgumentNullException.ThrowIfNull(data);
#endif

            if (data.Length != _dataSize + _errorCorrectionSize)
            {
                throw new ArgumentException($"Data size ({data.Length}) does not match expected total size ({_dataSize + _errorCorrectionSize})");
            }

            // Copy data
            byte[] receivedData = new byte[data.Length];
            Array.Copy(data, 0, receivedData, 0, data.Length);

            // Calculate syndromes
            int[] syndromes = CalculateSyndromes(receivedData);

            // If all syndrome values are zero, there are no errors
            if (syndromes.All(s => s == 0))
            {
                byte[] result = new byte[_dataSize];
                Array.Copy(receivedData, _errorCorrectionSize, result, 0, _dataSize);
                return (result, 0);
            }

            // Find error locations
            int[] errorLocations = FindErrorLocations(syndromes);
            int errorsFixed = errorLocations.Length;

            // Find error values
            int[] errorValues = FindErrorValues(syndromes, errorLocations);

            // Correct errors
            for (int i = 0; i < errorLocations.Length; i++)
            {
                int position = _field.Log(_field.Inverse(errorLocations[i]));
                if (position < receivedData.Length)
                {
                    receivedData[position] = (byte)_field.Add(receivedData[position], errorValues[i]);
                }
            }

            // Return only the data part
            byte[] result2 = new byte[_dataSize];
            Array.Copy(receivedData, _errorCorrectionSize, result2, 0, _dataSize);
            return (result2, errorsFixed);
        }

        /// <summary>
        /// Finds error locations using the Berlekamp-Massey algorithm
        /// </summary>
        private int[] FindErrorLocations(int[] syndromes)
        {
            // Berlekamp-Massey algorithm
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
                        b = [.. t.Select(x => _field.Multiply(x, _field.Inverse(delta)))];
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

            // Find error locations
            int[] errorLocations = new int[L];
            int count = 0;

            for (int i = 1; i < FieldSize; i++)
            {
                int x = _field.Exp(i);
                int result = lambda[0];

                for (int j = 1; j <= L; j++)
                {
                    result = _field.Add(result, _field.Multiply(lambda[j], _field.Exp(i * j % (FieldSize - 1))));
                }

                if (result == 0 && count < L)
                {
                    errorLocations[count++] = x;
                }
            }

            return errorLocations;
        }

        /// <summary>
        /// Finds error values
        /// </summary>
        private int[] FindErrorValues(int[] syndromes, int[] errorLocations)
        {
            int[] errorValues = new int[errorLocations.Length];
            int[] omega = new int[_errorCorrectionSize];

            // Forney algorithm
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
        /// Calculates syndromes
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
        /// Evaluates a polynomial
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
        /// Creates a generator polynomial
        /// </summary>
        private int[] GenerateGenerator(int numRoots)
        {
            int[] g = [1];

            for (int i = 0; i < numRoots; i++)
            {
                int[] p = [1, _field.Exp(i)];
                g = MultiplyPolynomials(g, p);
            }

            return g;
        }

        /// <summary>
        /// Multiplies two polynomials
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
        /// Shifts a polynomial to the left
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

        /// <summary>
        /// Returns the maximum supported block size
        /// </summary>
        /// <returns>Maximum block size for Reed-Solomon</returns>
        public static int GetMaxBlockSize()
        {
            return MaxBlockSize;
        }
    }

    /// <summary>
    /// Helper class for finite (Galois) field operations
    /// </summary>
    internal class GaloisField
    {
        private readonly int[] _expTable;
        private readonly int[] _logTable;
        private readonly int _size;

        /// <summary>
        /// Creates a Galois field
        /// </summary>
        /// <param name="fieldSize">Field size (2^n)</param>
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

            // Extend exp table
            for (int i = fieldSize - 1; i < (fieldSize * 2) - 1; i++)
            {
                _expTable[i] = _expTable[i - (fieldSize - 1)];
            }
        }

        /// <summary>
        /// Adds two numbers (XOR)
        /// </summary>
        public int Add(int a, int b)
        {
            return a ^ b;
        }

        /// <summary>
        /// Finds the difference between two numbers (XOR)
        /// </summary>
        public int Subtract(int a, int b)
        {
            return a ^ b; // Addition and subtraction are the same in finite fields
        }

        /// <summary>
        /// Multiplies two numbers
        /// </summary>
        public int Multiply(int a, int b)
        {
            if (a == 0 || b == 0)
            {
                return 0;
            }

            return _expTable[(_logTable[a] + _logTable[b]) % (_size - 1)];
        }

        /// <summary>
        /// Takes the inverse of a number
        /// </summary>
        public int Inverse(int a)
        {
            if (a == 0)
            {
                throw new ArgumentException("Zero has no inverse");
            }

            return _expTable[_size - 1 - _logTable[a]];
        }

        /// <summary>
        /// Raises to a power
        /// </summary>
        public int Exp(int power)
        {
            return _expTable[power % (_size - 1)];
        }

        /// <summary>
        /// Calculates the logarithm of a number
        /// </summary>
        public int Log(int value)
        {
            if (value == 0)
            {
                throw new ArgumentException("Logarithm of zero is undefined");
            }

            return _logTable[value];
        }
    }
}