using Lokad.ContentAddr;
using System;
using System.Collections.Generic;
using System.Text;
using Lokad.ScratchSpace.Indexing;
using Xunit;
using Lokad.ScratchSpace.Blocks;
using Xunit.Abstractions;
using System.Diagnostics;

namespace Lokad.ScratchSpace.Tests.Indexing
{
    public sealed class block_index
    {
        private readonly ITestOutputHelper _output;

        public block_index(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        ///     Creates a hash in the specified bucket. Two hashes are
        ///     equal if and only if they have the same bucket and seed.
        /// </summary>
        private static Hash HashInBucket(int bucket, int seed) =>
            new Hash((ulong)seed, (ulong)bucket);

        private readonly BlockIndex _index = new BlockIndex();

        [Fact]
        public void empty_contains_nothing() =>
            Assert.True(_index.Get(0, HashInBucket(0, 0)).IsNone());

        [Fact]
        public void return_added()
        {
            var a = new BlockAddress(1, 0);
            var h = HashInBucket(0, 0);
            Assert.True(_index.Add(0, h, a));
            Assert.Equal(a, _index.Get(0, h));
        }

        [Fact]
        public void do_not_return_removed()
        {
            var a = new BlockAddress(1, 0);
            var h = HashInBucket(0, 0);
            Assert.True(_index.Add(0, h, a));
            _index.Remove(0, h, a);
            Assert.True(_index.Get(0, h).IsNone());
        }

        [Fact]
        public void add_remove_add()
        {
            var a = new BlockAddress(1, 0);
            var h = HashInBucket(0, 0);
            Assert.True(_index.Add(0, h, a));
            _index.Remove(0, h, a);
            Assert.True(_index.Add(0, h, a));
            Assert.Equal(a, _index.Get(0, h));
        }

        [Fact]
        public void add_no_collision()
        {
            var a1 = new BlockAddress(1, 0);
            var a2 = new BlockAddress(2, 4096);
            var h1 = HashInBucket(0, 0);
            var h2 = HashInBucket(1, 1);

            Assert.True(_index.Add(0, h1, a1));
            Assert.True(_index.Add(0, h2, a2));
            Assert.Equal(a1, _index.Get(0, h1));
            Assert.Equal(a2, _index.Get(0, h2));
        }

        [Fact]
        public void add_overwrite()
        {
            var a1 = new BlockAddress(1, 0);
            var a2 = new BlockAddress(2, 4096);
            var h = HashInBucket(0, 0);

            Assert.True(_index.Add(0, h, a1));
            Assert.False(_index.Add(0, h, a2));
            Assert.Equal(a2, _index.Get(0, h));
        }

        [Fact]
        public void add_overwrite_remove_old()
        {
            var a1 = new BlockAddress(1, 0);
            var a2 = new BlockAddress(2, 4096);
            var h = HashInBucket(0, 0);

            Assert.True(_index.Add(0, h, a1));
            Assert.False(_index.Add(0, h, a2));
            _index.Remove(0, h, a1);
            Assert.Equal(a2, _index.Get(0, h));
        }

        [Fact]
        public void add_overwrite_remove_new()
        {
            var a1 = new BlockAddress(1, 0);
            var a2 = new BlockAddress(2, 4096);
            var h = HashInBucket(0, 0);

            Assert.True(_index.Add(0, h, a1));
            Assert.False(_index.Add(0, h, a2));
            _index.Remove(0, h, a2);
            Assert.True(_index.Get(0, h).IsNone());
        }

        [Fact]
        public void add_collision()
        {
            var a1 = new BlockAddress(1, 0);
            var a2 = new BlockAddress(2, 4096);
            var h1 = HashInBucket(0, 0);
            var h2 = HashInBucket(0, 1);

            Assert.True(_index.Add(0, h1, a1));
            Assert.True(_index.Add(0, h2, a2));
            Assert.Equal(a1, _index.Get(0, h1));
            Assert.Equal(a2, _index.Get(0, h2));
        }

        [Fact]
        public void add_collision_remove_first()
        {
            var a1 = new BlockAddress(1, 0);
            var a2 = new BlockAddress(2, 4096);
            var h1 = HashInBucket(0, 0);
            var h2 = HashInBucket(0, 1);

            Assert.True(_index.Add(0, h1, a1));
            Assert.True(_index.Add(0, h2, a2));
            _index.Remove(0, h1, a1);
            Assert.True(_index.Get(0, h1).IsNone());
            Assert.Equal(a2, _index.Get(0, h2));
        }

        [Fact]
        public void add_collision_remove_second()
        {
            var a1 = new BlockAddress(1, 0);
            var a2 = new BlockAddress(2, 4096);
            var h1 = HashInBucket(0, 0);
            var h2 = HashInBucket(0, 1);

            Assert.True(_index.Add(0, h1, a1));
            Assert.True(_index.Add(0, h2, a2));
            _index.Remove(0, h2, a2);
            Assert.True(_index.Get(0, h2).IsNone());
            Assert.Equal(a1, _index.Get(0, h1));
        }

        [Fact]
        public void add_collision_remove_mid()
        {
            var a1 = new BlockAddress(1, 0);
            var a2 = new BlockAddress(2, 4096);
            var a3 = new BlockAddress(3, 8192);
            var h1 = HashInBucket(0, 0);
            var h2 = HashInBucket(0, 1);

            Assert.True(_index.Add(0, h1, a1));
            Assert.True(_index.Add(0, h2, a2));
            Assert.True(_index.Add(1, h1, a3));
            _index.Remove(0, h2, a2);
            Assert.True(_index.Get(0, h2).IsNone());
            Assert.Equal(a1, _index.Get(0, h1));
            Assert.Equal(a3, _index.Get(1, h1));
        }

        [Fact(Skip = "Stress test; can take a while")]
        public void stress_test()
        {
            BlockAddress AddrOfIndex(int i)
            {
                var file = 1 + (i % BlockAddress.MaxFileCount);
                var offset = (uint)i / (BlockAddress.MaxFileCount) 
                    * (long)BlockAddress.BlockAlignment;
                return new BlockAddress((uint)file, offset);
            }

            Hash HashOfIndex(int i) => 
                HashInBucket(3 * i, i);

            var sw = Stopwatch.StartNew();

            for (var add = 0; add < IndexEntry.EntryKey.BucketCount * 2; ++add)
            {
                var rem = add - IndexEntry.EntryKey.BucketCount;
                if (rem >= 0)
                {
                    _index.Remove((uint)(rem % 1024), HashOfIndex(rem), AddrOfIndex(rem));
                }

                var chk = add - IndexEntry.EntryKey.BucketCount / 2;
                if (chk >= 0)
                {
                    Assert.Equal(
                        AddrOfIndex(chk),
                        _index.Get((uint)(chk % 1024), HashOfIndex(chk)));
                }

                Assert.True(_index.Add((uint)(add % 1024), HashOfIndex(add), AddrOfIndex(add)));
            }

            _output.WriteLine(
                $"{IndexEntry.EntryKey.BucketCount * 2} write-read-remove cycles in " +
                $"{sw.Elapsed}, {IndexEntry.EntryKey.BucketCount * 2 / sw.ElapsedMilliseconds}/ms");

            sw.Restart();

            for (var chk = IndexEntry.EntryKey.BucketCount; 
                 chk < IndexEntry.EntryKey.BucketCount * 2; ++chk)
            {
                Assert.Equal(
                    AddrOfIndex(chk),
                    _index.Get((uint)(chk % 1024), HashOfIndex(chk)));
            }

            _output.WriteLine(
                $"{IndexEntry.EntryKey.BucketCount} reads in " +
                $"{sw.Elapsed}, {IndexEntry.EntryKey.BucketCount / sw.ElapsedMilliseconds}/ms");

            // Joannes: 10k write / ms, 14k read / ms
            // Victor: 9k write / ms, 13k read / ms
        }
    }
}
