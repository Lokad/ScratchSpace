using Lokad.ContentAddr;
using Lokad.ScratchSpace.Blocks;
using System;
using System.Runtime.CompilerServices;

namespace Lokad.ScratchSpace.Indexing
{
    /// <summary> An index of all available blocks. </summary>
    /// <remarks>
    ///     This class performs a simple mapping from (realm,hash) to block address. 
    ///     It does not keep track of the status or properties of the block found 
    ///     at that address. 
    ///     
    ///     The class is optimized for serving many reads, from many different 
    ///     threads, while also performing a significantly smaller number of writes
    ///     from many threads as well.
    /// </remarks>
    public sealed class BlockIndex
    {
        /// <summary>
        ///     Indicates the equivalent of a 'null pointer' in our linked 
        ///     lists.
        /// </summary>
        private const int NoEntry = -1;

        /// <summary> All entries (and buckets) in the hash table. </summary>
        /// <remarks>
        ///     To access the elements in bucket N: 
        ///     
        ///      - The first entry M = `_entries[N].FirstInBucket` (we hope that this will,
        ///        most of the time, be equal to N itself).
        ///      - The second entry is `_entries[M].NextInBucket`, and so on.
        ///      - The list ends when the entry is '-1'.
        /// </remarks>
        private readonly IndexEntry[] _entries = new IndexEntry[IndexEntry.EntryKey.BucketCount];

        /// <summary> The mirror of 'NextInBucket', to create a doubly linked list. </summary>
        /// <remarks>
        ///     This is only used for writing to the block index (whereas most requests are 
        ///     read requests), and so it is kept separate from the entries list in order not
        ///     to pollute the processor cache with useless data when reading.
        /// </remarks>
        private readonly int[] _prevInBucket = new int[IndexEntry.EntryKey.BucketCount];

        /// <summary> The oldest element in the free list. </summary>
        /// <remarks> 
        ///     This is a doubly linked list that uses <see cref="IndexEntry.NextInBucket"/>
        ///     and <see cref="_prevInBucket"/>.    
        /// </remarks>
        private int _oldestFree;

        /// <summary> The youngest element in the free list. </summary>
        private int _youngestFree;

        /// <summary> The number of blocks in the index. </summary>
        public int Count { get; private set; }

        /// <summary>
        ///     Used to protect access to the doubly linked lists inside the
        ///     buckets and the free list.
        /// </summary>
        private readonly object _syncRoot = new object();

        public BlockIndex()
        {
            // Create the free list. This also has the effect of actually requesting the 
            // memory pages from the OS (as opposed to just allocating them) by writing 
            // values there.

            for (var i = 1; i < IndexEntry.EntryKey.BucketCount; ++i)
            {
                _entries[i - 1].NextInBucket = i;
                _entries[i - 1].FirstInBucket = NoEntry;
                _prevInBucket[i] = i - 1;
            }

            _oldestFree = 0;
            _prevInBucket[0] = NoEntry;
            _entries[IndexEntry.EntryKey.BucketCount - 1].NextInBucket = NoEntry;
            _entries[IndexEntry.EntryKey.BucketCount - 1].FirstInBucket = NoEntry;
            _youngestFree = IndexEntry.EntryKey.BucketCount - 1;
        }

        /// <summary> Dequeue the oldest free element from the free list. </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int DequeueOldestFree()
        {
            if (_oldestFree == NoEntry)
                throw new InvalidOperationException("No free index entry available.");

            var dequeued = _oldestFree;

            _oldestFree = _entries[_oldestFree].NextInBucket;

            if (_oldestFree == NoEntry)
            {
                _youngestFree = NoEntry;
            }
            else
            {
                _prevInBucket[_oldestFree] = NoEntry;
            }

            return dequeued;
        }

        /// <summary> Remove an element from the free list. </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FromFreeList(int pos)
        {
            var prev = _prevInBucket[pos];
            var next = _entries[pos].NextInBucket;

            if (prev == NoEntry)
            {
                _oldestFree = next;
            }
            else
            {
                _entries[prev].NextInBucket = next;
            }

            if (next == NoEntry)
            {
                _youngestFree = prev;
            }
            else
            {
                _prevInBucket[next] = prev;
            }

            return pos;
        }

        /// <summary>
        ///     Find a cache-friendly entry (one that is close to values that would
        ///     have already been read so far by the read method, and so would
        ///     not require an additional read from RAM).
        /// </summary>
        /// <param name="bucket">
        ///     The bucket into which we want to add the value.
        /// </param>
        /// <param name="last">
        ///     The position of the last entry in the linked list of the bucket.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindCacheFriendlyEntry(int bucket, int last)
        {
            if (last == NoEntry) // The bucket is currently empty
            {
                if (_entries[bucket].Address.IsNone())
                {
                    // Can use the bucket cell itself to store the entry. This is
                    // nearly optimal: the read of '.FirstInBucket' will have loaded 
                    // the entry into the L1 cache which means every read access 
                    // needs one less read from RAM.
                    return bucket;
                }

                // If the bucket itself cannot be used, try the few following
                // entries, as it is also likely they have been loaded into the
                // L1 cache (as part of a 64-byte cache line).
                if (bucket + 4 <= IndexEntry.EntryKey.BucketCount)
                {
                    for (var i = 1; i < 4; ++i)
                    {
                        if (_entries[bucket + i].Address.IsNone())
                        {
                            return bucket + i;
                        }
                    }
                }
            }
            else
            {
                // The bucket is not empty. Reading through the values has 
                // reached the entry at position 'last', which means the following
                // few entries are likely in the L1 cache as well. 
                if (last + 4 <= IndexEntry.EntryKey.BucketCount)
                {
                    for (var i = 1; i < 4; ++i)
                    {
                        if (_entries[bucket + i].Address.IsNone())
                        {
                            return bucket + i;
                        }
                    }
                }
            }

            return NoEntry;
        }

