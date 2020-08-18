using Lokad.ContentAddr;
using Lokad.ScratchSpace.Files;
using Lokad.ScratchSpace.Indexing;
using System;
using System.Threading;

namespace Lokad.ScratchSpace
{
    /// <summary> A scratch space, backed by memory-mapped files. </summary>
    public sealed class Scratch : IDisposable
    {
        /// <summary> An index of all block addresses, by (realm,hash). </summary>
        private readonly BlockIndex _index;

        /// <summary> All files where the addressed blocks are found. </summary>
        private readonly FileWheel _wheel;

        public Scratch(IFileSource source, CancellationToken cancel)
        {
            _index = new BlockIndex();

            // When a (realm,hash,address) is about to be deleted, remove it from
            // the index as well.
            _wheel = new FileWheel(source, _index.Remove);
            
            // Discovered anything on disk ? Register them with the index.
            foreach (var (realm, hash, address) in _wheel.EnumerateBlocks())
                _index.Add(realm, hash, address);
            
            _wheel.StartBackgroundThread(cancel);
        }

        /// <summary> The number of referenced blocks.</summary>
        public int Count => _index.Count;

        /// <summary>
        ///     Write a block to the scratch space. It will be possible to read it
        ///     back from the scratch space using the (realm, hash) that was 
        ///     provided to create it.
        /// </summary>
        /// <remarks>
        ///     <paramref name="hash"/> should be the hash of the data passed in,
        ///     according to <see cref="BlockHasher"/>.
        ///     
        ///     <paramref name="writer"/> will be called at some point in the future,
        ///     but not necessarily while <see cref="Write"/> is running.
        /// </remarks>
        public void Write(uint realm, Hash hash, int length, WithSpan.ReadWrite writer)
        {
            var addr = _wheel.ScheduleWrite(realm, hash, length, writer);
            _index.Add(realm, hash, addr);
        }

        /// <summary>
        ///     Calls <paramref name="reader"/> on the block represented by the 
        ///     realm and hash.
        /// </summary>
        /// <exception cref="MissingBlockException">
        ///     If the block is not present in the scratch space, or was corrupted
        ///     and failed a checksum check.
        /// </exception>
        public T Read<T>(uint realm, Hash hash, WithSpan.ReadOnlyReturns<T> reader)
        {
            var addr = _index.Get(realm, hash);
            try
            {
                if (_wheel.TryWithBlockAtAddress(addr, realm, hash, reader, out var result))
                    return result;
            }
            // 'MissingBlock' and 'ChecksumFailed', if they happen inside, should 
            // converted to a 'MissingBlock' and ensure that the broken block is
            // removed.
            catch (MissingBlockException) {}
            catch (CheckSumFailedException) {}

            _index.Remove(realm, hash, addr);
            throw new MissingBlockException(realm, hash);
        }

        /// <summary>
        ///     True if this scratch space contains data for the specified block.
        /// </summary>
        public bool ContainsKey(uint realm, Hash hash) =>
            !_index.Get(realm, hash).IsNone();

        /// <summary>
        ///     Calls <paramref name="reader"/> on the block represented by the 
        ///     realm and hash.
        /// </summary>
        /// <exception cref="MissingBlockException">
        ///     If the block is not present in the scratch space, or was corrupted
        ///     and failed a checksum check.
        /// </exception>
        public T Read<T>(uint realm, Hash hash, WithSpan.WithReader<T> reader) =>
            Read(realm, hash, span => reader(new BlittableReader(span)));

        /// <summary>
        ///     Remove the current data associated to a hash.
        /// </summary>
        public void Remove(uint realm, Hash hash) =>
            _index.Remove(realm, hash, _index.Get(realm, hash));

        /// <summary>
        ///     Create a writer that will write to the specified realm when 
        ///     its <see cref="BlittableWriter.Commit"/> method is called.
        /// </summary>
        public BlittableWriter Write(uint realm) =>
            new BlittableWriter(this, realm);

        public void Dispose() =>
            _wheel.Dispose();
    }
}
