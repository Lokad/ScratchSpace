using System;
using System.Runtime.InteropServices;
using Lokad.ContentAddr;
using Lokad.ScratchSpace.Blocks;
using Xunit;

namespace Lokad.ScratchSpace.Tests.Blocks
{
    public sealed class block
    {
        [Fact]
        public void empty()
        {
            var data = new byte[]
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                1, 0, 0, 0,
                2, 0, 0, 0,
                0, 0, 0, 0, 
                0xFF, 0xFF, 0xFF, 0xFF
            };

            var block = new Block(data);

            Assert.Equal(default, block.Header.Hash);
            Assert.Equal(1U, block.Header.Realm);
            Assert.Equal(2, block.Header.Rank);
            Assert.Equal(0, block.Header.ContentLength);
            Assert.Equal(0, block.Contents.Length);
            Assert.Equal(BlockAddress.BlockAlignment, block.RelativeOffsetToNextBlock);
        }

        [Fact]
        public void contents()
        {
            var data = new byte[]
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                1, 0, 0, 0,
                2, 0, 0, 0,
                4, 0, 0, 0,
                0xFF, 0xFF, 0xFF, 0xFF,
                1, 2, 3, 4
            };

            var block = new Block(data);

            Assert.Equal(default, block.Header.Hash);
            Assert.Equal(1U, block.Header.Realm);
            Assert.Equal(2, block.Header.Rank);
            Assert.Equal(4, block.Header.ContentLength);
            Assert.Equal(4, block.Contents.Length);
            Assert.Equal(1, block.Contents[0]);
            Assert.Equal(2, block.Contents[1]);
            Assert.Equal(3, block.Contents[2]);
            Assert.Equal(4, block.Contents[3]);
            Assert.Equal(BlockAddress.BlockAlignment, block.RelativeOffsetToNextBlock);
        }

        [Fact]
        public void writable_header()
        {
            var data = new byte[]
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                1, 0, 0, 0,
                2, 0, 0, 0,
                0, 0, 0, 0,
                0xFF, 0xFF, 0xFF, 0xFF
            };

            var h = new Hash("696695F3118DAB5A86F33ACECB67EBE0");

            var block = new Block(data);

            block.Header.Hash = h;

            Assert.Equal(h, MemoryMarshal.Cast<byte, Hash>(data.AsSpan())[0]);
        }
    }
}
