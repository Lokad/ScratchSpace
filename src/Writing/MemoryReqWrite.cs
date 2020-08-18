using Lokad.ScratchSpace.Files;
using System;
using System.Runtime.InteropServices;

namespace Lokad.ScratchSpace.Writing
{
    public class MemoryReqWrite<T> : IReqWrite where T: unmanaged
    {
        private readonly ReadOnlyMemory<T> _memory;

        public MemoryReqWrite(ReadOnlyMemory<T> memory)
        {
            _memory = memory;
        }

        public unsafe int Size => sizeof(T) * _memory.Length;

        public void WithSpan(WithSpan.ReadOnly onSpan)
        {
            var bytes = MemoryMarshal.Cast<T, byte>(_memory.Span);
            onSpan(bytes);
        }

        public void BlitTo(Span<byte> destination)
        {
            var bytes = MemoryMarshal.Cast<T, byte>(_memory.Span);
            bytes.CopyTo(destination);
        }
    }
}
