using System;
using System.IO;

namespace Lokad.ScratchSpace
{
    /// <summary>
    ///     Stream that encapsulates a BlittableWriter.
    ///     Writing to this stream writes to the underlying BlittableWriter.
    /// </summary>
    public class BlittableWriterStream : Stream
    {        
        private readonly BlittableWriter _blittableWriter;

        /// <summary>
        ///     Current array rented from the BlittableWriter.
        /// </summary>
        private byte[] _currentArray;

        /// <summary>
        ///     Next available byte in <see cref="_currentArray"/>.
        /// </summary>
        private int _positionInArray;

        /// <summary>
        ///     Writing is not available after flushing.
        /// </summary>
        private bool _canWrite;

        /// <summary>
        ///     Total number of bytes written.
        /// </summary>
        private long _position;

        /// <summary>
        ///     Length of the rented arrays by the BlittableWriter.
        /// </summary>
        private int _arrayLength;

        public BlittableWriterStream(BlittableWriter blittableWriter, int length)
        {
            _blittableWriter = blittableWriter;
            _currentArray = Array.Empty<byte>();
            _canWrite = true;
            _arrayLength = length;
        }

        public override bool CanRead => throw new NotSupportedException();

        public override bool CanSeek => throw new NotSupportedException();

        public override bool CanWrite => _canWrite;

        public override long Length => _position;

        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            var remaining = buffer.Length;
            var offset = 0;
            while (remaining > 0)
            {
                if (_currentArray.Length == _positionInArray)
                {
                    if (_currentArray.Length > 0)
                        _blittableWriter.Write(_currentArray);
                    _currentArray = _blittableWriter.RentArray(_arrayLength);
                    _positionInArray = 0;
                }

                var toWrite = Math.Min(buffer.Length - offset, _currentArray.Length - _positionInArray);
                buffer.Slice(offset, toWrite).CopyTo(_currentArray.AsSpan(_positionInArray));

                remaining -= toWrite;
                offset += toWrite;
                _positionInArray += toWrite;
                _position += toWrite;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Write(buffer.AsSpan(offset, count));
        }

        public override void Close()
        {
            _blittableWriter.Write(_currentArray[.._positionInArray]);
            _canWrite = false;
            base.Close();
        }
    }
}
