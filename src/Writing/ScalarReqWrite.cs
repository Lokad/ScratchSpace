using Lokad.ScratchSpace.Files;
using System;
using System.Runtime.InteropServices;

namespace Lokad.ScratchSpace.Writing
{
    public sealed class ScalarReqWrite<T> : IReqWrite where T : unmanaged
    {
        private T _scalar;

        public unsafe int Size => sizeof(T);

        public ScalarReqWrite(T scalar)
        {
            _scalar = scalar;
        }

        public void WithSpan(WithSpan.ReadOnly onSpan)
        {
            Span<byte> temp = stackalloc byte[Size];
            MemoryMarshal.Write(temp, ref _scalar);
            onSpan(temp);
        }

        public void BlitTo(Span<byte> destination) =>
            MemoryMarshal.Write(destination, ref _scalar);
    }
}
