using Lokad.ContentAddr;
using SpookilySharp;
using System;
using System.Security;

namespace Lokad.ScratchSpace.Blocks
{
    /// <summary> 
    ///     A hash function to hash block, represented as a stateful object. 
    ///     Call <see cref="Create"/> to create a new one, call <see cref="Update"/>
    ///     repeatedly on the data spans to hash.
    /// </summary>
    /// <remarks> Uses SpookyHash for performance and lack of collisions. </remarks>
    public struct BlockHasher
    {
        private readonly SpookyHash _hasher;

        private BlockHasher(SpookyHash hasher)
        {
            _hasher = hasher;
        }

        public static BlockHasher Create() => new BlockHasher(new SpookyHash());

        public unsafe void Update(ReadOnlySpan<byte> span)
        {
            if (span.Length == 0)
                return;

            fixed (byte* ptr = &span.GetPinnableReference())
            {
                _hasher.Update(ptr, span.Length * sizeof(byte));
            }
        }

        /// <summary>
        ///     Return the resulting hash of all data passed to <see cref="Update"/>
        ///     so far.
        /// </summary>
        public Hash Final()
        {
            _hasher.Final(out ulong left, out ulong right);
            return new Hash(left, right);
        }

        /// <summary>
        ///     Convenience function to compute the hash of a single span of bytes.
        /// </summary>
        public static Hash ComputeHash(ReadOnlySpan<byte> span)
        {
            var hasher = Create();
            hasher.Update(span);
            return hasher.Final();
        }
    }
}
