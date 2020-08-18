using System;

namespace Lokad.ScratchSpace.Mapping
{
    /// <summary>
    ///     A fully in-memory implementation of <see cref="IFileMemory"/>
    /// </summary>
    public sealed class VolatileMemory : IFileMemory
    {
        /// <summary> In-memory byte array that pretends to be in a file. </summary>
        private readonly byte[] _backing;

        public VolatileMemory(int length) =>
            _backing = new byte[length];

        /// <see cref="IFileMemory.Length"/>
        public long Length => _backing.Length;

        /// <see cref="IFileMemory.AsMemory"/>
        public Memory<byte> AsMemory(long offset, int length) =>
            _backing.AsMemory((int)offset, length);

        public void Dispose() { }

        /// <see cref="IFileMemory.Flush"/>
        public void Flush(long offset, long length) { }

        /// <summary> Reset internal memory to zero. </summary>
        internal void Clear() =>
            Array.Clear(_backing, 0, _backing.Length);
    }
}
