using System;

namespace Lokad.ScratchSpace.Files
{
    // We cannot use `Action<Span<byte>>` et al. because spans cannot be 
    // generic type arguments, so we need to define them as delegates 
    // instead. 
    public static class WithSpan
    {
        /// <summary>
        ///     A function that takes a read-only span and returns a value of
        ///     an arbitrary type.
        /// </summary>
        public delegate T ReadOnlyReturns<T>(ReadOnlySpan<byte> span);

        /// <summary>
        ///     A function that takes a read-only span and returns a value of
        ///     an arbitrary type.
        /// </summary>
        public delegate T ReadWriteReturns<T>(Span<byte> span);

        /// <summary> A function that takes a read-only span and returns nothing. </summary>
        public delegate void ReadOnly(ReadOnlySpan<byte> span);

        /// <summary> A function that takes a read-write span and returns nothing. </summary>
        public delegate void ReadWrite(Span<byte> span);

        /// <summary>
        ///     A function that takes a blittable reader and returns 
        ///     a value of an arbtirary type.
        /// </summary>
        public delegate T WithReader<T>(BlittableReader reader);
    }
}
