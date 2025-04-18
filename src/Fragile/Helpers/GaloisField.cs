namespace Fragile.Helpers
{
    /// <summary>
    /// Helper class for finite (Galois) field operations
    /// </summary>
    internal class GaloisField
    {
        private readonly int _size;
        private readonly int[] _expTable;
        private readonly int[] _logTable;

        /// <summary>
        /// Creates a Galois field
        /// </summary>
        /// <param name="fieldSize">Field size (2^n)</param>
        public GaloisField(int fieldSize)
        {
            _size = fieldSize;
            _logTable = new int[fieldSize];
            _expTable = new int[fieldSize * 2];

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