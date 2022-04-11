using Lokad.ContentAddr;
using Lokad.ScratchSpace.Blocks;
using Lokad.ScratchSpace.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Lokad.ScratchSpace;
using Lokad.ScratchSpace.Mapping;

namespace Lokad.ScratchSpace.Files
{
    /// <summary> Rotates through files. </summary>
    public sealed class FileWheel : IDisposable
    {
        /// <summary> All files available for reading. </summary>
        /// <remarks> Individual files will be null until allocated. </remarks>
        private readonly BlockFile[] _readFiles;

        /// <summary> Used to allocate files on the disk. </summary>
        private readonly IFileSource _fs;

        /// <summary> Holds the currently active writer. </summary>
        private readonly BackgroundRecycler<FileWriter> _writer =
            new BackgroundRecycler<FileWriter>();

        /// <summary>
        ///     Triggered whenever a block is about to be discarded (because its
        ///     containing file is being deleted).
        /// </summary>
        private Action<uint, Hash, BlockAddress> _onDeletion;

        /// <summary>
        ///     The position (in <see cref="_readFiles"/>) of the next file that 
        ///     will be deleted and re-created.
        /// </summary>
        /// <remarks>
        ///     This could in theory be a local variable in <see cref="BackgroundThread"/>,
        ///     but it's easier to debug the value if it's a private field.
        ///     
        ///     This setting is not persisted (if the application is shut down and
        ///     restarted, it will be forgotten).
        /// </remarks>
        private int _nextAlloc;

        public FileWheel(
            IFileSource dfs,
            Action<uint, Hash, BlockAddress> onDeletion)
        {
            _onDeletion = onDeletion ?? throw new ArgumentNullException(nameof(onDeletion));
            _fs = dfs ?? throw new ArgumentNullException(nameof(dfs));
            _readFiles = new BlockFile[dfs.Count];
            // Discover any on-disk files. 
            foreach (var (i, mmap) in dfs.ScanExistingFiles())
            {
                if (i >= 2)
                {
                    _readFiles[i] = new BlockFile(mmap, (uint)(i + 1));
                }
                else
                {
                    mmap.Dispose();
                }
            }
            // The first two files are always reserved for the first two writers.
            ReplaceFile(0);
            ReplaceFile(1);
            _nextAlloc = 2;
        }

        /// <summary> Enumerate all blocks in all files in this wheel. </summary>
        public IEnumerable<(uint realm, Hash hash, BlockAddress address)> EnumerateBlocks(CancellationToken cancel)
        {
            for (var i = _nextAlloc; i < _readFiles.Length; i++)
            {
                var f = _readFiles[i];
                if (f == null) continue;

                foreach (var tup in f.DiscoverBlocks())
                    yield return tup;
                
                if (cancel.IsCancellationRequested)
                    break;
            }
        }

        /// <summary>
        ///     Start the background thread that runs <see cref="BackgroundThread"/>
        /// </summary>
        public void StartBackgroundThread(CancellationToken cancel)
        {
            var thread = new Thread(() => BackgroundThread(cancel))
            {
                Name = "FileWheel.BackgroundThread"
            };

            thread.Start();
        }

        /// <summary>
        ///     The background thread of the file wheel. Flushes old writers and
        ///     creates new ones.
        /// </summary>
        private void BackgroundThread(CancellationToken cancel)
        {
            while (!cancel.IsCancellationRequested)
            {
                try
                {
                    var flushed = false;

                    if (_writer.CurrentIfExists is FileWriter current)
                        // Don't let the flush cursor fall too far behind the
                        // write cursor. However, the final flush is always 
                        // handled when the writer is recycled.
                        flushed = current.Flush(fast: true);

                    if (_writer.TryNextToBeRecycled(
                        flushed ? TimeSpan.Zero : TimeSpan.FromSeconds(1),
                        cancel, 
                        out var toBeFlushed))
                    {
                        // Perform the final flush in a separate thread: this can 
                        // take a while (up to 60 seconds) if there's a lot to 
                        // flush, so a thread creation is amortized.
                        new Thread(toBeFlushed.FlushAndClose){ Name = "FileWheel.Flusher" }
                            .Start();

                        var nextPos = _nextAlloc;
                        _nextAlloc = (_nextAlloc + 1) % _readFiles.Length;

                        if (_readFiles[nextPos] == null)
                        {
                            ReplaceFile(nextPos);
                        }
                        else
                        {
                            _readFiles[nextPos].RequestRemoval(() => ReplaceFile(nextPos));
                        }
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"Background thread: {e.Message}");
                }
            }
        }

        public void Dispose()
        {
            foreach (var f in _readFiles)
                f?.Dispose();
        }

        /// <summary> 
        ///     Erase the file at position `pos` and replace it with a 
        ///     brand new, empty file ; enqueue the writer for that empty
        ///     file into the new writers queue.
        /// </summary>
        private void ReplaceFile(int pos)
        {
            // Notify that files are about to be destroyed
            if (_readFiles[pos] is BlockFile bf)
            {
                foreach (var (r, h, a) in bf.EnumerateBlocks())
                {
                    try 
                    {
                        _onDeletion(r, h, a);
                    }
                    catch
                    {
                        // Don't let exceptions prevent the actual file replacement.
                        // TODO: log this exception
                    }
                }

                // Disposing will release the memory-map, and allow 
                // "DeleteAndCreate" to erase the underlying file.
                bf.Dispose();
            }

            var mma = _fs.DeleteAndCreate(pos);
            var (read, write) = FileWriter.CreateReaderWriterPair(
                mma, (uint)(pos + 1));

            _readFiles[pos] = read;
            _writer.CompleteRecycle(write);
        }

        /// <summary>
        ///     Schedule the writing of a block, returns the address where the block will be
        ///     available once written.
        /// </summary>
        /// <remarks>
        ///     It is of course possible to immediately query the data at that address, in 
        ///     which case the data will be returned. 
        ///     
        ///     Can be called from multiple threads.
        /// </remarks>
        public BlockAddress ScheduleWrite(
            uint realm, 
            Hash hash,
            int length,
            WithSpan.ReadWrite writer)
        {
            for (var retries = 0; retries < 3; ++retries)
            {
                var currentWriter = _writer.GetCurrent();
                var addr = currentWriter.TryScheduleWrite(realm, hash, length, writer);
                if (!addr.IsNone())
                    return addr;

                // Scheduling a write failed because the current writer is full,
                // so close it and loop to get a new writer.
                _writer.RequestRecycle(currentWriter);
            }

            throw new Exception("Could not schedule write after three retries");
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
        /// <see cref="BlockFile.TryWithBlockAtAddress"/>
        public bool TryWithBlockAtAddress<T>(
            BlockAddress address,
            uint realm,
            Hash hash,
            WithSpan.ReadOnlyReturns<T> onBlock,
            out T result)
        {
            if (address.IsNone())
            {
                result = default;
                return false;
            }

            // BlockAddress.File() is 1..1023 but our array is zero-indexed.
            var fid = address.File() - 1;
            var file = _readFiles[fid];

            if (file == null)
            {
                result = default;
                return false;
            }

            return file.TryWithBlockAtAddress(
                address,
                realm,
                hash,
                onBlock,
                out result);
        }
    }
}
