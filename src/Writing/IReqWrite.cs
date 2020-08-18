using Lokad.ScratchSpace.Files;
using System;

namespace Lokad.ScratchSpace.Writing
{
    /// <summary> A write request for serializing data to a memory-mapped file. </summary>
    /// <remarks>
    ///     <see cref="BlittableWriter"/> serialization is performed in two steps:
    ///     
    ///     1. Decompose data into several <see cref="IReqWrite"/>, each request 
    ///        contains blittable primitives such as scalar or array; Size of actual 
    ///        serialization can be then evaluated, as well as its hash.
    ///     2. Write the actual bytes of <see cref="IReqWrite"/> one by one into a 
    ///        memory area allocated with the size computed in step 1.
    /// </remarks>
    public interface IReqWrite
    {
        /// <summary> The number of bytes written by this write request. </summary>
        int Size { get; }

        /// <summary> Expose the inner span (used to compute hash and checksum). </summary>
        void WithSpan(WithSpan.ReadOnly onSpan);

        /// <summary> Write the bytes to the provided destination. </summary>
        void BlitTo(Span<byte> destination);
    }
}
