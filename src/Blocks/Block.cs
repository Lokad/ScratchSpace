using System;
using System.Runtime.InteropServices;

namespace Lokad.ScratchSpace.Blocks
{
    /// <summary> An in-memory block. </summary>
    /// <remarks> 
    ///     No distinction is made between read-write and read-only blocks.
    /// </remarks>
    public ref struct Block
    {
        /// <summary> A long span that contains the block. </summary>
        /// <remarks> 
        ///     The block header starts at the beginning of the span, 
        ///     followed by the block contents. The block will most likely 
        ///     end before the end of the span.
        /// </remarks>
        private readonly Span<byte> _span;

        public Block(Span<byte> span)
        {
            _span = span;
        }

        /// <summary> The header of this block. </summary>
        public ref BlockHeader Header =>
            ref MemoryMarshal.Cast<byte, BlockHeader>(_span)[0];

        /// <summary>
        ///     The span that covers precisely the contents of this block.
        /// </summary>
        public Span<byte> Contents =>
            _span.Slice(BlockHeader.Size, Header.ContentLength);

        /// <summary>
        ///     The relative offset from the start of this block to the
        ///     start of the next block, in bytes. 
        /// </summary>
        public long RelativeOffsetToNextBlock =>
            BlockAddress.Ceiling(BlockHeader.Size + Header.ContentLength);
    }
}
