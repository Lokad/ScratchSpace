using Lokad.ScratchSpace.Files;
using System;
using System.Runtime.InteropServices;

namespace Lokad.ScratchSpace.Writing
{
    /// <summary>
    ///     Represent a value that must be computed after
    ///     it has been added to the BlittableWriter.
    /// </summary>
    public sealed class DelayedWrite<T> : IReqWrite where T : unmanaged 
    {
        public T Value;

        public unsafe int Size => sizeof(T);

        public void BlitTo(Span<byte> destination)
        {
            MemoryMarshal.Write(destination, ref Value);
        }

        public void WithSpan(WithSpan.ReadOnly onSpan)
        {
            Span<byte> temp = stackalloc byte[Size];
            MemoryMarshal.Write(temp, ref Value);
            onSpan(temp);
        }
    }
}
