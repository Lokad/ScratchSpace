using Lokad.ContentAddr;
using Lokad.ScratchSpace.Indexing;
using Xunit;

namespace Lokad.ScratchSpace.Tests.Indexing
{
    public sealed class index_entry_key
    {
        [InlineData(0x01234567_89abcdef, 0x01234567_89abcdef, 12, 0xabcdef)]
        [InlineData(0x01234567_89abcdef, 0x01234567_89abcdef, 13, 0xabcdef)]
        [InlineData(0x01234567_89abcdef, 0xfedcba98_76543210, 13, 0x543210)]
        [InlineData(0x01234567_89abcdef, 0xfedcba98_76543210, 0xFFFFFF, 0x543210)]
        [Theory]
        public void preserve(ulong hashLeft, ulong hashRight, uint realm, int bucket)
        {
            var hash = new Hash(hashLeft, hashRight);
            var k = new IndexEntry.EntryKey(hash, realm);

            Assert.Equal(bucket, IndexEntry.EntryKey.BucketOfHash(hash));
            Assert.Equal(hash, k.Hash(bucket));
            Assert.Equal(realm, k.Realm());
        }

        [Fact]
        public void different()
        {
            var h = new Hash(0x01234567_89abcdef, 0x01234567_89abcdef);

            var k1 = new IndexEntry.EntryKey(h, 1);
            var k2 = new IndexEntry.EntryKey(h, 2);

            Assert.False(k1.Equals(k2));
        }
    }
}
