using Lokad.ContentAddr;
using Lokad.ScratchSpace.Blocks;
using Lokad.ScratchSpace.Files;
using Lokad.ScratchSpace.Mapping;
using System;
using System.Linq;
using Xunit;

namespace Lokad.ScratchSpace.Tests.Files
{
    public sealed class block_file
    {
        [Fact]
        public void from_empty_file()
        {
            using (var data = new VolatileMemory(8192))
            using (var file = new BlockFile(data, 42))
            {
                file.DiscoverBlocks().ToArray();
                var blocks = file.EnumerateBlocks().ToArray();

                Assert.Single(blocks);
                Assert.Equal(default, blocks[0].hash);
                Assert.Equal(0U, blocks[0].realm);
                Assert.Equal(42U, blocks[0].address.File());
                Assert.Equal(0L, blocks[0].address.FirstByteOffset());
                Assert.False(file.TryWithBlockAtAddress(
                    new BlockAddress(42, 0),
                    0,
                    default,
                    span => { Assert.True(false); return 0; },
                    out _));
            }
        }

        private IFileMemory WithTwoBlocks(out Hash b1, out Hash b2)
        {
            const int ba = BlockAddress.BlockAlignment;
            var data = new VolatileMemory(2 * ba);

            var block1 = new Block(data.AsMemory(0, ba).Span)
            {
                Header = new BlockHeader
                {
                    Realm = 42,
                    ContentLength = 1024,
                    Rank = 0
                }
            };

            b1 = block1.Header.Hash = BlockHasher.ComputeHash(block1.Contents);

            var block2 = new Block(data.AsMemory(ba, ba).Span)
            {
                Header = new BlockHeader
                {
                    Realm = 1337,
                    ContentLength = 2514,
                    Rank = 1
                }
            };

            b2 = block2.Header.Hash = BlockHasher.ComputeHash(block2.Contents);

            return data;
        }

        [Fact]
        public void from_correct_hashes()
        {
            using (var data = WithTwoBlocks(out var h1, out var h2))
            using (var file = new BlockFile(data, 13))
            {
                file.DiscoverBlocks().ToArray();

                var blocks = file.EnumerateBlocks().ToArray();

                Assert.Equal(2, blocks.Length);

                Assert.Equal(h1, blocks[0].hash);
                Assert.Equal(42U, blocks[0].realm);
                Assert.Equal(13U, blocks[0].address.File());
                Assert.Equal(0L, blocks[0].address.FirstByteOffset());

                Assert.Equal(h2, blocks[1].hash);
                Assert.Equal(1337U, blocks[1].realm);
                Assert.Equal(13U, blocks[1].address.File());
                Assert.Equal(
                    BlockAddress.BlockAlignment, 
                    blocks[1].address.FirstByteOffset());

                Assert.True(file.TryWithBlockAtAddress(
                    new BlockAddress(13, 0),
                    42,
                    h1,
                    span => span.Length,
                    out var r1));

                Assert.Equal(1024, r1);

                Assert.True(file.TryWithBlockAtAddress(
                    new BlockAddress(13, BlockAddress.BlockAlignment),
                    1337,
                    h2,
                    span => span.Length,
                    out var r2));

                Assert.Equal(2514, r2);
            }
        }

        [Fact]
        public void check_file()
        {
            using (var data = WithTwoBlocks(out var h1, out _))
            using (var file = new BlockFile(data, 13))
            {
                try
                {
                    file.TryWithBlockAtAddress(
                        new BlockAddress(11, 0), // Actual is 13
                        42,
                        h1,
                        span => { Assert.True(false); return 0; },
                        out _);

                    Assert.True(false);
                }
                catch (ArgumentException a)
                {
                    Assert.Equal("address", a.ParamName);
                    Assert.Equal(
                        "Address in file 11 not present in file 13 (Parameter 'address')", 
                        a.Message);
                }
            }
        }

        [Fact]
        public void check_realm()
        {
            using (var data = WithTwoBlocks(out var h1, out _))
            using (var file = new BlockFile(data, 13))
            {
                Assert.False(file.TryWithBlockAtAddress(
                    new BlockAddress(13, 0),
                    40, // Actual is 42
                    h1,
                    span => { Assert.True(false); return 0; },
                    out _));
            }
        }

        [Fact]
        public void check_hash()
        {
            using (var data = WithTwoBlocks(out var h1, out _))
            using (var file = new BlockFile(data, 13))
            {
                Assert.False(file.TryWithBlockAtAddress(
                    new BlockAddress(13, BlockAddress.BlockAlignment),
                    1337,
                    h1, // Actual is h2
                    span => { Assert.True(false); return 0; },
                    out _));
            }
        }
    }
}
