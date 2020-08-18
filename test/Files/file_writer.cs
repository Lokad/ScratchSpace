using Lokad.ScratchSpace.Blocks;
using Lokad.ScratchSpace.Files;
using Lokad.ScratchSpace.Mapping;
using System;
using System.Linq;
using System.Text;
using Xunit;

namespace Lokad.ScratchSpace.Tests.Files
{
    public sealed class file_writer
    {
        [Fact]
        public void initially_empty_file()
        {
            using (var data = new VolatileMemory(8192))
            {
                var (read, write) = FileWriter.CreateReaderWriterPair(data, 1);

                var blocks = read.EnumerateBlocks().ToArray();

                Assert.Empty(blocks);
            }
        }

        [Fact]
        public void write_block()
        {
            var raw = Encoding.UTF8.GetBytes("Hello, world!");
            var h = BlockHasher.ComputeHash(raw);

            using (var data = new VolatileMemory(8192))
            {
                var (read, write) = FileWriter.CreateReaderWriterPair(data, 1);

                var writePerformed = false;

                var addr = write.TryScheduleWrite(1337, h, raw.Length,
                    span =>
                    {
                        raw.AsSpan().CopyTo(span);
                        writePerformed = true;
                    });

                Assert.False(writePerformed);

                var blocks = read.EnumerateBlocks().ToArray();

                Assert.Single(blocks);
                Assert.Equal(1337U, blocks[0].realm);
                Assert.Equal(h, blocks[0].hash);
                Assert.Equal(1U, blocks[0].address.File());
                Assert.Equal(0L, blocks[0].address.FirstByteOffset());

                Assert.True(read.TryWithBlockAtAddress(
                    blocks[0].address,
                    blocks[0].realm,
                    blocks[0].hash,
                    span =>
                    {
                        var bytes = new byte[span.Length];
                        span.CopyTo(bytes);
                        return Encoding.UTF8.GetString(bytes);
                    },
                    out var hello));

                Assert.True(writePerformed);
                Assert.Equal("Hello, world!", hello);
            }
        }


        [Fact]
        public void write_blocks()
        {
            const int ba = BlockAddress.BlockAlignment;
            var raw = Encoding.UTF8.GetBytes("Hello, world!");
            var h = BlockHasher.ComputeHash(raw);

            using (var data = new VolatileMemory(2 * ba))
            {
                var (read, write) = FileWriter.CreateReaderWriterPair(data, 1);

                var writesPerformed = 0;

                write.TryScheduleWrite(1337, h, raw.Length,
                    span =>
                    {
                        raw.AsSpan().CopyTo(span);
                        ++writesPerformed;
                    });

                write.TryScheduleWrite(1338, h, raw.Length,
                    span =>
                    {
                        raw.AsSpan().CopyTo(span);
                        ++writesPerformed;
                    });

                Assert.Equal(0, writesPerformed);

                var blocks = read.EnumerateBlocks().ToArray();

                Assert.Equal(2, blocks.Length);

                Assert.Equal(1337U, blocks[0].realm);
                Assert.Equal(h, blocks[0].hash);
                Assert.Equal(1U, blocks[0].address.File());
                Assert.Equal(0L, blocks[0].address.FirstByteOffset());

                Assert.Equal(1338U, blocks[1].realm);
                Assert.Equal(h, blocks[1].hash);
                Assert.Equal(1U, blocks[1].address.File());
                Assert.Equal(ba, blocks[1].address.FirstByteOffset());

                Assert.True(read.TryWithBlockAtAddress(
                    blocks[1].address,
                    blocks[1].realm,
                    blocks[1].hash,
                    span =>
                    {
                        var bytes = new byte[span.Length];
                        span.CopyTo(bytes);
                        return Encoding.UTF8.GetString(bytes);
                    },
                    out var hello));

                Assert.Equal(1, writesPerformed);
                Assert.Equal("Hello, world!", hello);
            }
        }
    }
}
