using System;
using System.IO;

namespace Lokad.ScratchSpace.Mapping
{
    /// <summary> A stream that reads from <see cref="ReadOnlyMemory{byte}"/>. </summary>
    internal sealed class MemoryReadStream : Stream
    {
        /// <summary> A type of callback called when the stream is disposed. </summary>
        public delegate void DisposeCallback();

        /// <summary> To be invoked when the stream is disposed. </summary>
        private readonly DisposeCallback _onDispose;

        /// <summary> Backing for <see cref="Position"/> </summary>
        private int _position;

        /// <summary> The underlying memory. </summary>
        public ReadOnlyMemory<byte> Memory { get; }

        public int Remaining => Memory.Length - _position;

        public MemoryReadStream(ReadOnlyMemory<byte> memory) : this(memory, null) {}

        public MemoryReadStream(
            ReadOnlyMemory<byte> memory,
            DisposeCallback onDispose)
        {
            Memory = memory;
            _position = 0;
            _onDispose = onDispose;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => Memory.Length;

        public override long Position
        {
            get => _position;
            set
            {
                var intVal = Convert.ToInt32(value);
                if (intVal < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));
                if (intVal > Memory.Length)
                    throw new EndOfStreamException();

                _position = intVal;
            }
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (offset + count > buffer.Length)
                throw new ArgumentException(nameof(offset) + nameof(count));

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (_position + count > Memory.Length)
            {
                count = Memory.Length - _position; // truncate if remaining data length is insufficient
            }

            Memory.Slice(_position, count).CopyTo(buffer.AsMemory(offset, count));
            _position += count;
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    return Position;
                case SeekOrigin.End:
                    Position = Memory.Length + Convert.ToInt32(offset);
                    return Position;
                case SeekOrigin.Current:
                    Position = Position + offset;
                    return Position;
                default:
                    throw new NotSupportedException();
            }
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public ReadOnlySpan<byte> ReadBytes(int count)
        {
            var span = Memory.Slice(_position, count).Span;
            _position += count;
            return span;
        }

        protected override void Dispose(bool disposing)
        {
            _onDispose?.Invoke();
            base.Dispose(disposing);
        }
    }
}
