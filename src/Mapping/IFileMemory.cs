using System;

namespace Lokad.ScratchSpace.Mapping
{
    /// <summary> A source of memory in a file. </summary>
    public interface IFileMemory : IDisposable
    {
        /// <summary> A portion of the file, as memory. </summary>
        Memory<byte> AsMemory(long offset, int length);

        /// <summary> Flush a portion of the written memory, to the file. </summary>
        void Flush(long offset, long length);

        /// <summary> Total length of the file. </summary>
        long Length { get; }
    }
}
