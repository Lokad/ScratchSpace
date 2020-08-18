using Lokad.ContentAddr;
using Lokad.ScratchSpace.Blocks;
using Lokad.ScratchSpace.Files;
using Lokad.ScratchSpace.Writing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Lokad.ScratchSpace
{
    /// <summary>
    ///     Used to write to the scratch space (in a specific realm). 
    ///     The data is added with the various <c>Write</c> functions, 
    ///     then committed to the scratch space with <see cref="Commit"/>,
    ///     which returns the hash.
    /// </summary>
    public sealed class BlittableWriter
    {
        /// <summary>
        ///     All blocks that were requested to be written so far.
        /// </summary>
        private readonly Queue<IReqWrite> _requestQueue = new Queue<IReqWrite>();

        /// <summary> The checksum state in the current checksum region. </summary>
        private uint _checksumSoFar = Checksum.Seed;

        /// <summary> The scratch space where this will be written. </summary>
        private readonly Scratch _scratch;

        /// <summary> The realm where this data belongs. </summary>
        private readonly uint _realm;

        /// <summary> Used to hash on-the-fly. </summary>
        private readonly BlockHasher _hasher;

        /// <summary> Has <see cref="Commit"/> already been called ? </summary>
        private bool _closed;

        /// <summary> 
        ///     The number of bytes added to <see cref="_requestQueue"/>. 
        ///     so far.
        /// </summary>
        public int TotalBytes { get; private set; }

        public BlittableWriter(Scratch scratch, uint realm)
        {
            _scratch = scratch;
            _realm = realm;
            _hasher = BlockHasher.Create();
        }

        /// <summary>
        ///     Write data to the writer. This should be called by users of the writers
        ///     (i.e. serialization protocols).
        /// </summary>
        public void Write(IReqWrite req)
        {
            if (_closed)
                throw new InvalidOperationException("Cannot write to committed writer.");

            _requestQueue.Enqueue(req);

            if (int.MaxValue - req.Size < TotalBytes + BlockHeader.Size)
                throw new ArgumentException("Writer requests size overflows max block serialization size.");

            TotalBytes += req.Size;
        }

        /// <summary>
        ///     Perform the pre-commit phase, computing the hash and checksums.
        /// </summary>
        private void PreCommit()
        {

            foreach (var req in _requestQueue)
                req.WithSpan(span =>
                {
                    _hasher.Update(span);
                    _checksumSoFar = Checksum.UpdateCRC32(span, _checksumSoFar);
                });
        }

        /// <summary> Write a value of type <typeparamref name="T"/>. </summary>
        public void Write<T>(T value) where T : unmanaged =>
            Write(new ScalarReqWrite<T>(value));

        /// <summary>
        ///     Write an array of type <typeparamref name="T"/>. Length should be
        ///     serialized separately.
        /// </summary>
        public void Write<T>(ReadOnlyMemory<T> value) where T : unmanaged =>
            Write(new MemoryReqWrite<T>(value));

        /// <summary>
        ///     Write an array of type <typeparamref name="T"/>. Length should be
        ///     serialized separately.
        /// </summary>
        public void Write<T>(T[] value) where T : unmanaged =>
            Write((ReadOnlyMemory<T>)value);

        /// <summary>
        ///     Write an array of type <typeparamref name="T"/>. Length should be
        ///     serialized separately.
        /// </summary>
        public void Write<T>(Memory<T> value) where T : unmanaged =>
            Write((ReadOnlyMemory<T>)value);

        /// <summary> Write a string as 4-byte length, then UTF-8 bytes. </summary>
        public void Write(string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            Write(bytes.Length);
            Write(bytes);
        }

        /// <summary> Copy the written data to a target area of memory. </summary>
        public void CopyTo(Span<byte> fullSpan)
        {
            if (!_closed)
            {
                PreCommit(); // To compute checksums
                _closed = true;
            }

            var offset = 0;
            foreach (var reqWrite in _requestQueue)
            {
                var size = reqWrite.Size;
                var span = fullSpan.Slice(offset, reqWrite.Size);
                offset += reqWrite.Size;
                reqWrite.BlitTo(span);
            }
        }

        /// <summary>
        ///     Commit the written data to the atom store, returns the size
        ///     and hash of the blob. Once this function has been called, 
        ///     do not call <see cref="Write"/> anymore.
        /// </summary>
        public Hash Commit()
        {
            if (!_closed)
            {
                PreCommit();
                _closed = true;
            }

            var hash = _hasher.Final();

            // This requests the write, but `CopyTo` will be called later
            // (possibly much later), which is why we prevent further writes
            // to this writer.
            _scratch.Write(_realm, hash, TotalBytes, CopyTo);
            
            return hash;
        }

        /// <summary> Start a region with a CheckSum </summary>
        public void StartCheckSumRegion() =>
            Write(new StartCheckSumReq(this));

        /// <summary>
        ///     Finish a region with a checksum, write the 4-byte checksum
        ///     of the region.
        /// </summary>
        public void EndCheckSumRegion() =>
            Write(new EndCheckSumReq(this));

        /// <summary> Write the full data to a file to help debugging. </summary>
        public void DumpToFile(string path)
        {
            var bytes = new byte[TotalBytes];
            CopyTo(bytes);
            using (var stream = new FileStream(path, FileMode.Create))
                stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary> Used to reset the checksum during the pre-commit phase. </summary>
        private sealed class StartCheckSumReq : IReqWrite
        {
            private readonly BlittableWriter _writer;

            public StartCheckSumReq(BlittableWriter writer) =>
                _writer = writer ?? throw new ArgumentNullException(nameof(writer));

            public int Size => 0;

            public void BlitTo(Span<byte> destination)
            {}

            public void WithSpan(WithSpan.ReadOnly onSpan) =>
                _writer._checksumSoFar = Checksum.Seed;
        }

        /// <summary>
        ///     Used to extract and remember the checksum during the pre-commit phase, 
        ///     and to write it during the commit phase.
        /// </summary>
        private sealed class EndCheckSumReq : IReqWrite
        {
            private readonly BlittableWriter _writer;

            /// <summary>
            ///     Checksum preserved in the call to <see cref="WithSpan"/>,
            ///     because the value in the writer will be overwritten.
            /// </summary>
            private uint _checksum;

            public EndCheckSumReq(BlittableWriter writer) =>
                _writer = writer ?? throw new ArgumentNullException(nameof(writer));

            public int Size => sizeof(int);

            public void BlitTo(Span<byte> destination) =>
                MemoryMarshal.Write(destination, ref _checksum);

            public void WithSpan(WithSpan.ReadOnly onSpan)
            {
                _checksum = Checksum.FinalizeCRC32(_writer._checksumSoFar);
                Span<byte> span = stackalloc byte[sizeof(int)];
                BlitTo(span);
                onSpan(span);
            }
        }
    }
}
