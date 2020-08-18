using Lokad.ScratchSpace.Blocks;
using Xunit;

namespace Lokad.ScratchSpace.Tests.Blocks
{
    public sealed class block_address
    {
        [Fact]
        public void default_is_none()
        {
            var d = default(BlockAddress);
            Assert.True(d.IsNone());
            Assert.True(d.Equals(BlockAddress.None));
        }

        [Fact]
        public void none_is_none() =>
            Assert.True(BlockAddress.None.IsNone());

        [Fact]
        public void none_file_zero() =>
            Assert.Equal(0U, BlockAddress.None.File());

        [Fact]
        public void none_offset_zero() =>
            Assert.Equal(0L, BlockAddress.None.FirstByteOffset());

        [InlineData(1, 0)]
        [InlineData(1023, 0)]
        [InlineData(256, 0)]
        [InlineData(1, BlockAddress.MaxFileSize - BlockAddress.BlockAlignment)]
        [Theory]
        public void preserve_file_offset(uint file, long firstByteOffset)
        {
            var addr = new BlockAddress(file, firstByteOffset);
            Assert.Equal(file, addr.File());
            Assert.Equal(firstByteOffset, addr.FirstByteOffset());
        }
    }
}
