using Lokad.ContentAddr;
using System.Runtime.InteropServices;

namespace Lokad.ScratchSpace.Blocks
{
    /// <summary> The header of a <see cref="Block"/>. </summary>
    /// <remarks>
    ///     Even though it only needs 28 bytes, enforcing a 32-byte size 
    ///     ensures that the data following it will still be aligned on 
    ///     16 bytes.
    /// </remarks>
    [StructLayout(LayoutKind.Explicit, Size = 32, CharSet = CharSet.Ansi)]
    public struct BlockHeader
    {
        /// <summary> The size of this header, in bytes. </summary>
        public const int Size = 32;

        /// <summary> The hash of this block. </summary>
        /// <remarks> Only covers the block contents, not the header itself. </remarks>
        [FieldOffset(0)]
        public Hash Hash;

        /// <summary> The realm of this block. </summary>
        [FieldOffset(16)]
        public uint Realm;

        /// <summary> The rank of this block among all blocks in this file. </summary>
        /// <remarks> 
        ///     The first block has rank 0, the second has rank 1, etc. 
        ///     Ranks are useful in order to more easily associate a piece of data
        ///     with each block.
        /// </remarks>
        [FieldOffset(20)]
        public int Rank;

        /// <summary> The length of this block's contents. </summary>
        [FieldOffset(24)]
        public int ContentLength;
    }
}
