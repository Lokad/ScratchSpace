using Lokad.ContentAddr;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Lokad.ScratchSpace
{
    /// <summary>
    ///     Used for reading from a <see cref="ReadOnlySpan{T}"/> data that was
    ///     written with a <see cref="BlittableWriter"/>.
    /// </summary>
    public ref struct BlittableReader
    {
        /// <summary> The memory left to read. </summary>
        private ReadOnlySpan<byte> _memory;

        /// <summary> The number of bytes left to read.  </summary>
        public int Length => _memory.Length;

        public BlittableReader(ReadOnlySpan<byte> memory) =>
            _memory = memory;

        /// <summary>
        ///     Read a value of type <typeparamref name="T"/>, then 
        ///     move the cursor forward.
        /// </summary>
        public unsafe T Read<T>() where T : unmanaged
        {
            var result = MemoryMarshal.Read<T>(_memory);
            _memory = _memory.Slice(sizeof(T));
            return result;
        }

        /// <summary>
        ///     Read an array of <paramref name="count"/> values of 
        ///     type <typeparamref name="T"/>, then move the cursor
        ///     forward.
        /// </summary>
        public unsafe T[] Read<T>(int count) where T : unmanaged
        {
            var array = new T[count];
            MemoryMarshal.Cast<byte, T>(_memory).Slice(0, count).CopyTo(array);
            _memory = _memory.Slice(count * sizeof(T));
            return array;
        }

        /// <summary>
        ///     Extract a span of <paramref name="count"/> elements of 
        ///     type <typeparamref name="T"/>, then move the cursor 
        ///     forward.
        /// </summary>
        public unsafe ReadOnlySpan<T> ReadSpan<T>(int count) where T : unmanaged
        {
            var span = MemoryMarshal.Cast<byte, T>(_memory).Slice(0, count);
            _memory = _memory.Slice(count * sizeof(T));
            return span;
        }

        /// <summary> Read a string encoded as UTF-8, then move the block forward. </summary>
        public string ReadString() =>
            Encoding.UTF8.GetString(Read<byte>(Read<int>()));

        /// <summary> 
        ///     Return the next <paramref name="sizeInBytes"/> bytes as an in-memory 
        ///     stream. The bytes will be copied.
        /// </summary>
        public MemoryStream ReadStream(int sizeInBytes) =>
            new MemoryStream(Read<byte>(sizeInBytes), writable: false);

        /// <summary>
        ///     Return a reader that reads from a specified slice of the 
        ///     current reader. Does not advance the current reader.
        /// </summary>
        public BlittableReader GetSubReader(int start, int length) =>
            new BlittableReader(_memory.Slice(start, length));

        /// <summary> Skip the next <paramref name="byteCount"/> bytes. </summary>
        public void Skip(int byteCount) =>
            _memory = _memory.Slice(byteCount);
        
        /// <summary>
        ///     To be called at the beginning of a checksummed region of 
        ///     <paramref name="sizeInBytes"/> bytes, followed by the 
        ///     checksum over 4 bytes. This will read ahead and check the 
        ///     checksum to ensure that the contents are valid.
        /// </summary>
        public void StartCheckSumRegion(int sizeInBytes)
        {
            var realCrc32 = Checksum.CRC32(_memory.Slice(0, sizeInBytes));
            var expectedCrc32 = MemoryMarshal.Read<uint>(_memory.Slice(sizeInBytes));

            if (realCrc32 != expectedCrc32)
                throw new CheckSumFailedException();
        }

        /// <summary>
        /// This notifies the reader that we read the whole content, 
        /// and that it must skip the next 4 bytes where the checksum of the previosu content was written.
        /// </summary>
        public void EndCheckSumRegion() =>
            Skip(sizeof(int));
    }
}
