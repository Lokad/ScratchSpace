using System;
using System.Collections.Generic;
using System.Diagnostics;
using Lokad.ContentAddr;
using Lokad.ScratchSpace.Blocks;
using Lokad.ScratchSpace.Helpers;
using Lokad.ScratchSpace.Mapping;

namespace Lokad.ScratchSpace.Files
{
    /// <summary>
    ///     This class represents metadata about a memory-mapped file that 
    ///     contains blocks. Only read-only access is permitted.
    /// </summary>
    public sealed class BlockFile : IDisposable
    {
        /// <summary> Used to pin the file every time a block is accessed. </summary>
        private readonly Pinner _pinner = new Pinner();

        /// <summary> A memory-mapper containing the data for the file. </summary>
        private readonly IFileMemory _file;

        /// <summary> Read flags for all the blocks in the data. </summary>
        /// <remarks>
        ///     This is used when reloading from disk the data from a previous instance,
        ///     in order to validate the hash of the data the first time the data is
        ///     requested.
        ///     
        ///     It is also used when the file is a read-only view over a file being
        ///     written out, in which case the flags allow the reader thread to 
        ///     trigger the write earlier than the write thread itself.
        /// </remarks>
        private readonly AppendList<ReadFlag> _flags;

        /// <summary> A callback to call once the pin count has reached zero. </summary>
        /// <remarks>
        ///     Since it is not possible to close (and delete) a file while there are
        ///     pointers to its contents, the <see cref="_pinner"/> is used to keep 
        ///     track of active pointers. Once the file should be removed, the removal
        ///     callback is provided and will be called as soon as the pin count 
        ///     reaches zero.
        /// </remarks>
        private Action _removalCallback;

        /// <summary> Construct from raw building blocks. </summary>
        /// <remarks> 
        ///     Used to create a read-only view over a file that is being written to.
        ///     Note that <see cref="_flags"/> may grow over time, as new blocks are 
        ///     appended.
        /// </remarks>
        internal BlockFile(
            IFileMemory file,
            AppendList<ReadFlag> flags,
            uint fileId)
        {
            _file = file ?? throw new ArgumentNullException(nameof(file));
            _flags = flags ?? throw new ArgumentNullException(nameof(flags));
            FileId = fileId;
        }

        /// <summary> Construct from a read-only file. </summary>
        /// <remarks>
        ///     Used when recovering files written by a previous program 
        ///     instance from their locations.
        /// </remarks>
        public BlockFile(IFileMemory file, uint fileId)
        {
            _pinner = new Pinner();
            _file = file ?? throw new ArgumentNullException(nameof(file));
            FileId = fileId;

            _flags = new AppendList<ReadFlag>();

            var offset = 0L;
            while (offset < _file.Length)
            {
                var block = AtOffset(offset);
                ref var header = ref block.Header;

                if (header.Rank != _flags.Count || header.ContentLength < 0 ||
                    header.ContentLength + BlockHeader.Size > _file.Length)
                {
                    // The header of the block appears broken. Do not include
                    // it in the list, and stop scanning (since we don't know
                    // how far to jump ahead).
                    break; 
                }

                var thisOffset = offset;
                _flags.Append(ReadFlag.Triggered(() => VerifyAtOffset(thisOffset)));

                offset += block.RelativeOffsetToNextBlock;
            }
        }

        /// <summary>
        ///     The internal file identifier, as expressed by the
        ///     <see cref="BlockAddress.File"/>
        /// </summary>
        public uint FileId { get; }

        /// <summary>
        ///     The number of pins currently applied to any blocks inside 
        ///     this file.
        /// </summary>
        public int PinCount => _pinner.PinCount;

        /// <summary> The block present at the specified offset. </summary>
        private Block AtOffset(long offset)
        {
            var mem = _file.AsMemory(
                    offset,
                    (int)Math.Min(int.MaxValue, _file.Length - offset));

            return new Block(mem.Span);
        }

        /// <summary>
        ///     Verifies that the block at the specified offset has the correct hash,
        ///     throws an exception if it doesn't.
        /// </summary>
        private void VerifyAtOffset(long offset)
        {
            var block = AtOffset(offset);
            var hash = BlockHasher.ComputeHash(block.Contents);

            if (!hash.Equals(block.Header.Hash))
                throw new InvalidHashException(FileId, offset, hash, block.Header.Hash);
        }

