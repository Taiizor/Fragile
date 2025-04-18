namespace Fragile.Streams
{
    /// <summary>
    /// A stream that represents a chunk of another stream
    /// </summary>
    internal class ChunkStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly long _start;
        private readonly long _length;
        private long _position;

        public ChunkStream(Stream baseStream, long start, long length)
        {
            _position = 0;
            _start = start;
            _length = length;
            _baseStream = baseStream;

            // Position the base stream at the start of the chunk
            _baseStream.Position = _start;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > _length)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _position = value;
                _baseStream.Position = _start + _position;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= _length)
            {
                return 0;
            }

            int bytesToRead = (int)Math.Min(count, _length - _position);
            int bytesRead = _baseStream.Read(buffer, offset, bytesToRead);

            _position += bytesRead;

            return bytesRead;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_position >= _length)
            {
                return 0;
            }

            int bytesToRead = (int)Math.Min(count, _length - _position);

#if NET48_OR_GREATER || NETSTANDARD2_0
            int bytesRead = await _baseStream.ReadAsync(buffer, offset, bytesToRead, cancellationToken).ConfigureAwait(false);
#else
            int bytesRead = await _baseStream.ReadAsync(buffer.AsMemory(offset, bytesToRead), cancellationToken).ConfigureAwait(false);
#endif

            _position += bytesRead;

            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPosition = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _length + offset,
                _ => throw new ArgumentException("Invalid seek origin", nameof(origin))
            };

            if (newPosition < 0 || newPosition > _length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            _position = newPosition;
            _baseStream.Position = _start + _position;

            return _position;
        }

        public override void Flush()
        {
            _baseStream.Flush();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}