using Lokad.ContentAddr;
using Lokad.ScratchSpace.Blocks;
using System.Diagnostics;
using System.Runtime.CompilerServices;

[assembly:InternalsVisibleTo("Lokad.ScratchSpace.Tests")]

namespace Lokad.ScratchSpace.Indexing
{
    /// <summary> An entry in the <see cref="BlockIndex"/>. </summary> 
    /// <remarks>
    ///     The Block Index is a large hash table (2^24 entries), so index entries
    ///     should remain small (28 bytes).  
    ///     
    ///     All the fields in this struct are given meaning by <see cref="BlockIndex"/>.
    /// </remarks>
    internal struct IndexEntry
    {
        /// <summary> The key of this entry. </summary>
        public EntryKey Key; // 16 bytes

        /// <summary>
        ///     If the entry is at position X, returns the position of the first entry in 
        ///     bucket X. Most of the time, this value will be equal to the position of 
        ///     the entry (i.e. the first entry of bucket *is* at position X) but sometimes,
        ///     collisions will require values to be moved. 
        /// </summary>
        /// <remarks> -1 if the bucket does not contain anything. </remarks>
        public int FirstInBucket; // 4 bytes

        /// <summary> The position of the next entry in the same bucket as this one. </summary>
        /// <remarks> 
        ///     -1 if the bucket contains no more entries. 
        ///
        ///     When in the "free list" (of entries that are currently not in any bucket), 
        /// </remarks>
        public int NextInBucket; // 4 bytes        

        /// <summary> The address of the block. </summary>
        /// <remarks> 
        ///     An index entry becomes "dead" at the moment its address becomes
        ///     <see cref="BlockAddress.None"/>.
        /// </remarks>
        public BlockAddress Address; // 4 bytes

        /// <summary> The key used for an index search. </summary>
        public struct EntryKey
        {
            public EntryKey(Hash hash, uint realm)
            {
                Debug.Assert((realm & RightRealmMask) == realm);

                HashLeft = hash.HashLeft;
                HashRightAndRealm = (RightHashMask & hash.HashRight) | realm;
            }

            /// <summary> The left portion of the Hash. </summary>
            public readonly ulong HashLeft;

            /// <summary> 
            ///     The first 5 bytes of the right portion of the Hash, 
            ///     followed by the 3 bytes of the realm. </summary>
            /// <remarks> 
            ///     We XOR with the realm so that we can save the 4 bytes needed to store it.
            ///     Consider: the realm is always 3 bytes, and the last 3 bytes of the 
            ///     hash are equal to the bucket number that the index is stored in.
            /// </remarks>
            public readonly ulong HashRightAndRealm;

            /// <summary>
            ///     Mask to select the hash section of the 
            ///     <see cref="_hashRightAndRealm"/>.
            /// </summary>
            private const ulong RightHashMask = 0xFFFF_FFFF_FF00_0000UL;

            /// <summary>
            ///     Mask to select the real section of the 
            ///     <see cref="_hashRightAndRealm"/>
            /// </summary>
            private const uint RightRealmMask = 0x00FF_FFFFU;

            /// <summary> The number of buckets that should be present in the block index. </summary>
            public const int BucketCount = 1 + (int)RightRealmMask; 

            /// <summary> The hash of the block stored in this entry. </summary>
            /// <remarks> Part of the hash is stored in the block number. </remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Hash Hash(int bucket) =>
                new Hash(HashLeft, (HashRightAndRealm & RightHashMask) | (uint)bucket);

            /// <summary> The 24-bit realm of the entry. </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public uint Realm() => RightRealmMask & (uint)HashRightAndRealm;

            /// <summary>
            ///     The 24-bit bucket from a hash, so that the <see cref="Hash"/> can properly 
            ///     recompose the full hash from the bucket.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int BucketOfHash(Hash hash) =>
                (int)((uint)hash.HashRight & RightRealmMask);

            /// <summary> True if two entry keys are equal. </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(EntryKey other) =>
                other.HashLeft == HashLeft && other.HashRightAndRealm == HashRightAndRealm;
        }
    }
}