        /// <summary> Enumerate all blocks in this file. </summary>
        public IEnumerable<(uint realm, Hash hash, BlockAddress address)> EnumerateBlocks()
        {
            var offset = 0L;
            for (var i = 0; i < _flags.Count; ++i)
                yield return TupleOfBlock(FileId, ref offset);

            /// Need a separate function because Block/Span are not allowed in a 
            /// yield-return functoin.
            (uint realm, Hash hash, BlockAddress address) TupleOfBlock(
                uint fileId,
                ref long off)
            {
                var block = AtOffset(off);
                var header = block.Header;

                var addr = new BlockAddress(fileId, off);

                off += block.RelativeOffsetToNextBlock;

                return (header.Realm, header.Hash, addr);
            }
        }

        /// <summary>
        ///     Calls <paramref name="onBlock"/> on the contents of the block at 
        ///     address <paramref name="address"/>. 
        /// </summary>
        /// <remarks>
        ///     If the block has pending work (such as validating contents, or 
        ///     writing them out), that pending work is performed before the call.
        ///     
        ///     Fully thread-safe.
        ///     
        ///     Function <paramref name="onBlock"/> should endeavour to return 
        ///     quickly, as the provided span is pinned, for all intents and 
        ///     purposes, until it does.
        /// </remarks>
        /// <returns>
        ///     True of the block could be extracted and passed to <paramref name="onBlock"/>.
        ///     This may fail if the file is being unpinned, or if the block does not 
        ///     have the expected realm/hash, or if it is corrupted, or if the address has
        ///     somehow expired.
        ///     
        ///     The out argument contains the return value of <paramref name="onBlock"/>.
        /// </returns>
        public bool TryWithBlockAtAddress<T>(
            BlockAddress address,
            uint realm, 
            Hash hash,
            WithSpan.ReadOnlyReturns<T> onBlock,
            out T result)
        {
            if (address.File() != FileId)
                throw new ArgumentException(
                    $"Address in file {address.File()} not present in file {FileId}",
                    nameof(address));

            if (!_pinner.TryPin())
            {
                result = default;
                return false;
            }

            try
            {
                // Basic sanity checks (we cannot trust that 'address' points to  
                // a valid location inside the file).

                var offset = address.FirstByteOffset();

                if (offset + BlockHeader.Size > _file.Length)
                {
                    result = default;
                    return false;
                }

                var block = AtOffset(offset);
                ref var header = ref block.Header;

                if (header.Realm != realm || !header.Hash.Equals(hash) ||
                    header.Rank < 0 || header.Rank >= _flags.Count ||
                    header.ContentLength < 0)
                {
                    result = default;
                    return false;
                }

                ref var flag = ref _flags[header.Rank];

                try
                {
                    // Don't worry about multi-threading: `WaitUntilReadable` guarantees
                    // that the returned flag has the same behaviour as the original flag,
                    // except it may be faster and/or use less memory, so data races do
                    // not change anything.

                    flag = flag.WaitUntilReadable();
                }
                catch
                {
                    // TODO: log the exception.
                    result = default;
                    return false;
                }

                result = onBlock(block.Contents);
                return true;
            }
            finally
            {
                if (_pinner.Unpin())
                    _removalCallback();
            }
        }

        /// <summary> Request the removal of this file. </summary>
        /// <remarks>
        ///     <see cref="TryWithBlockAtAddress"/> will start returning false. 
        ///     Once the <see cref="PinCount"/> reaches zero, the provided 
        ///     callback is invoked.
        /// </remarks>
        public void RequestRemoval(Action callback)
        {
            // Assign this *before* calling MakeUnpinnable: if there was only one pin,
            // and it is unpinned immediately after MakeUnpinnable, then the unpinner
            // thread must see the proper _removalCallback.

            _removalCallback = callback;

            if (_pinner.MakeUnpinnable())
                callback();
        }

        public void Dispose()
        {
            Debug.Assert(PinCount == 0);
            _file.Dispose();
        }
    }

    /// <summary> Thrown when a block does not have the advertised hash. </summary>
    public class InvalidHashException : Exception
    {
        public InvalidHashException(uint fileId, long offset, Hash realHash, Hash expectedHash)
            : base($"In file {fileId} offset {offset}, expected hash {expectedHash} but found {realHash}.")
        {
        }
    }
}