        /// <summary> Add a new block to the index. </summary>
        /// <remarks> 
        ///     If the block already exists, overwrites its address and returns
        ///     false, otherwise returns true.
        /// </remarks>
        public bool Add(uint realm, Hash hash, BlockAddress address)
        {
            if (address.IsNone())
                throw new ArgumentException("'None' address provided.", nameof(address));

            var key = new IndexEntry.EntryKey(hash, realm);
            var bucket = IndexEntry.EntryKey.BucketOfHash(hash);

            lock (_syncRoot) // All write operations require locking.
            {
                // Traverse the bucket, looking for an entry with the same key.
                // If not found, will remember 'last', the last entry in the 
                // sequence.
                var prev = NoEntry;
                var search = _entries[bucket].FirstInBucket;

                while (search != NoEntry)
                {
                    ref var entry = ref _entries[search];
                    if (entry.Key.Equals(key))
                    {
                        // The (realm,hash) already exists in the index: just overwrite 
                        // the address with the new one. 
                        _entries[search].Address = address;
                        return false;
                    }

                    prev = search;
                    search = entry.NextInBucket;
                }

                var cacheFriendlyPos = FindCacheFriendlyEntry(bucket, prev);

                // If found no cache-friendly position to place the entry,
                // pull an arbitrary position from the free list.

                var free = cacheFriendlyPos == NoEntry
                    ? DequeueOldestFree()
                    : FromFreeList(cacheFriendlyPos);

                // Initialize the entry at 'pos' to contain the key-address 
                // pair, then insert the entry into the bucket's list right
                // after element 'prev'.
                ref var inserted = ref _entries[free];

                inserted.Key = key;
                inserted.Address = address;

                int next;
                if (prev == NoEntry)
                {
                    ref var ptr = ref _entries[bucket].FirstInBucket;
                    next = ptr;
                    ptr = free;
                }
                else
                {
                    ref var ptr = ref _entries[prev].NextInBucket;
                    next = ptr;
                    ptr = free;
                }

                if (next != NoEntry)
                    _prevInBucket[next] = free;

                _prevInBucket[free] = prev;
                inserted.NextInBucket = next;

                ++Count;

                return true;
            }
        }

        /// <summary> Remove a block from the index if it has a specified address. </summary>
        public void Remove(uint realm, Hash hash, BlockAddress addr)
        {
            var key = new IndexEntry.EntryKey(hash, realm);
            var bucket = IndexEntry.EntryKey.BucketOfHash(hash);

            lock (_syncRoot)
            {
                var prev = -1;
                var current = _entries[bucket].FirstInBucket;

                while (current != NoEntry)
                {
                    ref var entry = ref _entries[current];
                    if (entry.Key.Equals(key))
                    {
                        if (!entry.Address.Equals(addr))
                            // Present, but with another address
                            return;

                        entry.Address = default;
                        entry.Key = default;

                        // Remove from current doubly linked list

                        var next = entry.NextInBucket;
                        if (prev == NoEntry)
                        {
                            _entries[bucket].FirstInBucket = next;
                        }
                        else
                        {
                            _entries[prev].NextInBucket = next;
                        }

                        if (next != NoEntry)
                        {
                            _prevInBucket[next] = prev;
                        }

                        // Insert at the end of the free list.

                        entry.NextInBucket = NoEntry;
                        _prevInBucket[current] = _youngestFree;
                        if (_youngestFree == NoEntry)
                        {
                            _oldestFree = current;
                        }
                        else
                        {
                            _entries[_youngestFree].NextInBucket = current;
                        }
                        _youngestFree = current;

                        --Count;

                        return;
                    }

                    prev = current;
                    current = entry.NextInBucket;
                }

                // Not present in index
                return;
            }
        }

        /// <summary>
        ///     Return the address associated with a (realm, hash) pair,
        ///     or <see cref="BlockAddress.None"/> if none.
        /// </summary>
        /// <remarks>
        ///     Reading does not require locks, and can be done in parallel.
        ///     
        ///     Most of the time, we return the correct address (or no address
        ///     if the pair is not in the index).
        ///     
        ///     Rarely, a race condition leads us to return a None address 
        ///     for a pair that is in the index (case where `entry.Key` has
        ///     been set but `entry.Address` has not). This is an acceptable
        ///     mistake, if rare enough.
        ///     
        ///     Very rarely, we return the wrong address for the pair (but 
        ///     this requires one hell of a race, since all entries pass 
        ///     through the free list before being re-assigned, which means
        ///     they should have contained all-zeros at some point). Still,
        ///     an invalid address is something that the file side of things
        ///     expects and is able to deal with (because addresses may not
        ///     match what's in the file anymore) so there's no danger in
        ///     that.
        /// </remarks>
        public BlockAddress Get(uint realm, Hash hash)
        {
            var key = new IndexEntry.EntryKey(hash, realm);
            var bucket = IndexEntry.EntryKey.BucketOfHash(hash);

            var current = _entries[bucket].FirstInBucket;

            while (current != NoEntry)
            {
                ref var entry = ref _entries[current];
                if (entry.Key.Equals(key)) return entry.Address;
                current = entry.NextInBucket;
            }

            return default;
        }
    }
}
