using System;
using System.Text;
using Lokad.ContentAddr;
using Lokad.ScratchSpace.Blocks;
using Xunit;

namespace Lokad.ScratchSpace.Tests.Blocks
{
    public sealed class block_hasher
    {
        [Fact]
        public void compute_hash_of_empty()
        {
            var h = BlockHasher.ComputeHash(Array.Empty<byte>());
            Assert.Equal(new Hash("696695F3118DAB5A86F33ACECB67EBE0"), h);
        }

        [Fact]
        public void compute_hash_of_hello_world()
        {
            var buf = Encoding.UTF8.GetBytes("Hello, world!");
            var h = BlockHasher.ComputeHash(buf);
            Assert.Equal(new Hash("307EA73B830D76AD85ED617FADBD655C"), h);
        }

        [Fact]
        public void iterated_of_hello_world()
        {
            var buf = Encoding.UTF8.GetBytes("Hello, world!");
            var hasher = BlockHasher.Create();
            hasher.Update(buf.AsSpan(0, 8));
            hasher.Update(buf.AsSpan(8));
            var h = hasher.Final();
            Assert.Equal(new Hash("307EA73B830D76AD85ED617FADBD655C"), h);
        }
    }
}
