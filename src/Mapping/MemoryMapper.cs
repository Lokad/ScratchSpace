using System;
using System.IO.MemoryMappedFiles;

namespace Lokad.ScratchSpace.Mapping
{
    /// <summary>
    ///     Wraps around a <see cref="MemoryMappedFile"/> to provide views over its contents
    ///     as <see cref="Memory{T}"/>.
    /// </summary>
    public sealed unsafe partial class MemoryMapper : IFileMemory
    {
        /// <summary> The memory-mapped file. </summary>
        /// <remarks> 
        ///     Kept for disposal only, all access goes through <see cref="_mmva"/> 
        ///     and <see cref="_ptr"/> instead. 
        /// </remarks>
        private readonly MemoryMappedFile _mmf;

        /// <summary> View accessor covering all of <see cref="_mmf"/>. </summary>
        private readonly MemoryMappedViewAccessor _mmva;

        /// <summary> Pointer to the first byte of <see cref="_mmf"/> </summary>
        private readonly byte* _ptr;

        private bool _disposed;

        /// <summary> In bytes. </summary>
        public long Length { get; }

        /// <param name="mmf"> Backing file. </param>
        /// <param name="length"> In bytes. </param>
        public MemoryMapper(MemoryMappedFile mmf, long length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            _mmf = mmf ?? throw new ArgumentNullException(nameof(mmf));
            Length = length;
            _mmva = _mmf.CreateViewAccessor(0, Length);
            _mmva.SafeMemoryMappedViewHandle.AcquirePointer(ref _ptr);
            _disposed = false;
        }

        /// <summary> A portion of the entire memory range. </summary>
        public Memory<byte> AsMemory(long offset, int length)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (offset + length > Length)
                throw new ArgumentException(nameof(offset) + nameof(length));

            return new MappedMemory(_ptr, offset, length).Memory;
        }

        public void Flush(long offset, long length)
        {
            using (var fView = _mmf.CreateViewAccessor(offset, length))
            {
                fView.Flush();
            }
        }

        private void ReleaseUnmanagedResources()
        {
            if (_ptr != null)
            {
                _mmva.SafeMemoryMappedViewHandle.ReleasePointer();
            }
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            ReleaseUnmanagedResources();
            if (disposing)
            {
                _mmva?.Dispose();
                _mmf?.Dispose();
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~MemoryMapper()
        {
            Dispose(false);
        }
    }
}
