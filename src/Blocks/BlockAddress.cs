using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Lokad.ScratchSpace.Blocks
{
    /// <summary> The address of a block. </summary>
    public struct BlockAddress : IEquatable<BlockAddress>
    {
        /// <summary> The maximum number of files over which blocks can be spread. </summary>
        /// <remarks> Files are numbered from 1 to this count. </remarks>
        public const int MaxFileCount = 1023;

        /// <summary>
        ///     Inside a file, a block's start position is always a multiplier 
        ///     of this many bytes.
        /// </summary>
        public const int BlockAlignment = 4096;

        /// <summary> The maximum size of a file, in bytes. </summary>
        public const long MaxFileSize = 
            (1 + (uint.MaxValue / (MaxFileCount + 1))) // one past the highest start address.
            * (long)BlockAlignment;

        /// <summary> The block file and byte offset. </summary>
        private uint _packed;

        public BlockAddress(uint file, long firstByteOffset)
        {
            Debug.Assert(file > 0);
            Debug.Assert(file <= MaxFileCount);
            Debug.Assert(firstByteOffset % BlockAlignment == 0);

            var offset = checked((uint)(firstByteOffset / BlockAlignment));

            _packed = file + offset * (MaxFileCount + 1);

            Debug.Assert(_packed != 0);
        }

        /// <summary> An address that points nowhere (offset 0 in file 0). </summary>
        public static BlockAddress None = default;

        /// <summary> Whether this block address is valid. </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNone() => _packed == 0;

        /// <summary> The file in which this block is found. </summary>
        /// <remarks> Will be 0 on a 'None' address. </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint File() => _packed % (MaxFileCount + 1);

        /// <summary> The offset of the first byte of this block, within its file. </summary>
        /// <remarks> Will be 0 on a 'None' address. </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long FirstByteOffset() => 
            // Integer division is important to trim off the file.
            _packed / (MaxFileCount + 1) * (long)BlockAlignment;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(BlockAddress other) =>
            _packed == other._packed;

        /// <summary>
        ///     Returns the smallest size greater than <paramref name="size"/> that
        ///     is aligned on <see cref="BlockAlignment"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Ceiling(long size)
        {
            var mod = size % BlockAlignment;
            if (mod == 0) return size;

            return size + (BlockAlignment - mod);
        }
    }
}
