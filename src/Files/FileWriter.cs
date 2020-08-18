using Lokad.ContentAddr;
using Lokad.ScratchSpace.Blocks;
using Lokad.ScratchSpace.Helpers;
using Lokad.ScratchSpace.Mapping;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Lokad.ScratchSpace.Files
{
    /// <summary> Writes blocks to a new file. </summary>
    public sealed class FileWriter
    {
        /// <summary> The memory-mapped file where this writer appends blocks. </summary>
        private readonly IFileMemory _file;

        /// <summary> A read-flag for every write appended to this file. </summary>
        /// <remarks> 
        ///     Appending to the file does not immediately copy the bytes to the 
        ///     memory-mapped range ; instead, a read flag is created so that the 
        ///     bytes are copied either by the background thread of the writer,
        ///     or by the first reader.
        /// </remarks>
        private readonly AppendList<ReadFlag> _flags;

        /// <summary>
        ///     File identifier, used to created a <see cref="BlockAddress"/> for
        ///     each write.
        /// </summary>
        public uint FileId { get; }

        /// <summary> The current offset inside the file. </summary>
        /// <remarks> The next block should be allocated there. </remarks>
        private long _offset = 0;

        /// <summary> The offset up to which data has been flushed to file. </summary>
        private long _flushOffset = 0;

        /// <summary>
        ///     To synchronize access to <see cref="_flags"/> and
        ///     <see cref="_offset"/>.
        /// </summary>
        private readonly object _syncRoot = new object();

        private FileWriter(IFileMemory file, AppendList<ReadFlag> flags, uint fileId)
        {
            _file = file;
            _flags = flags;
            FileId = fileId;
        }

        /// <summary>
        ///     Flush the contents of the file to disk, and prevent any further 
        ///     writes. From this point on, the file becomes officially read-only.
        /// </summary>
        public void FlushAndClose()
        {
            lock (_syncRoot)
                // First, prevent additional writes.
                _offset = _file.Length;

            Flush(fast: false);
        }

        /// <summary>
        ///     Attempts to schedule a write of the specified length into the file.
        /// </summary>
        /// <returns>
        ///     A valid block address representing the location where the written 
        ///     block will be found, or a 'None' address if the file does not 
        ///     currently have enough room for this. 
        /// </returns>
        /// <remarks>
        ///     The provided writer will be invoked at some point in the future. 
        ///     
        ///     Thread-safe.
        /// </remarks>
        public BlockAddress TryScheduleWrite(
            uint realm,
            Hash hash,
            int length,
            WithSpan.ReadWrite writer)
        {
            if (length < 0)
                throw new ArgumentException($"Negative length: {length}", nameof(length));
            int rank;
            long offset;

            lock (_syncRoot)
            {
                offset = _offset;
                _offset = BlockAddress.Ceiling(offset + BlockHeader.Size + length);

                if (_offset > _file.Length)
                {
                    // Since this file is about to be flushed and closed, 
                    // because it's "full enough", we prevent additional writes.
                    _offset = _file.Length;
                    return BlockAddress.None;
                }

                rank = _flags.Count;
                _flags.Append(ReadFlag.Triggered(() => PerformWrite(offset, writer)));
            }

            // We touched '_offset' and '_flags' in the critical section, the remaining
            // work on the actual byte span can be done without mutex.

            var span = _file.AsMemory(offset, BlockHeader.Size).Span;
            ref var header = ref MemoryMarshal.Cast<byte, BlockHeader>(span)[0];

            header.Hash = hash;
            header.Realm = realm;
            header.Rank = rank;
            header.ContentLength = length;

            // The question is: is the calling thread responsible for copying the bytes
            // to the file, or do we delegate that to the first reader, or the 
            // FileWheel background thread, whichever comes first ?
            //
            // By delegating, the calling thread can be released faster to do some 
            // other stuff, which can be interesting if writes come in bursts (the 
            // background thread can then manage all the writes on its own during a 
            // period of low write activity) ; by making the calling thread perform
            // the write, overall performance is better when writes are consistently
            // higher than a single thread can manage.
            
#pragma warning disable CS0162 // Unreachable code detected
            const bool callerShouldWrite = false;
            if (callerShouldWrite)
                _flags[rank].WaitUntilReadable();
#pragma warning restore CS0162 // Unreachable code detected

            return new BlockAddress(FileId, offset);
        }

        /// <summary> Flush all writes performed since the last flush. </summary>
        /// <remarks> 
        ///     If <paramref name="fast"/> is true, will only flush a small 
        ///     amount of memory, to avoid blocking for too long (less than 0.1s).
        /// </remarks>
        public bool Flush(bool fast)
        {
            // To be consistent with each other, the values need to be read
            // as part of a lock.
            int flagsCount;
            long offset;
            lock (_syncRoot)
            {
                flagsCount = _flags.Count;
                offset = _offset;
            }

            if (_flushOffset >= offset)
                return false;

            // Traverse `_flags` to perform any writes that have not 
            // been performed by the readers yet. 
            for (var i = 0; i < flagsCount; ++i)
            {
                ref var flag = ref _flags[i];
                try
                {
                    flag = flag.WaitUntilReadable();
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"Flush: {e.Message}");
                    // Ignore the failure, it will be re-triggered if someone
                    // tries to read the value.
                }
            }

            var todo = offset - _flushOffset;

            const long fastFlushSize = 1 << 21;
            if (fast) todo = Math.Min(todo, fastFlushSize);

            // Now, all the data is in the memory-mapped region. 
            _file.Flush(_flushOffset, todo);
            
            _flushOffset = offset + todo;

            return true;
        }

        /// <summary>
        ///     Invokes the writer function on the byte range reserved for that
        ///     write. Should be performed before the data is read.
        /// </summary>
        private void PerformWrite(long offset, WithSpan.ReadWrite writer)
        {
            var mem = _file.AsMemory(
                    offset,
                    (int)Math.Min(int.MaxValue, _file.Length - offset));

            var block = new Block(mem.Span);

            writer(block.Contents);
        }

        /// <summary>
        ///     Creates a file writer, and a <see cref="BlockFile"/> that can immediately
        ///     give access to blocks written through the writer.
        /// </summary>
        public static (BlockFile, FileWriter) CreateReaderWriterPair(
            IFileMemory file,
            uint fileId)
        {
            var flags = new AppendList<ReadFlag>();

            return (new BlockFile(file, flags, fileId),
                    new FileWriter(file, flags, fileId));
        }
    }
}
